#include "OrblitTexturesInternal.h"

// A file handed in, as a texture with something to show at once.
//
// KTX 2 is taken as it is, Basis is transcoded to a format the device
// samples, and a PNG or JPEG is measured now and decoded later. Each returns
// a texture the moment it is asked for — its smallest level filled with the
// placeholder where nothing has arrived — and leaves the rest queued.
//
// Part of `orblit::TextureQueue`; see OrblitTexturesInternal.h.

namespace orblit {

using namespace textures;

Texture *TextureQueue::push(const Request &request, std::string &why) {
  why.clear();
  const uint8_t *data =
      request.shared ? request.shared->data() : request.data;
  const size_t size = request.shared ? request.shared->size() : request.size;
  if (data == nullptr || size == 0) {
    why = "It is empty.";
    return nullptr;
  }
  if (_stopping) {
    why = "The renderer is shutting down.";
    return nullptr;
  }

  const double started = now();
  Texture *texture = nullptr;
  if (request.mime == kKtx2 || ktx2::isKtx2(data, size)) {
    texture = pushKtx2(request, data, size, why);
  } else if (request.mime.empty() || request.mime == "image/png" ||
             request.mime == "image/jpeg") {
    texture = pushPicture(request, data, size, why);
  } else {
    why = format("%s is not an image type this renderer reads.",
                 request.mime.c_str());
  }
  if (texture != nullptr) {
    std::lock_guard<std::mutex> hold(_lock);
    _counts[request.client].pushed++;
  }
  _pushSeconds += now() - started;
  _pushSecondsEver += now() - started;
  return texture;
}

Texture *TextureQueue::pushKtx2(const Request &request, const uint8_t *data,
                                size_t size, std::string &why) {
  ktx2::Header header;
  why = ktx2::read(data, size, header);
  if (!why.empty()) return nullptr;
  if (header.basis) return pushBasis(request, data, size, header, why);

  if (header.faces != 1) {
    why = "It is a cubemap, and this is loading a 2D texture.";
    return nullptr;
  }

  // Sampled as the material asks: the sRGB and linear forms of a block
  // format are the same bytes, so a map cooked one way is read the other at
  // no cost. Where the format has no twin, or the device lacks it, the file's
  // own is used.
  const ktx2::Format *format =
      sampledAs(*header.format, request.srgb ? 1 : 0, false);
  if (format == nullptr) {
    why = orblit::format("This device cannot sample %s.", header.format->name);
    return nullptr;
  }
  if (format->srgb != request.srgb &&
      ktx2::withTransfer(*format, request.srgb) != nullptr) {
    // Drawn, but not in the colour space it is used as. Said rather than
    // refused: a texture a little too light or dark is easier to find than
    // one that is missing.
    log("[orblit] %s is sampled as %s: this device has no %s",
        request.name.c_str(), format->name,
        ktx2::withTransfer(*format, request.srgb)->name);
  }
  const GpuFormat *gpu = gpuFormatOf(format->vkFormat);
  if (gpu == nullptr) {
    why = orblit::format("%s has no Filament format.", format->name);
    return nullptr;
  }

  const uint32_t skip = ktx2::levelsToSkip(header, _maxSide);
  const uint32_t width = ktx2::levelWidth(header, skip);
  const uint32_t height = ktx2::levelHeight(header, skip);
  if (std::max(width, height) > _deviceLargest) {
    why = orblit::format("It is %u by %u, larger than this device's largest "
                         "texture, %u, and has no smaller level to use "
                         "instead.",
                         width, height, _deviceLargest);
    return nullptr;
  }

  // A header of nought levels asks for mipmaps to be made, which Filament
  // can do for an uncompressed format and nothing else.
  const bool generate =
      header.generateMipmaps && !gpu->compressed &&
      Texture::isTextureFormatMipmappable(_engine, gpu->internal);
  const uint32_t levels =
      generate ? mipLevels(width, height) : header.levels - skip;

  Texture::Usage usage = Texture::Usage::DEFAULT;
  if (generate) usage = usage | Texture::Usage::GEN_MIPMAPPABLE;
  Texture *texture = Texture::Builder()
                         .width(width)
                         .height(height)
                         .levels(uint8_t(levels))
                         .format(gpu->internal)
                         .sampler(Texture::Sampler::SAMPLER_2D)
                         .usage(usage)
                         .build(_engine);
  if (texture == nullptr) {
    why = "Filament would not make a texture of it.";
    return nullptr;
  }
  auto item = std::make_shared<Item>();
  item->storage = storageOf(*texture, *format);
  if (!generate || placeholderBeforeGeneratedLevels(_engine)) {
    item->placeholder = format;
    item->placeholderLevel = levels - 1;
  }
  placeAtPush(request, *item, texture);
  item->kind = Item::Kind::ktx2;
  item->client = request.client;
  item->owner = request.owner;
  item->name = request.name;
  item->texture = texture;
  item->source = request.shared
                     ? request.shared
                     : std::make_shared<const std::vector<uint8_t>>(
                           data, data + size);
  item->header = std::move(header);
  item->gpu = gpu;
  item->skip = skip;
  item->generateMipmaps = generate;
  enqueue(item);
  return texture;
}

Texture *TextureQueue::pushBasis(const Request &request, const uint8_t *data,
                                 size_t size, const ktx2::Header &header,
                                 std::string &why) {
  // A low tier leaves out Basis levels too, by handing the transcoder a copy
  // of the file that starts lower down.
  std::vector<uint8_t> smaller;
  const uint32_t skip = ktx2::levelsToSkip(header, _maxSide);
  if (skip > 0) {
    why = ktx2::withoutLargestLevels(data, size, header, skip, smaller);
    if (!why.empty()) return nullptr;
    data = smaller.data();
    size = smaller.size();
  }
  const uint32_t width = ktx2::levelWidth(header, skip);
  const uint32_t height = ktx2::levelHeight(header, skip);
  if (std::max(width, height) > _deviceLargest) {
    why = orblit::format("It is %u by %u, larger than this device's largest "
                         "texture, %u.",
                         width, height, _deviceLargest);
    return nullptr;
  }

#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
  // In a browser the file goes to a decoder worker, so it is not copied and
  // started here as asyncCreate would: the target is chosen from the header
  // exactly as Ktx2Reader chooses it, and the texture made as it makes it.
  (void)header;
  std::vector<web::BasisCandidate> candidates;
  for (InternalFormat target : kBasisTargets) {
    if (const ktx2::Format *format = formatOfInternal(target)) {
      candidates.push_back(
          {format->vkFormat, Texture::isTextureFormatSupported(_engine, target)});
    }
  }
  web::BasisTarget target;
  why = web::chooseBasisTarget(data, size, request.srgb, candidates.data(),
                               candidates.size(), target);
  if (!why.empty()) return nullptr;
  const GpuFormat *gpu = gpuFormatOf(target.vkFormat);
  if (gpu == nullptr || target.levels == 0) {
    why = "Filament's Basis reader would not take it: it may be a cubemap or "
          "an array, or transcode to nothing this device samples.";
    return nullptr;
  }
  Texture *texture = Texture::Builder()
                         .width(target.width)
                         .height(target.height)
                         .levels(uint8_t(target.levels))
                         .sampler(Texture::Sampler::SAMPLER_2D)
                         .format(gpu->internal)
                         .build(_engine);
  if (texture == nullptr) {
    why = "Filament would not make a texture of it.";
    return nullptr;
  }
  auto item = std::make_shared<Item>();
  item->storage = storageOf(*texture, *ktx2::formatOf(target.vkFormat));
  item->placeholder = ktx2::formatOf(target.vkFormat);
  item->placeholderLevel = target.levels - 1;
  placeAtPush(request, *item, texture);
  item->kind = Item::Kind::basis;
  item->client = request.client;
  item->owner = request.owner;
  item->name = request.name;
  item->texture = texture;
  item->gpu = gpu;
  item->basisFormat = target.transcoderFormat;
  item->basisCompressed = target.compressed;
  if (!smaller.empty()) {
    item->source = std::make_shared<const std::vector<uint8_t>>(std::move(smaller));
  } else if (request.shared) {
    item->source = request.shared;
  } else {
    item->source = std::make_shared<const std::vector<uint8_t>>(data, data + size);
  }
  enqueue(item);
  return texture;
#else
  using Transfer = ktxreader::Ktx2Reader::TransferFunction;
  ktxreader::Ktx2Reader::Async *async = _basis->asyncCreate(
      data, size, request.srgb ? Transfer::sRGB : Transfer::LINEAR);
  if (async == nullptr) {
    const bool marked = header.transfer != ktx2::Transfer::unspecified;
    const bool mismatch =
        marked && (header.transfer == ktx2::Transfer::srgb) != request.srgb;
    why = mismatch
              ? orblit::format("It is Basis marked %s, and is used where %s "
                               "is needed; Basis cannot be read the other "
                               "way.",
                               request.srgb ? "linear" : "sRGB",
                               request.srgb ? "sRGB" : "linear")
              : "Filament's Basis reader would not take it: it may be a "
                "cubemap or an array, or transcode to nothing this device "
                "samples.";
    return nullptr;
  }
  Texture *texture = async->getTexture();
  auto item = std::make_shared<Item>();
  if (const ktx2::Format *format = formatOfInternal(texture->getFormat())) {
    item->storage = storageOf(*texture, *format);
    item->placeholder = format;
    item->placeholderLevel = uint32_t(texture->getLevels()) - 1;
  }
  placeAtPush(request, *item, texture);
  item->kind = Item::Kind::basis;
  item->client = request.client;
  item->owner = request.owner;
  item->name = request.name;
  item->texture = texture;
  item->async = async;
  enqueue(item);
  return texture;
#endif
}

Texture *TextureQueue::pushPicture(const Request &request, const uint8_t *data,
                                   size_t size, std::string &why) {
  int wide = 0;
  int tall = 0;
  int channels = 0;
  if (size > size_t(INT_MAX) ||
      !stbi_info_from_memory(data, int(size), &wide, &tall, &channels) ||
      wide <= 0 || tall <= 0) {
    why = "It is not a PNG or JPEG that can be read.";
    return nullptr;
  }

  // Pictures carry no mipmaps to leave out, so a picture larger than the
  // limit is halved on the decoder's thread until it fits.
  uint32_t width = uint32_t(wide);
  uint32_t height = uint32_t(tall);
  uint32_t skip = 0;
  while (_maxSide != 0 && std::max(width, height) > _maxSide &&
         (width > 1 || height > 1)) {
    width = std::max<uint32_t>(1, width / 2);
    height = std::max<uint32_t>(1, height / 2);
    skip++;
  }
  if (std::max(width, height) > _deviceLargest) {
    why = orblit::format("It is %u by %u, larger than this device's largest "
                         "texture, %u.",
                         width, height, _deviceLargest);
    return nullptr;
  }

  const InternalFormat internal = request.srgb ? IF::SRGB8_A8 : IF::RGBA8;
  const uint32_t levels = mipLevels(width, height);
  Texture *texture =
      Texture::Builder()
          .width(width)
          .height(height)
          .levels(uint8_t(levels))
          .format(internal)
          .sampler(Texture::Sampler::SAMPLER_2D)
          .usage(Texture::Usage::DEFAULT | Texture::Usage::GEN_MIPMAPPABLE)
          .build(_engine);
  if (texture == nullptr) {
    why = "Filament would not make a texture of it.";
    return nullptr;
  }
  const ktx2::Format &pixels = *ktx2::formatOf(request.srgb ? 43 : 37);
  auto item = std::make_shared<Item>();
  item->storage = storageOf(*texture, pixels);
  if (placeholderBeforeGeneratedLevels(_engine)) {
    item->placeholder = &pixels;
    item->placeholderLevel = levels - 1;
  }
  placeAtPush(request, *item, texture);
  item->kind = Item::Kind::picture;
  item->client = request.client;
  item->owner = request.owner;
  item->name = request.name;
  item->texture = texture;
  item->source = request.shared
                     ? request.shared
                     : std::make_shared<const std::vector<uint8_t>>(
                           data, data + size);
  item->skip = skip;
  item->srgb = request.srgb;
  enqueue(item);
  return texture;
}

bool TextureQueue::startsAsPlaceholder(const ktx2::Format &format) const {
  // Measured, per format, on Apple silicon through Filament's Metal backend:
  // a texture of these that nothing has been written into samples as the
  // placeholder does, even straight after other textures' memory has been
  // freed (orblit_textures_check, "never-written storage"). Metal gives a
  // texture zeros, and zeros in these formats decode to black with no
  // alpha — within two levels of it for ETC2 without alpha, whose smallest
  // modifier is two. Not ASTC, where zeros are a reserved block that decodes
  // as the error colour, and not the uncompressed formats, which sample as
  // magenta until written. Other backends are not measured, so they keep the
  // placeholder.
  if (_engine.getBackend() != filament::backend::Backend::METAL) return false;
  const uint32_t vk = format.vkFormat;
  return (vk >= 131 && vk <= 134) ||  // BC1
         (vk >= 137 && vk <= 142) ||  // BC3, BC4, BC5
         vk == 145 || vk == 146 ||    // BC7
         (vk >= 147 && vk <= 156);    // ETC2 and EAC
}

uint64_t TextureQueue::storageOf(const Texture &texture,
                                 const ktx2::Format &format) {
  uint64_t bytes = 0;
  for (size_t level = 0; level < texture.getLevels(); level++) {
    bytes += uint64_t((texture.getWidth(level) + format.blockWidth - 1) /
                      format.blockWidth) *
             ((texture.getHeight(level) + format.blockHeight - 1) /
              format.blockHeight) *
             format.bytesPerBlock;
  }
  return bytes;
}

void TextureQueue::placeAtPush(const Request &request, Item &item,
                               Texture *texture) {
  item.texture = texture;
  // A model's textures are hidden with the model until pump has written into
  // every one of them (see unprimed), so their placeholders wait for it: a
  // write makes the GPU find memory for the whole texture, and four hundred of
  // them at once is a frame of a second.
  if (request.owner != nullptr || item.placeholder == nullptr) return;
  // Sampled from the next frame. Where the storage already reads as the
  // placeholder nothing need be written, and the GPU finds the memory when it
  // is first drawn or written.
  if (startsAsPlaceholder(*item.placeholder)) return;
  item.written = writePlaceholder(texture, *item.placeholder,
                                  item.placeholderLevel, true);
  if (item.written) _placeholderBytes += item.storage;
}

bool TextureQueue::writePlaceholder(Texture *texture,
                                    const ktx2::Format &format,
                                    uint32_t level, bool whole) {
  const GpuFormat *gpu = gpuFormatOf(format.vkFormat);
  if (gpu == nullptr) return false;
  const uint32_t levelWidth = uint32_t(texture->getWidth(level));
  const uint32_t levelHeight = uint32_t(texture->getHeight(level));
  // One block is enough where the rest of the storage already reads as the
  // placeholder: what it is written for is making the memory.
  const uint32_t width =
      whole ? levelWidth : std::min<uint32_t>(levelWidth, format.blockWidth);
  const uint32_t height =
      whole ? levelHeight : std::min<uint32_t>(levelHeight, format.blockHeight);
  const size_t across = (width + format.blockWidth - 1) / format.blockWidth;
  const size_t down = (height + format.blockHeight - 1) / format.blockHeight;
  const size_t bytes = across * down * format.bytesPerBlock;

  std::shared_ptr<std::vector<uint8_t>> &kept =
      _placeholders[{format.vkFormat, bytes}];
  if (!kept) {
    uint8_t block[16];
    placeholderBlock(format, block);
    kept = std::make_shared<std::vector<uint8_t>>(bytes);
    for (size_t at = 0; at + format.bytesPerBlock <= bytes;
         at += format.bytesPerBlock) {
      memcpy(kept->data() + at, block, format.bytesPerBlock);
    }
  }

  // The buffer is shared, so what Filament is given to let go of is a
  // reference to it rather than the bytes.
  auto *holder = new std::shared_ptr<std::vector<uint8_t>>(kept);
  const auto letGo = [](void *, size_t, void *user) {
    delete static_cast<std::shared_ptr<std::vector<uint8_t>> *>(user);
  };
  if (gpu->compressed) {
    texture->setImage(_engine, level, 0, 0, width, height,
                      Texture::PixelBufferDescriptor(
                          kept->data(), bytes, gpu->compressedType,
                          uint32_t(bytes), letGo, holder));
  } else {
    texture->setImage(_engine, level, 0, 0, width, height,
                      Texture::PixelBufferDescriptor(
                          kept->data(), bytes, gpu->pixelFormat,
                          gpu->pixelType, letGo, holder));
  }
  return true;
}

}  // namespace orblit
