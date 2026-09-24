#include "OrblitTerrain.h"

#include <filament/Box.h>
#include <filament/IndexBuffer.h>
#include <filament/MaterialInstance.h>
#include <filament/RenderableManager.h>
#include <filament/Texture.h>
#include <filament/TextureSampler.h>
#include <filament/TransformManager.h>
#include <filament/VertexBuffer.h>
#include <math/mat4.h>
#include <math/vec2.h>
#include <math/vec4.h>
#include <utils/Entity.h>
#include <utils/EntityManager.h>

#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>
#include <map>
#include <set>

#include "terrain_material.h"

using namespace filament;
using namespace filament::math;

namespace orblit {

namespace {

using Notes = std::vector<std::pair<std::string, std::string>>;

/// Bytes a texel of a region's maps: a height, a cover word, a colour.
constexpr uint64_t kRegionTexelBytes = 12;

/// Bytes a vertex of the grid: where it is, then which way it faces.
constexpr uint8_t kVertexBytes = 3 * sizeof(float) + 4 * sizeof(int16_t);

/// Which way every vertex faces, as the quaternion Filament's tangent
/// attribute holds: a quarter turn about x, taking the normal Filament starts
/// from, +z, to +y, and leaving the tangent along +x. Normalised shorts, so
/// √½ is 23170.
constexpr int16_t kFacingUp[4] = {-23170, 0, 0, 23170};

/// Layers the region arrays start with, doubled as a terrain outgrows them.
constexpr uint32_t kFirstLayers = 4;

bool within(int32_t value, int32_t low, int32_t high) {
  return value >= low && value <= high;
}

bool powerOfTwo(int32_t value) {
  return value > 0 && (value & (value - 1)) == 0;
}

/// The largest power of two not above `value`, as its exponent: what a
/// region size is exactly, and one less than the mip levels a picture has.
int32_t log2Of(int32_t value) {
  int32_t shift = 0;
  while ((value >> (shift + 1)) != 0) shift++;
  return shift;
}

std::string describe(int32_t key, int32_t x, int32_t z) {
  return "terrain " + std::to_string(key) + ", region (" + std::to_string(x) +
         ", " + std::to_string(z) + ")";
}

/// Walks the three arrays in step and never past the end of any. Counts are
/// 64-bit whatever size_t is: a terrain's pictures can pass four gigabytes on
/// paper, and wasm's size_t is 32 bits.
class Reader {
 public:
  Reader(const int32_t *ints, size_t intCount, const float *floats,
         size_t floatCount, const uint8_t *data, size_t dataLength)
      : _ints(ints),
        _intCount(intCount),
        _floats(floats),
        _floatCount(floatCount),
        _data(data),
        _dataLength(dataLength) {}

  bool ints(uint64_t count, const int32_t *&out) {
    return take(_ints, _intCount, _intAt, count, out);
  }

  bool floats(uint64_t count, const float *&out) {
    return take(_floats, _floatCount, _floatAt, count, out);
  }

  bool bytes(uint64_t count, const uint8_t *&out) {
    return take(_data, _dataLength, _dataAt, count, out);
  }

  bool finished() const {
    return _intAt == _intCount && _floatAt == _floatCount &&
           _dataAt == _dataLength;
  }

 private:
  template <typename T>
  static bool take(const T *array, size_t length, size_t &at, uint64_t count,
                   const T *&out) {
    if (count > uint64_t(length - at)) return false;
    out = count == 0 ? nullptr : array + at;
    at += size_t(count);
    return true;
  }

  const int32_t *_ints;
  size_t _intCount;
  size_t _intAt = 0;
  const float *_floats;
  size_t _floatCount;
  size_t _floatAt = 0;
  const uint8_t *_data;
  size_t _dataLength;
  size_t _dataAt = 0;
};

TerrainParse readTerrain(Reader &read, std::vector<TerrainRequest> &requests) {
  const int32_t *head = nullptr;
  if (!read.ints(1, head)) return TerrainParse::length;
  if (head[0] < 0) return TerrainParse::range;
  const auto count = size_t(head[0]);

  for (size_t t = 0; t < count; t++) {
    const int32_t *ints = nullptr;
    if (!read.ints(kTerrainInts, ints)) return TerrainParse::length;
    TerrainRequest request;
    request.key = ints[0];
    request.flags = ints[1];
    request.regionSize = ints[2];
    request.meshSize = ints[3];
    request.levels = ints[4];
    request.autoSteep = ints[5];
    request.autoFlat = ints[6];
    request.setCount = ints[7];
    request.textureSize = ints[8];
    request.picturesArrived = ints[9] == 1;
    request.triplanar = uint32_t(ints[10]);
    const int32_t regionCount = ints[11];
    if (!powerOfTwo(request.regionSize) ||
        !within(request.regionSize, kTerrainMinRegionSize,
                kTerrainMaxRegionSize) ||
        request.meshSize % 2 != 0 ||
        !within(request.meshSize, kTerrainMinMeshSize, kTerrainMaxMeshSize) ||
        !within(request.levels, 1, kTerrainMaxLevels) ||
        !within(request.autoSteep, 0, kTerrainMaxSets - 1) ||
        !within(request.autoFlat, 0, kTerrainMaxSets - 1) ||
        !within(request.setCount, 0, kTerrainMaxSets) ||
        !within(ints[9], 0, 1) ||
        !within(regionCount, 0, kTerrainMaxRegions)) {
      return TerrainParse::range;
    }
    const bool pictures = request.picturesArrived && request.setCount > 0;
    if (pictures &&
        !within(request.textureSize, 1, kTerrainMaxTextureSize)) {
      return TerrainParse::range;
    }
    for (const TerrainRequest &earlier : requests) {
      if (earlier.key == request.key) return TerrainParse::range;
    }

    const float *params = nullptr;
    if (!read.floats(kTerrainParams, params)) return TerrainParse::length;
    request.spacing = params[0];
    request.blendSharpness = params[1];
    request.autoSlope = params[2];
    request.autoHeightFalloff = params[3];
    if (!std::isfinite(request.spacing) || request.spacing <= 0.0f ||
        !std::isfinite(request.blendSharpness) ||
        !std::isfinite(request.autoSlope) ||
        !std::isfinite(request.autoHeightFalloff)) {
      return TerrainParse::range;
    }
    request.blendSharpness = std::clamp(request.blendSharpness, 0.0f, 1.0f);
    if (!read.floats(uint64_t(request.setCount) * kTerrainSetParams,
                     request.tileSizes)) {
      return TerrainParse::length;
    }
    for (int32_t s = 0; s < request.setCount; s++) {
      const float size = request.tileSizes[size_t(s) * kTerrainSetParams];
      if (!std::isfinite(size) || size <= 0.0f) return TerrainParse::range;
    }

    const int32_t *rows = nullptr;
    if (!read.ints(uint64_t(regionCount) * kTerrainRegionInts, rows)) {
      return TerrainParse::length;
    }
    if (pictures) {
      const auto side = uint64_t(request.textureSize);
      if (!read.bytes(uint64_t(request.setCount) * side * side * 4 * 2,
                      request.pictures)) {
        return TerrainParse::length;
      }
    }
    const uint64_t texels =
        uint64_t(request.regionSize) * uint64_t(request.regionSize);
    std::set<std::pair<int32_t, int32_t>> seen;
    request.regions.reserve(size_t(regionCount));
    for (int32_t r = 0; r < regionCount; r++) {
      const int32_t *row = rows + size_t(r) * kTerrainRegionInts;
      if (!within(row[2], 0, 1)) return TerrainParse::range;
      if (!seen.insert({row[0], row[1]}).second) return TerrainParse::range;
      TerrainRegionRequest region;
      region.x = row[0];
      region.z = row[1];
      region.arrived = row[2] == 1;
      if (region.arrived &&
          !read.bytes(texels * kRegionTexelBytes, region.maps)) {
        return TerrainParse::length;
      }
      request.regions.push_back(region);
    }
    requests.push_back(std::move(request));
  }
  return TerrainParse::ok;
}

/// An array texture of `layers` layers, each `side` square.
Texture *arrayTexture(Engine &engine, uint32_t side, uint32_t layers,
                      uint8_t levels, Texture::InternalFormat format,
                      bool mipmapped) {
  return Texture::Builder()
      .width(side)
      .height(side)
      .depth(layers)
      .levels(levels)
      .sampler(Texture::Sampler::SAMPLER_2D_ARRAY)
      .format(format)
      .usage(mipmapped ? Texture::Usage::DEFAULT | Texture::Usage::GEN_MIPMAPPABLE
                       : Texture::Usage::DEFAULT)
      .build(engine);
}

/// Hands Filament its own copy of `bytes`, freed once uploaded. The maps
/// kept here can change or go before the upload happens.
Texture::PixelBufferDescriptor copied(const uint8_t *bytes, size_t length,
                                      Texture::Format format,
                                      Texture::Type type) {
  auto *copy = new uint8_t[length];
  std::memcpy(copy, bytes, length);
  return Texture::PixelBufferDescriptor(
      copy, length, format, type,
      [](void *buffer, size_t, void *) {
        delete[] static_cast<uint8_t *>(buffer);
      });
}

/// Read a texel at a time and never mixed: a height between two texels is
/// the shader's to work out, and a cover word has no in-between.
TextureSampler exactSampler() {
  return TextureSampler(TextureSampler::MinFilter::NEAREST,
                        TextureSampler::MagFilter::NEAREST,
                        TextureSampler::WrapMode::CLAMP_TO_EDGE);
}

TextureSampler pictureSampler() {
  TextureSampler sampler(TextureSampler::MinFilter::LINEAR_MIPMAP_LINEAR,
                         TextureSampler::MagFilter::LINEAR,
                         TextureSampler::WrapMode::REPEAT);
  // Ground is seen at a glancing angle more than anything else in a scene,
  // which is exactly where a picture blurs without this.
  sampler.setAnisotropy(8.0f);
  return sampler;
}

}  // namespace

TerrainParse parseTerrain(const int32_t *ints, size_t intCount,
                          const float *floats, size_t floatCount,
                          const uint8_t *data, size_t dataLength,
                          std::vector<TerrainRequest> &requests) {
  requests.clear();
  // Nothing at all is no terrain, which is what a scene that never had one
  // sends.
  if (intCount == 0) {
    return floatCount == 0 && dataLength == 0 ? TerrainParse::ok
                                              : TerrainParse::length;
  }
  Reader read(ints, intCount, floats, floatCount, data, dataLength);
  TerrainParse result = readTerrain(read, requests);
  if (result == TerrainParse::ok && !read.finished()) {
    result = TerrainParse::length;
  }
  if (result != TerrainParse::ok) requests.clear();
  return result;
}

/// One terrain: its grid, a renderable a level, its maps on the GPU and a
/// copy of them here, and what it was last told.
class TerrainField {
 public:
  TerrainField(Engine &engine, Scene &scene, Material &material)
      : _engine(engine), _scene(scene) {
    _instance = material.createInstance();
  }

  ~TerrainField() {
    destroyLevels();
    destroyGrid();
    _engine.destroy(_instance);
    for (Texture *texture :
         {_heights, _cover, _colours, _regionMap, _albedo, _normals}) {
      if (texture != nullptr) _engine.destroy(texture);
    }
  }

  TerrainField(const TerrainField &) = delete;
  TerrainField &operator=(const TerrainField &) = delete;

  void apply(const TerrainRequest &request, Notes &notes) {
    _key = request.key;
    if (request.regionSize != _regionSize) {
      // Every layer is the wrong size now. The regions that came with this
      // message are loaded again below; the rest are gone until sent. The
      // old arrays stay bound until new ones replace them, because a
      // material instance holding a destroyed texture is a crash.
      _regions.clear();
      _free.clear();
      _used = 0;
      _capacity = 0;
      _regionSize = request.regionSize;
    }
    if (request.meshSize != _meshSize) {
      buildGrid(request.meshSize);
      _levelCount = -1;
    }
    if (request.levels != _levelCount || request.flags != _flags) {
      buildLevels(request.levels, request.flags);
    }
    _spacing = request.spacing;
    setSettings(request);
    if (request.picturesArrived || _albedo == nullptr) loadPictures(request);
    placeRegions(request, notes);
  }

  /// Every level square round the camera, each with the hole its inner
  /// neighbour fills.
  void update(const double3 &eye) {
    if (!_shown) return;
    _instance->setParameter("cameraAt", float3(eye));

    TransformManager &transforms = _engine.getTransformManager();
    RenderableManager &renderables = _engine.getRenderableManager();
    const auto half = double(_meshSize);
    for (size_t k = 0; k < _levels.size(); k++) {
      Level &level = _levels[k];
      // A level's cells are twice its inner neighbour's, and it snaps to a
      // whole cell of the level outside it. Its vertices then sit on every
      // other one of its inner neighbour's, and every vertex of the next.
      const double unit = std::ldexp(double(_spacing), int(k));
      const double next = unit * 2.0;
      const double x = std::floor(eye.x / next) * next - half * unit;
      const double z = std::floor(eye.z / next) * next - half * unit;
      transforms.setTransform(
          transforms.getInstance(level.entity),
          mat4f::translation(float3{float(x), 0.0f, float(z)}) *
              mat4f::scaling(float3{float(unit), 1.0f, float(unit)}));

      // Where the inner square sits within this one's hole: half a mesh in,
      // and a cell further along each axis the camera is in the odd cell of.
      int32_t range = -1;
      if (k > 0) {
        const auto cellX = int64_t(std::floor(eye.x / unit));
        const auto cellZ = int64_t(std::floor(eye.z / unit));
        range = int32_t((cellX & 1) + 2 * (cellZ & 1));
      }
      if (range != level.range) {
        const size_t offset =
            range < 0 ? 0 : _wholeCount + size_t(range) * _holedCount;
        const size_t count = range < 0 ? _wholeCount : _holedCount;
        renderables.setGeometryAt(renderables.getInstance(level.entity), 0,
                                  RenderableManager::PrimitiveType::TRIANGLES,
                                  _grid, _indices, offset, count);
        level.range = range;
      }
    }
  }

 private:
  struct Region {
    int32_t layer = 0;
    /// Heights, cover and colours end to end, as they arrived.
    std::vector<uint8_t> maps;
    float low = 0.0f;
    float high = 0.0f;
    bool uploaded = false;
  };

  struct Level {
    utils::Entity entity;
    /// Which index range it draws: -1 the whole grid, 0 to 3 a holed one.
    int32_t range = -1;
  };

  void setSettings(const TerrainRequest &request) {
    _instance->setParameter("regionShift", log2Of(request.regionSize));
    _instance->setParameter("spacing", request.spacing);
    _instance->setParameter("meshSize", request.meshSize);
    _instance->setParameter("autoSteep", request.autoSteep);
    _instance->setParameter("autoFlat", request.autoFlat);
    _instance->setParameter("autoSlope", request.autoSlope);
    _instance->setParameter("autoHeightFalloff", request.autoHeightFalloff);
    _instance->setParameter("blendSharpness", request.blendSharpness);
    _instance->setParameter("triplanar", request.triplanar);
    float4 sizes[kTerrainMaxSets / 4];
    for (auto &size : sizes) size = float4(1.0f);
    for (int32_t s = 0; s < request.setCount; s++) {
      sizes[s / 4][s % 4] = request.tileSizes[size_t(s) * kTerrainSetParams];
    }
    _instance->setParameter("tileSizes", sizes, kTerrainMaxSets / 4);
  }

  /// The sets' pictures, or a plain layer for a terrain that has none.
  void loadPictures(const TerrainRequest &request) {
    const bool real = request.picturesArrived && request.setCount > 0;
    const uint32_t side = real ? uint32_t(request.textureSize) : 1;
    const uint32_t layers = real ? uint32_t(request.setCount) : 1;
    const auto levels = uint8_t(real ? log2Of(int32_t(side)) + 1 : 1);
    const size_t bytes = size_t(side) * side * 4 * layers;

    // Pale grey, half height; facing straight out, fairly rough.
    static const uint8_t kPlainAlbedo[4] = {190, 190, 190, 128};
    static const uint8_t kPlainNormal[4] = {128, 128, 255, 230};
    const uint8_t *albedoBytes = real ? request.pictures : kPlainAlbedo;
    const uint8_t *normalBytes = real ? request.pictures + bytes : kPlainNormal;

    Texture *albedo = arrayTexture(_engine, side, layers, levels,
                                   Texture::InternalFormat::SRGB8_A8, real);
    Texture *normals = arrayTexture(_engine, side, layers, levels,
                                    Texture::InternalFormat::RGBA8, real);
    albedo->setImage(_engine, 0, 0, 0, 0, side, side, layers,
                     copied(albedoBytes, bytes, Texture::Format::RGBA,
                            Texture::Type::UBYTE));
    normals->setImage(_engine, 0, 0, 0, 0, side, side, layers,
                      copied(normalBytes, bytes, Texture::Format::RGBA,
                             Texture::Type::UBYTE));
    if (levels > 1) {
      albedo->generateMipmaps(_engine);
      normals->generateMipmaps(_engine);
    }

    _instance->setParameter("albedo", albedo, pictureSampler());
    _instance->setParameter("normals", normals, pictureSampler());
    if (_albedo != nullptr) _engine.destroy(_albedo);
    if (_normals != nullptr) _engine.destroy(_normals);
    _albedo = albedo;
    _normals = normals;
  }

  void placeRegions(const TerrainRequest &request, Notes &notes) {
    using Key = std::pair<int32_t, int32_t>;

    // The regions a map one texel a region can hold, taken in the order
    // given: one that would stretch it past kTerrainMaxSpan is left out.
    std::vector<const TerrainRegionRequest *> accepted;
    std::set<Key> named;
    int32_t minX = 0;
    int32_t maxX = 0;
    int32_t minZ = 0;
    int32_t maxZ = 0;
    for (const TerrainRegionRequest &region : request.regions) {
      const bool first = accepted.empty();
      const int32_t lowX = first ? region.x : std::min(minX, region.x);
      const int32_t highX = first ? region.x : std::max(maxX, region.x);
      const int32_t lowZ = first ? region.z : std::min(minZ, region.z);
      const int32_t highZ = first ? region.z : std::max(maxZ, region.z);
      if (int64_t(highX) - lowX >= kTerrainMaxSpan ||
          int64_t(highZ) - lowZ >= kTerrainMaxSpan) {
        notes.emplace_back(describe(_key, region.x, region.z),
                           "It is " + std::to_string(kTerrainMaxSpan) +
                               " or more regions from others in this "
                               "terrain, so it is not drawn.");
        continue;
      }
      minX = lowX;
      maxX = highX;
      minZ = lowZ;
      maxZ = highZ;
      accepted.push_back(&region);
      named.insert({region.x, region.z});
    }

    // Regions no longer named give their layers back.
    for (auto it = _regions.begin(); it != _regions.end();) {
      if (named.count(it->first) == 0) {
        _free.push_back(it->second.layer);
        it = _regions.erase(it);
      } else {
        ++it;
      }
    }

    const size_t texels = size_t(_regionSize) * size_t(_regionSize);
    for (const TerrainRegionRequest *region : accepted) {
      const Key key{region->x, region->z};
      auto found = _regions.find(key);
      if (!region->arrived) {
        if (found == _regions.end()) {
          notes.emplace_back(describe(_key, region->x, region->z),
                             "Its maps have not been sent, so it is not "
                             "drawn.");
        }
        continue;
      }
      if (found == _regions.end()) {
        Region fresh;
        if (!_free.empty()) {
          fresh.layer = _free.back();
          _free.pop_back();
        } else {
          fresh.layer = _used++;
        }
        found = _regions.emplace(key, std::move(fresh)).first;
      }
      Region &kept = found->second;
      kept.maps.assign(region->maps, region->maps + texels * kRegionTexelBytes);
      kept.uploaded = false;
      measure(kept, texels);
    }

    if (uint32_t(_used) > _capacity) grow();
    for (auto &entry : _regions) {
      if (!entry.second.uploaded) upload(entry.second);
    }
    buildRegionMap(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
    fitBoxes();
    show(!_regions.empty());
  }

  /// The lowest and highest the region's ground reaches, for the boxes the
  /// levels are culled by.
  static void measure(Region &region, size_t texels) {
    float low = std::numeric_limits<float>::max();
    float high = std::numeric_limits<float>::lowest();
    for (size_t i = 0; i < texels; i++) {
      float height;
      std::memcpy(&height, region.maps.data() + i * sizeof(float),
                  sizeof(float));
      if (!std::isfinite(height)) continue;
      low = std::min(low, height);
      high = std::max(high, height);
    }
    if (low > high) low = high = 0.0f;
    region.low = low;
    region.high = high;
  }

  /// Arrays with room for every region, and every region in them. The
  /// regions keep their layers, so nothing else changes.
  void grow() {
    uint32_t capacity = std::max(_capacity, kFirstLayers);
    while (capacity < uint32_t(_used)) capacity *= 2;
    capacity = std::min(capacity, uint32_t(kTerrainMaxRegions));

    const auto side = uint32_t(_regionSize);
    Texture *heights = arrayTexture(_engine, side, capacity, 1,
                                    Texture::InternalFormat::R32F, false);
    Texture *cover = arrayTexture(_engine, side, capacity, 1,
                                  Texture::InternalFormat::R32UI, false);
    Texture *colours = arrayTexture(_engine, side, capacity, 1,
                                    Texture::InternalFormat::SRGB8_A8, false);
    _instance->setParameter("heights", heights, exactSampler());
    _instance->setParameter("cover", cover, exactSampler());
    _instance->setParameter("colours", colours, exactSampler());
    for (Texture *old : {_heights, _cover, _colours}) {
      if (old != nullptr) _engine.destroy(old);
    }
    _heights = heights;
    _cover = cover;
    _colours = colours;
    _capacity = capacity;
    for (auto &entry : _regions) entry.second.uploaded = false;
  }

  void upload(Region &region) {
    const auto side = uint32_t(_regionSize);
    const size_t plane = size_t(side) * side * 4;
    const auto layer = uint32_t(region.layer);
    const uint8_t *maps = region.maps.data();
    _heights->setImage(_engine, 0, 0, 0, layer, side, side, 1,
                       copied(maps, plane, Texture::Format::R,
                              Texture::Type::FLOAT));
    _cover->setImage(_engine, 0, 0, 0, layer, side, side, 1,
                     copied(maps + plane, plane, Texture::Format::R_INTEGER,
                            Texture::Type::UINT));
    _colours->setImage(_engine, 0, 0, 0, layer, side, side, 1,
                       copied(maps + plane * 2, plane, Texture::Format::RGBA,
                              Texture::Type::UBYTE));
    region.uploaded = true;
  }

  /// One texel a region across the span the regions cover, holding each
  /// one's layer plus one, and nought where there is none.
  void buildRegionMap(int32_t originX, int32_t originZ, int32_t spanX,
                      int32_t spanZ) {
    if (_regions.empty()) {
      originX = originZ = 0;
      spanX = spanZ = 1;
    }
    std::vector<uint32_t> cells(size_t(spanX) * size_t(spanZ), 0);
    for (const auto &entry : _regions) {
      const int32_t x = entry.first.first - originX;
      const int32_t z = entry.first.second - originZ;
      cells[size_t(z) * size_t(spanX) + size_t(x)] =
          uint32_t(entry.second.layer) + 1;
    }

    if (_regionMap == nullptr || int32_t(_regionMap->getWidth()) != spanX ||
        int32_t(_regionMap->getHeight()) != spanZ) {
      Texture *map = Texture::Builder()
                         .width(uint32_t(spanX))
                         .height(uint32_t(spanZ))
                         .levels(1)
                         .sampler(Texture::Sampler::SAMPLER_2D)
                         .format(Texture::InternalFormat::R32UI)
                         .build(_engine);
      _instance->setParameter("regions", map, exactSampler());
      if (_regionMap != nullptr) _engine.destroy(_regionMap);
      _regionMap = map;
    }
    _regionMap->setImage(
        _engine, 0,
        copied(reinterpret_cast<const uint8_t *>(cells.data()),
               cells.size() * sizeof(uint32_t), Texture::Format::R_INTEGER,
               Texture::Type::UINT));
    _instance->setParameter("regionOrigin", int2{originX, originZ});
    _instance->setParameter("regionSpan", int2{spanX, spanZ});
  }

  /// Every level's box, in the grid's own units: the whole square across,
  /// and as high as the ground goes. The same for every level, since each is
  /// the grid scaled.
  void fitBoxes() {
    float low = std::numeric_limits<float>::max();
    float high = std::numeric_limits<float>::lowest();
    for (const auto &entry : _regions) {
      low = std::min(low, entry.second.low);
      high = std::max(high, entry.second.high);
    }
    if (low > high) low = high = 0.0f;
    _box = boxFor(low, high);
    RenderableManager &renderables = _engine.getRenderableManager();
    for (const Level &level : _levels) {
      renderables.setAxisAlignedBoundingBox(
          renderables.getInstance(level.entity), _box);
    }
  }

  Box boxFor(float low, float high) const {
    const auto across = float(_meshSize);
    // Flat ground has a flat box, and a box with no height at all is one
    // some culling arithmetic divides by; a sliver keeps it honest.
    return Box{{across, (low + high) * 0.5f, across},
               {across, std::max((high - low) * 0.5f, 1e-3f), across}};
  }

  /// The grid: (2m + 1)² vertices a unit apart, and five index ranges over
  /// them — the whole of it, then the four ways of leaving an m-square hole
  /// half a mesh in, a cell further along x, z, both or neither.
  void buildGrid(int32_t meshSize) {
    destroyLevels();
    destroyGrid();
    _meshSize = meshSize;

    const auto half = uint32_t(meshSize);
    const uint32_t cells = half * 2;
    const uint32_t row = cells + 1;
    const uint32_t vertices = row * row;
    auto *bytes = new uint8_t[size_t(vertices) * kVertexBytes];
    for (uint32_t j = 0; j < row; j++) {
      for (uint32_t i = 0; i < row; i++) {
        uint8_t *vertex = bytes + size_t(i + j * row) * kVertexBytes;
        const float at[3] = {float(i), 0.0f, float(j)};
        std::memcpy(vertex, at, sizeof(at));
        std::memcpy(vertex + sizeof(at), kFacingUp, sizeof(kFacingUp));
      }
    }
    _grid = VertexBuffer::Builder()
                .vertexCount(vertices)
                .bufferCount(1)
                .attribute(VertexAttribute::POSITION, 0,
                           VertexBuffer::AttributeType::FLOAT3, 0, kVertexBytes)
                .attribute(VertexAttribute::TANGENTS, 0,
                           VertexBuffer::AttributeType::SHORT4,
                           3 * sizeof(float), kVertexBytes)
                .normalized(VertexAttribute::TANGENTS)
                .build(_engine);
    _grid->setBufferAt(
        _engine, 0,
        VertexBuffer::BufferDescriptor(
            bytes, size_t(vertices) * kVertexBytes,
            [](void *buffer, size_t, void *) {
              delete[] static_cast<uint8_t *>(buffer);
            }));

    _wholeCount = size_t(cells) * cells * 6;
    _holedCount = (size_t(cells) * cells - size_t(half) * half) * 6;
    const size_t total = _wholeCount + 4 * _holedCount;
    auto *indices = new uint32_t[total];
    uint32_t *at = indices;
    // Two triangles a cell, anticlockwise seen from above, split along the
    // diagonal towards +x and +z — the one Terrain.heightAt interpolates
    // across, so a height asked of the Dart side is the height drawn.
    auto cell = [&](uint32_t i, uint32_t j) {
      const uint32_t a = i + j * row;
      const uint32_t b = a + 1;
      const uint32_t c = a + row;
      const uint32_t d = c + 1;
      *at++ = a;
      *at++ = d;
      *at++ = b;
      *at++ = a;
      *at++ = c;
      *at++ = d;
    };
    for (uint32_t j = 0; j < cells; j++) {
      for (uint32_t i = 0; i < cells; i++) cell(i, j);
    }
    for (uint32_t hole = 0; hole < 4; hole++) {
      const uint32_t fromX = half / 2 + (hole & 1);
      const uint32_t fromZ = half / 2 + (hole >> 1);
      for (uint32_t j = 0; j < cells; j++) {
        for (uint32_t i = 0; i < cells; i++) {
          if (i >= fromX && i < fromX + half && j >= fromZ &&
              j < fromZ + half) {
            continue;
          }
          cell(i, j);
        }
      }
    }
    _indices = IndexBuffer::Builder()
                   .indexCount(uint32_t(total))
                   .bufferType(IndexBuffer::IndexType::UINT)
                   .build(_engine);
    _indices->setBuffer(
        _engine, IndexBuffer::BufferDescriptor(
                     indices, total * sizeof(uint32_t),
                     [](void *buffer, size_t, void *) {
                       delete[] static_cast<uint32_t *>(buffer);
                     }));
    _box = boxFor(0.0f, 0.0f);
  }

  void buildLevels(int32_t levels, int32_t flags) {
    destroyLevels();
    _levelCount = levels;
    _flags = flags;
    for (int32_t k = 0; k < levels; k++) {
      Level level;
      level.entity = utils::EntityManager::get().create();
      _engine.getTransformManager().create(level.entity);
      RenderableManager::Builder(1)
          .boundingBox(_box)
          .material(0, _instance)
          .geometry(0, RenderableManager::PrimitiveType::TRIANGLES, _grid,
                    _indices, 0, _wholeCount)
          .castShadows((flags & kTerrainCastsShadows) != 0)
          .receiveShadows((flags & kTerrainReceivesShadows) != 0)
          .build(_engine, level.entity);
      if (_shown) _scene.addEntity(level.entity);
      _levels.push_back(level);
    }
  }

  void destroyLevels() {
    RenderableManager &renderables = _engine.getRenderableManager();
    for (const Level &level : _levels) {
      if (_shown) _scene.remove(level.entity);
      renderables.destroy(level.entity);
      _engine.getTransformManager().destroy(level.entity);
      utils::EntityManager::get().destroy(level.entity);
    }
    _levels.clear();
  }

  void destroyGrid() {
    if (_indices != nullptr) _engine.destroy(_indices);
    if (_grid != nullptr) _engine.destroy(_grid);
    _indices = nullptr;
    _grid = nullptr;
  }

  void show(bool shown) {
    if (shown == _shown) return;
    for (const Level &level : _levels) {
      if (shown) {
        _scene.addEntity(level.entity);
      } else {
        _scene.remove(level.entity);
      }
    }
    _shown = shown;
  }

  Engine &_engine;
  Scene &_scene;
  MaterialInstance *_instance = nullptr;
  int32_t _key = 0;

  VertexBuffer *_grid = nullptr;
  IndexBuffer *_indices = nullptr;
  size_t _wholeCount = 0;
  size_t _holedCount = 0;
  std::vector<Level> _levels;
  Box _box{{0, 0, 0}, {1, 1, 1}};
  int32_t _meshSize = 0;
  int32_t _levelCount = -1;
  int32_t _flags = -1;
  float _spacing = 1.0f;
  bool _shown = false;

  Texture *_heights = nullptr;
  Texture *_cover = nullptr;
  Texture *_colours = nullptr;
  Texture *_regionMap = nullptr;
  Texture *_albedo = nullptr;
  Texture *_normals = nullptr;
  int32_t _regionSize = 0;
  uint32_t _capacity = 0;
  /// Layers handed out so far; `_free` are those given back.
  int32_t _used = 0;
  std::vector<int32_t> _free;
  std::map<std::pair<int32_t, int32_t>, Region> _regions;
};

TerrainScene::TerrainScene(Engine &engine, Scene &scene)
    : _engine(engine), _scene(scene) {}

TerrainScene::~TerrainScene() { clear(); }

void TerrainScene::clear() {
  _fields.clear();
  if (_material != nullptr) {
    _engine.destroy(_material);
    _material = nullptr;
  }
}

void TerrainScene::apply(const std::vector<TerrainRequest> &requests,
                         Notes &notes) {
  if (_material == nullptr) {
    if (requests.empty()) return;
    _material = Material::Builder()
                    .package(kterrainMaterial, kterrainMaterial_len)
                    .build(_engine);
  }

  const uint64_t generation = ++_generation;
  for (const TerrainRequest &request : requests) {
    Kept &kept = _fields[request.key];
    kept.seen = generation;
    if (!kept.field) {
      kept.field = std::make_unique<TerrainField>(_engine, _scene, *_material);
    }
    kept.field->apply(request, notes);
  }

  for (auto it = _fields.begin(); it != _fields.end();) {
    if (it->second.seen != generation) {
      it = _fields.erase(it);
    } else {
      ++it;
    }
  }
}

void TerrainScene::update(const double3 &eye) {
  for (auto &entry : _fields) entry.second.field->update(eye);
}

}  // namespace orblit
