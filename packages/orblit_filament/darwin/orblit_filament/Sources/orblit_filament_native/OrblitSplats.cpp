#include "OrblitSplats.h"

#include "OrblitResources.h"

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <limits>
#include <sstream>

// A browser build without -pthread has std::thread and cannot start one — its
// constructor throws — so that build sorts on a Web Worker instead.
#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
#define ORBLIT_SPLAT_THREADS 0
#else
#define ORBLIT_SPLAT_THREADS 1
#include <condition_variable>
#include <mutex>
#include <thread>
#endif

namespace orblit {

void splatCovariance(const float scale[3], const float rotation[4],
                     float out[6]) {
  // Normalised here rather than trusted: a quaternion quantised to bytes is
  // never quite unit length, and a rotation that is not a rotation scales.
  float w = rotation[0], x = rotation[1], y = rotation[2], z = rotation[3];
  const float length = std::sqrt(w * w + x * x + y * y + z * z);
  if (length > 0) {
    w /= length;
    x /= length;
    y /= length;
    z /= length;
  } else {
    w = 1;
    x = y = z = 0;
  }

  // The rotation matrix of a unit quaternion, rows.
  const float r[3][3] = {
      {1 - 2 * (y * y + z * z), 2 * (x * y - w * z), 2 * (x * z + w * y)},
      {2 * (x * y + w * z), 1 - 2 * (x * x + z * z), 2 * (y * z - w * x)},
      {2 * (x * z - w * y), 2 * (y * z + w * x), 1 - 2 * (x * x + y * y)},
  };
  const float s2[3] = {scale[0] * scale[0], scale[1] * scale[1],
                       scale[2] * scale[2]};

  // Σ = R diag(s²) Rᵀ, so Σij = Σk Rik Rjk sk². The same matrix the
  // reference builds as (S R)ᵀ (S R) with its column-major R.
  auto sigma = [&](int i, int j) {
    return r[i][0] * r[j][0] * s2[0] + r[i][1] * r[j][1] * s2[1] +
           r[i][2] * r[j][2] * s2[2];
  };
  out[0] = sigma(0, 0);
  out[1] = sigma(0, 1);
  out[2] = sigma(0, 2);
  out[3] = sigma(1, 1);
  out[4] = sigma(1, 2);
  out[5] = sigma(2, 2);
}

namespace {

uint8_t toByte(float value) {
  return uint8_t(std::clamp(std::lround(value * 255.0f), 0l, 255l));
}

/// Makes room, and resets the box so the first splat sets it.
void begin(SplatCloud &cloud, uint32_t count) {
  cloud.count = count;
  cloud.positions.assign(size_t(count) * 3, 0.0f);
  cloud.covariances.assign(size_t(count) * 6, 0.0f);
  cloud.colours.assign(count, 0u);
  for (int a = 0; a < 3; a++) {
    cloud.minimum[a] = std::numeric_limits<float>::max();
    cloud.maximum[a] = std::numeric_limits<float>::lowest();
  }
  cloud.harmonicDegree = 0;
  cloud.harmonics.clear();
  for (int band = 0; band < 3; band++) cloud.harmonicScale[band] = 0;
  cloud.droppedHigherBands = false;
}

/// One splat into the cloud, and the box grown round it to three sigma.
void put(SplatCloud &cloud, uint32_t i, const float position[3],
         const float scale[3], const float rotation[4], uint32_t colour) {
  std::memcpy(&cloud.positions[size_t(i) * 3], position, sizeof(float) * 3);
  splatCovariance(scale, rotation, &cloud.covariances[size_t(i) * 6]);
  cloud.colours[i] = colour;

  const float reach =
      3.0f * std::max({std::abs(scale[0]), std::abs(scale[1]), std::abs(scale[2])});
  for (int a = 0; a < 3; a++) {
    cloud.minimum[a] = std::min(cloud.minimum[a], position[a] - reach);
    cloud.maximum[a] = std::max(cloud.maximum[a], position[a] + reach);
  }
}

void finish(SplatCloud &cloud) {
  if (cloud.count > 0) return;
  for (int a = 0; a < 3; a++) cloud.minimum[a] = cloud.maximum[a] = 0;
}

}  // namespace

bool readSplatRecords(const uint8_t *data, size_t length, SplatCloud &into,
                      std::string &error) {
  if (length % kSplatRecordBytes != 0) {
    error = "a .splat file is whole 32-byte records, and this is " +
            std::to_string(length) + " bytes";
    return false;
  }
  const uint32_t count = uint32_t(length / kSplatRecordBytes);
  begin(into, count);

  for (uint32_t i = 0; i < count; i++) {
    const uint8_t *record = data + size_t(i) * kSplatRecordBytes;
    float position[3], scale[3];
    std::memcpy(position, record, 12);
    std::memcpy(scale, record + 12, 12);
    uint32_t colour;
    std::memcpy(&colour, record + 24, 4);
    const float rotation[4] = {
        (float(record[28]) - 128.0f) / 128.0f,
        (float(record[29]) - 128.0f) / 128.0f,
        (float(record[30]) - 128.0f) / 128.0f,
        (float(record[31]) - 128.0f) / 128.0f,
    };
    put(into, i, position, scale, rotation, colour);
  }
  finish(into);
  return true;
}

namespace {

/// What one PLY property is and where it sits in a vertex.
struct Property {
  std::string name;
  size_t offset = 0;
  size_t size = 0;
  char kind = 'f';  // f float, d double, u unsigned integer, i signed
};

size_t sizeOf(const std::string &type, char &kind) {
  if (type == "float" || type == "float32") { kind = 'f'; return 4; }
  if (type == "double" || type == "float64") { kind = 'd'; return 8; }
  if (type == "uchar" || type == "uint8") { kind = 'u'; return 1; }
  if (type == "char" || type == "int8") { kind = 'i'; return 1; }
  if (type == "ushort" || type == "uint16") { kind = 'u'; return 2; }
  if (type == "short" || type == "int16") { kind = 'i'; return 2; }
  if (type == "uint" || type == "uint32") { kind = 'u'; return 4; }
  if (type == "int" || type == "int32") { kind = 'i'; return 4; }
  return 0;
}

float readAs(const uint8_t *at, const Property &p) {
  switch (p.kind) {
    case 'f': { float v; std::memcpy(&v, at, 4); return v; }
    case 'd': { double v; std::memcpy(&v, at, 8); return float(v); }
    case 'u': {
      uint32_t v = 0;
      std::memcpy(&v, at, p.size);
      return float(v);
    }
    default: {
      if (p.size == 1) return float(int8_t(at[0]));
      if (p.size == 2) { int16_t v; std::memcpy(&v, at, 2); return float(v); }
      int32_t v; std::memcpy(&v, at, 4); return float(v);
    }
  }
}

/// Which band a coefficient belongs to, counting the first band as nought:
/// three coefficients in the first, five in the second, seven in the third.
uint32_t bandOf(uint32_t coefficient) {
  if (coefficient < 3) return 0;
  return coefficient < 8 ? 1 : 2;
}

/// One coefficient as the byte the shader decodes: 128 is nought, and the
/// band's scale either way is 1 and 255. Anything past the scale clamps.
uint8_t toHarmonicByte(float value, float scale) {
  if (!(scale > 0) || !std::isfinite(value)) return 128;
  const float unit = std::clamp(value / scale, -1.0f, 1.0f);
  return uint8_t(std::lround(unit * kSplatHarmonicSteps) + 128);
}

/// Quantises the bands above the flat colour into the cloud, from whatever
/// file held them.
///
/// `read(splat, coefficient, channel)` answers one coefficient as the number
/// it stands for, in whatever order and layout that file keeps them in; what
/// lands in the cloud is always one splat's coefficients together, with red,
/// green and blue within each, which is how the shader reads them.
///
/// Two passes, because what a byte is worth cannot be known until every
/// coefficient has been seen. The first only counts magnitudes; the second
/// writes the bytes.
template <typename Read>
void quantiseHarmonics(uint64_t count, uint32_t degree, SplatCloud &into,
                       Read read) {
  const uint32_t keep = kSplatHarmonicCoefficients[degree];
  if (keep == 0 || count == 0) return;

  // What a byte of each band is worth.
  //
  // A trained capture's coefficients are nearly all small and a few are not:
  // a handful of splats carry a coefficient many times larger than anything
  // else in the file, and a scale stretched to reach those would spend most
  // of a byte's 255 steps on values nothing has. So each band is scaled by
  // the magnitude 99.9% of its own coefficients are below and the rest clamp:
  // a few splats very slightly too bright, against a step several times finer
  // on all of them.
  //
  // The magnitudes go into fixed bins of a 128th up to eight rather than into
  // bins sized from the largest one seen, so this is one pass and not two,
  // and so one absurd coefficient cannot coarsen the histogram itself.
  constexpr int kBins = 1024;
  constexpr float kPerUnit = 128.0f;
  std::vector<uint64_t> histogram(size_t(kBins) * 3, 0);
  for (uint64_t i = 0; i < count; i++) {
    for (uint32_t c = 0; c < 3; c++) {
      for (uint32_t k = 0; k < keep; k++) {
        const float value = read(i, k, c);
        if (!std::isfinite(value)) continue;
        int bin = int(std::abs(value) * kPerUnit);
        if (bin >= kBins) bin = kBins - 1;
        histogram[size_t(bandOf(k)) * kBins + bin]++;
      }
    }
  }

  for (uint32_t band = 0; band < 3; band++) {
    uint64_t total = 0;
    for (int bin = 0; bin < kBins; bin++) total += histogram[size_t(band) * kBins + bin];
    if (total == 0) continue;
    const uint64_t want = uint64_t(double(total) * 0.999);
    uint64_t seen = 0;
    int at = 0;
    for (; at < kBins - 1; at++) {
      seen += histogram[size_t(band) * kBins + at];
      if (seen >= want) break;
    }
    // The top of that bin, and never nought: a band whose coefficients are
    // all exactly zero would otherwise be scaled by zero.
    into.harmonicScale[band] = std::max(float(at + 1) / kPerUnit, 1.0f / kPerUnit);
  }

  into.harmonicDegree = degree;
  into.harmonics.assign(size_t(count) * splatHarmonicBytes(degree), 128);
  for (uint64_t i = 0; i < count; i++) {
    uint8_t *out = &into.harmonics[size_t(i) * splatHarmonicBytes(degree)];
    for (uint32_t k = 0; k < keep; k++) {
      const float scale = into.harmonicScale[bandOf(k)];
      for (uint32_t c = 0; c < 3; c++) {
        out[size_t(k) * 3 + c] = toHarmonicByte(read(i, k, c), scale);
      }
    }
  }
}

}  // namespace

bool readSplatPly(const uint8_t *data, size_t length, uint32_t maxDegree,
                  SplatCloud &into, std::string &error) {
  // The header is text and ends at the first "end_header" line. Capped, so a
  // file that is not a PLY at all is refused rather than scanned to its end.
  const char *text = reinterpret_cast<const char *>(data);
  const size_t searchable = std::min<size_t>(length, 64 * 1024);
  const std::string head(text, searchable);
  const size_t end = head.find("end_header");
  if (head.rfind("ply", 0) != 0 || end == std::string::npos) {
    error = "not a PLY file: no ply/end_header header";
    return false;
  }
  size_t body = head.find('\n', end);
  if (body == std::string::npos) {
    error = "the PLY header has no line after end_header";
    return false;
  }
  body += 1;

  std::istringstream lines(head.substr(0, end));
  std::string line;
  bool binaryLittle = false;
  bool inVertex = false;
  bool vertexFirst = true;
  bool sawElement = false;
  uint64_t vertices = 0;
  size_t stride = 0;
  std::vector<Property> properties;

  while (std::getline(lines, line)) {
    if (!line.empty() && line.back() == '\r') line.pop_back();
    std::istringstream words(line);
    std::string word;
    words >> word;
    if (word == "format") {
      std::string format;
      words >> format;
      binaryLittle = format == "binary_little_endian";
    } else if (word == "element") {
      std::string name;
      words >> name;
      // Only the vertex element is read, and only when it comes first —
      // which is how every trainer writes it. Anything before it would need
      // its size worked out to be skipped.
      if (name == "vertex") {
        if (sawElement) vertexFirst = false;
        words >> vertices;
        inVertex = true;
      } else {
        inVertex = false;
      }
      sawElement = true;
    } else if (word == "property" && inVertex) {
      std::string type, name;
      words >> type;
      if (type == "list") {
        error = "a list property in the vertex element, which splats do not have";
        return false;
      }
      words >> name;
      Property p;
      p.name = name;
      p.size = sizeOf(type, p.kind);
      if (p.size == 0) {
        error = "a property of unknown type " + type;
        return false;
      }
      p.offset = stride;
      stride += p.size;
      properties.push_back(p);
    }
  }

  if (!binaryLittle) {
    error = "only binary_little_endian PLY is read";
    return false;
  }
  if (!vertexFirst) {
    error = "the vertex element has to come first";
    return false;
  }
  if (vertices == 0 || stride == 0) {
    error = "the PLY has no vertices";
    return false;
  }
  if (vertices > 50'000'000ull || body + vertices * stride > length) {
    error = "the PLY says " + std::to_string(vertices) +
            " vertices, more than the file holds";
    return false;
  }

  auto find = [&](const char *name) -> const Property * {
    for (const auto &p : properties) {
      if (p.name == name) return &p;
    }
    return nullptr;
  };

  const char *required[] = {"x", "y", "z", "f_dc_0", "f_dc_1", "f_dc_2",
                            "opacity", "scale_0", "scale_1", "scale_2",
                            "rot_0", "rot_1", "rot_2", "rot_3"};
  const Property *p[14];
  for (int i = 0; i < 14; i++) {
    p[i] = find(required[i]);
    if (p[i] == nullptr) {
      error = std::string("the PLY has no ") + required[i] +
              ", so it is not a Gaussian splat capture";
      return false;
    }
  }

  // The bands above the flat one, if the file has them and the caller asked
  // for them.
  //
  // f_rest_* is channel-major: every coefficient of red, then every one of
  // green, then blue. That is what the reference trainer writes when it
  // flattens its (splat, coefficient, channel) tensor with the last two
  // transposed, so coefficient k of channel c is property c * perChannel + k.
  // How many there are says which degree the capture was trained to: three a
  // channel is one band, eight is two, fifteen is three, and anything else is
  // a layout this does not know how to take apart.
  uint32_t stored = 0;
  while (find(("f_rest_" + std::to_string(stored)).c_str()) != nullptr) stored++;
  uint32_t fileDegree = 0;
  for (uint32_t d = 1; d <= kSplatMaxHarmonicDegree; d++) {
    if (stored == kSplatHarmonicCoefficients[d] * 3) fileDegree = d;
  }
  const uint32_t degree =
      std::min(fileDegree, std::min(maxDegree, kSplatMaxHarmonicDegree));

  std::vector<const Property *> rest;
  if (degree > 0) {
    const uint32_t keep = kSplatHarmonicCoefficients[degree];
    const uint32_t perChannel = stored / 3;
    rest.resize(size_t(keep) * 3);
    for (uint32_t c = 0; c < 3; c++) {
      for (uint32_t k = 0; k < keep; k++) {
        rest[size_t(c) * keep + k] =
            find(("f_rest_" + std::to_string(c * perChannel + k)).c_str());
      }
    }
  }

  begin(into, uint32_t(vertices));
  // Said when the picture is missing something the file had: bands above the
  // degree asked for, or a count of f_rest properties that is none of the
  // three a trainer writes.
  into.droppedHigherBands =
      fileDegree > degree || (stored > 0 && fileDegree == 0);

  for (uint32_t i = 0; i < uint32_t(vertices); i++) {
    const uint8_t *v = data + body + size_t(i) * stride;
    const float position[3] = {readAs(v + p[0]->offset, *p[0]),
                               readAs(v + p[1]->offset, *p[1]),
                               readAs(v + p[2]->offset, *p[2])};
    // The flat part of the colour, from the degree-zero coefficient. What the
    // higher bands add is read below and evaluated in the shader, where the
    // direction to the camera is known.
    const float r = 0.5f + kShC0 * readAs(v + p[3]->offset, *p[3]);
    const float g = 0.5f + kShC0 * readAs(v + p[4]->offset, *p[4]);
    const float b = 0.5f + kShC0 * readAs(v + p[5]->offset, *p[5]);
    // Stored as a logit, so it can be trained without a clamp.
    const float opacity =
        1.0f / (1.0f + std::exp(-readAs(v + p[6]->offset, *p[6])));
    // Stored as logs, for the same reason.
    const float scale[3] = {std::exp(readAs(v + p[7]->offset, *p[7])),
                            std::exp(readAs(v + p[8]->offset, *p[8])),
                            std::exp(readAs(v + p[9]->offset, *p[9]))};
    const float rotation[4] = {readAs(v + p[10]->offset, *p[10]),
                               readAs(v + p[11]->offset, *p[11]),
                               readAs(v + p[12]->offset, *p[12]),
                               readAs(v + p[13]->offset, *p[13])};
    const uint32_t colour = uint32_t(toByte(r)) | (uint32_t(toByte(g)) << 8) |
                            (uint32_t(toByte(b)) << 16) |
                            (uint32_t(toByte(opacity)) << 24);
    put(into, i, position, scale, rotation, colour);
  }
  // f_rest is channel-major, as the header found it: coefficient k of channel
  // c is rest[c * keep + k].
  if (degree > 0) {
    const uint8_t *first = data + body;
    const uint32_t keep = kSplatHarmonicCoefficients[degree];
    quantiseHarmonics(vertices, degree, into,
                      [&](uint64_t i, uint32_t k, uint32_t c) {
                        const Property &p = *rest[size_t(c) * keep + k];
                        return readAs(first + size_t(i) * stride + p.offset, p);
                      });
  }
  finish(into);
  return true;
}

namespace {

// stb_image's own inflater. Filament links stb into libstb on every platform
// this builds for and ships no header for it, exactly as OrblitPlatform.cpp
// finds its image decoder; this is stb_image.h's public signature, unchanged.
extern "C" int stbi_zlib_decode_noheader_buffer(char *obuffer, int olen,
                                                const char *ibuffer, int ilen);

/// The most a compressed file is allowed to unpack to.
///
/// The only thing that says how large a gzip file's contents are is that
/// file, so a capped buffer is what stands between a page and a few lines of
/// zeroes that claim to be sixteen gigabytes. Well past any real capture:
/// a million splats at degree three is about seventy megabytes.
constexpr size_t kMaxUnpacked = size_t(1536) * 1024 * 1024;

/// Where the deflate stream inside a gzip wrapper begins, how long it is, and
/// what the file says it unpacks to.
///
/// RFC 1952: a ten-byte header, then the optional extra field, name, comment
/// and header checksum the flags say are there, then the stream, then a
/// checksum and the length modulo 2^32.
bool gzipStream(const uint8_t *data, size_t length, const uint8_t *&stream,
                size_t &deflated, size_t &expanded, std::string &error) {
  if (length < 18 || data[0] != 0x1f || data[1] != 0x8b) {
    error = "not a gzip file";
    return false;
  }
  if (data[2] != 8) {
    error = "the gzip file is not deflate";
    return false;
  }
  const uint8_t flags = data[3];
  size_t at = 10;
  if (flags & 0x04) {
    if (at + 2 > length) {
      error = "the gzip header stops in its extra field";
      return false;
    }
    at += 2 + (size_t(data[at]) | (size_t(data[at + 1]) << 8));
  }
  // The name, then the comment: each a string ending in a nought.
  for (const uint8_t bit : {uint8_t(0x08), uint8_t(0x10)}) {
    if (!(flags & bit)) continue;
    while (at < length && data[at] != 0) at++;
    at++;
  }
  if (flags & 0x02) at += 2;
  if (at + 8 >= length) {
    error = "the gzip file has no deflate stream in it";
    return false;
  }
  stream = data + at;
  deflated = length - 8 - at;
  expanded = size_t(data[length - 4]) | (size_t(data[length - 3]) << 8) |
             (size_t(data[length - 2]) << 16) |
             (size_t(data[length - 1]) << 24);
  return true;
}

bool inflateGzip(const uint8_t *data, size_t length,
                 std::vector<uint8_t> &into, std::string &error) {
  const uint8_t *stream = nullptr;
  size_t deflated = 0;
  size_t expanded = 0;
  if (length > kMaxUnpacked) {
    error = "the file is larger than this reads";
    return false;
  }
  if (!gzipStream(data, length, stream, deflated, expanded, error)) return false;
  if (expanded == 0 || expanded > kMaxUnpacked) {
    error = "the gzip file says it unpacks to " + std::to_string(expanded) +
            " bytes, which this will not do";
    return false;
  }
  into.assign(expanded, 0);
  const int got = stbi_zlib_decode_noheader_buffer(
      reinterpret_cast<char *>(into.data()), int(expanded),
      reinterpret_cast<const char *>(stream), int(deflated));
  if (got < 0 || size_t(got) != expanded) {
    error = "the gzip file does not unpack to the length it states";
    return false;
  }
  return true;
}

/// "NGSP", as the four bytes at the front of a `.spz` spell it here.
constexpr uint32_t kSpzMagic = 0x5053474e;

/// What a stored `.spz` colour is worth: the format's own scale, which its
/// packer divides a degree-zero coefficient by before quantising it.
constexpr float kSpzColourScale = 0.15f;

/// Which spherical-harmonic coefficients change sign when y and z do.
///
/// Turning a capture from the right-up-back frame a `.spz` holds into the
/// right-down-front one a `.ply` has is a half turn about x, and a band's
/// coefficients have to turn with it: a basis function odd in y or in z
/// alone changes sign, and its coefficient changes sign to match. The ones
/// even in both — yz, xy is odd, and so on — do not. Worked out from the
/// basis in splat.mat, term by term.
constexpr bool kSpzFlipped[15] = {true,  true,  false, true,  false,
                                  false, true,  false, true,  false,
                                  true,  true,  false, true,  false};

/// "OSPL", and the version of the layout after it.
constexpr uint32_t kCookedMagic = 0x4c50534f;
constexpr uint32_t kCookedVersion = 1;

/// Magic, version, count and degree; the three band scales; the box; and a
/// word kept for whatever a later version wants to say.
constexpr size_t kCookedHeader = 4 * 4 + 3 * 4 + 6 * 4 + 4;

uint32_t readWord(const uint8_t *at) {
  uint32_t value;
  std::memcpy(&value, at, 4);
  return value;
}

}  // namespace

bool readSplatSpz(const uint8_t *data, size_t length, uint32_t maxDegree,
                  SplatCloud &into, std::string &error) {
  std::vector<uint8_t> unpacked;
  if (!inflateGzip(data, length, unpacked, error)) return false;
  if (unpacked.size() < 16) {
    error = "the .spz is too short to hold a header";
    return false;
  }

  const uint32_t magic = readWord(unpacked.data());
  const uint32_t version = readWord(unpacked.data() + 4);
  const uint32_t points = readWord(unpacked.data() + 8);
  const uint32_t fileDegree = unpacked[12];
  const uint32_t fractionalBits = unpacked[13];
  // unpacked[14] is a flag saying the capture was trained antialiased, which
  // changes nothing this renderer does with it; unpacked[15] is kept back.
  if (magic != kSpzMagic) {
    error = "not a .spz file: it does not begin NGSP";
    return false;
  }
  if (version >= 4) {
    error = "this .spz is version " + std::to_string(version) +
            ", which is zstd streams and a table of contents rather than one "
            "gzip block; this reads versions 2 and 3";
    return false;
  }
  if (version < 2) {
    error = "this .spz is version " + std::to_string(version) +
            "; this reads versions 2 and 3";
    return false;
  }
  if (points == 0 || points > 50'000'000u) {
    error = "the .spz says it holds " + std::to_string(points) + " splats";
    return false;
  }
  if (fileDegree > kSplatMaxHarmonicDegree) {
    error = "the .spz says its harmonics are degree " +
            std::to_string(fileDegree);
    return false;
  }
  if (fractionalBits == 0 || fractionalBits > 24) {
    error = "the .spz says its positions have " +
            std::to_string(fractionalBits) + " fractional bits";
    return false;
  }

  const uint32_t stored = kSplatHarmonicCoefficients[fileDegree];
  const size_t rotationBytes = version >= 3 ? 4 : 3;
  const size_t perSplat = 9 + 1 + 3 + 3 + rotationBytes + size_t(stored) * 3;
  if (unpacked.size() < 16 + size_t(points) * perSplat) {
    error = "the .spz says it holds " + std::to_string(points) +
            " splats, more than the file has room for";
    return false;
  }

  // Each attribute in a block of its own, in the order the format writes
  // them: positions, alphas, colours, scales, rotations, harmonics.
  const uint8_t *positions = unpacked.data() + 16;
  const uint8_t *alphas = positions + size_t(points) * 9;
  const uint8_t *colours = alphas + size_t(points);
  const uint8_t *scales = colours + size_t(points) * 3;
  const uint8_t *rotations = scales + size_t(points) * 3;
  const uint8_t *harmonics = rotations + size_t(points) * rotationBytes;

  const uint32_t degree =
      std::min(fileDegree, std::min(maxDegree, kSplatMaxHarmonicDegree));
  begin(into, points);
  into.droppedHigherBands = fileDegree > degree;

  const float step = 1.0f / float(uint32_t(1) << fractionalBits);
  for (uint32_t i = 0; i < points; i++) {
    const uint8_t *p = positions + size_t(i) * 9;
    float place[3];
    for (int a = 0; a < 3; a++) {
      // Three bytes, little-endian, signed: the top bit carried up.
      uint32_t bits = uint32_t(p[a * 3]) | (uint32_t(p[a * 3 + 1]) << 8) |
                      (uint32_t(p[a * 3 + 2]) << 16);
      if (bits & 0x00800000u) bits |= 0xff000000u;
      int32_t fixed;
      std::memcpy(&fixed, &bits, sizeof fixed);
      place[a] = float(fixed) * step;
    }
    // Right-up-back to right-down-front: a half turn about x.
    place[1] = -place[1];
    place[2] = -place[2];

    const uint8_t *s = scales + size_t(i) * 3;
    const float size[3] = {std::exp(float(s[0]) / 16.0f - 10.0f),
                           std::exp(float(s[1]) / 16.0f - 10.0f),
                           std::exp(float(s[2]) / 16.0f - 10.0f)};

    // The file keeps a quaternion as (x, y, z, w); this wants (w, x, y, z).
    float turn[4] = {0, 0, 0, 1};  // x, y, z, w, as read
    if (version >= 3) {
      // The smallest three: two bits saying which component was left out,
      // then three of ten bits each — nine of magnitude and one of sign —
      // from the last component to the first. The one left out is whichever
      // was largest, and is worked out from the other three.
      uint32_t packed = readWord(rotations + size_t(i) * 4);
      const uint32_t largest = packed >> 30;
      constexpr float kOverRootTwo = 0.70710678118654752f;
      float sum = 0;
      for (int a = 3; a >= 0; a--) {
        if (uint32_t(a) == largest) continue;
        const uint32_t magnitude = packed & 0x1ffu;
        const bool negative = ((packed >> 9) & 1u) != 0;
        packed >>= 10;
        float value = kOverRootTwo * float(magnitude) / 511.0f;
        if (negative) value = -value;
        turn[a] = value;
        sum += value * value;
      }
      turn[largest] = std::sqrt(std::max(0.0f, 1.0f - sum));
    } else {
      const uint8_t *r = rotations + size_t(i) * 3;
      turn[0] = float(r[0]) / 127.5f - 1.0f;
      turn[1] = float(r[1]) / 127.5f - 1.0f;
      turn[2] = float(r[2]) / 127.5f - 1.0f;
      turn[3] = std::sqrt(std::max(
          0.0f, 1.0f - (turn[0] * turn[0] + turn[1] * turn[1] +
                        turn[2] * turn[2])));
    }
    // (w, x, y, z), with the same half turn about x applied: conjugating a
    // rotation by a half turn about x leaves w and x alone and takes the
    // sign off y and z.
    const float rotation[4] = {turn[3], turn[0], -turn[1], -turn[2]};

    const uint8_t *c = colours + size_t(i) * 3;
    // A stored colour is the degree-zero coefficient, scaled and quantised;
    // the flat colour is what that coefficient makes.
    const float red =
        0.5f + kShC0 * ((float(c[0]) / 255.0f - 0.5f) / kSpzColourScale);
    const float green =
        0.5f + kShC0 * ((float(c[1]) / 255.0f - 0.5f) / kSpzColourScale);
    const float blue =
        0.5f + kShC0 * ((float(c[2]) / 255.0f - 0.5f) / kSpzColourScale);
    const float opacity = float(alphas[i]) / 255.0f;
    const uint32_t colour = uint32_t(toByte(red)) |
                            (uint32_t(toByte(green)) << 8) |
                            (uint32_t(toByte(blue)) << 16) |
                            (uint32_t(toByte(opacity)) << 24);
    put(into, i, place, size, rotation, colour);
  }

  if (degree > 0) {
    quantiseHarmonics(points, degree, into,
                      [&](uint64_t i, uint32_t k, uint32_t c) {
                        const uint8_t byte =
                            harmonics[size_t(i) * stored * 3 + size_t(k) * 3 + c];
                        const float value = (float(byte) - 128.0f) / 128.0f;
                        return kSpzFlipped[k] ? -value : value;
                      });
  }
  finish(into);
  return true;
}

std::vector<uint8_t> writeSplatCooked(const SplatCloud &cloud) {
  const uint32_t degree = cloud.harmonics.empty() ? 0 : cloud.harmonicDegree;
  const size_t harmonicBytes = size_t(cloud.count) * splatHarmonicBytes(degree);
  std::vector<uint8_t> out(kCookedHeader + size_t(cloud.count) * 9 * 4 +
                           size_t(cloud.count) * 4 + harmonicBytes);
  uint8_t *at = out.data();
  auto putWord = [&](uint32_t value) {
    std::memcpy(at, &value, 4);
    at += 4;
  };
  auto putFloats = [&](const float *values, size_t count) {
    if (count > 0) std::memcpy(at, values, count * 4);
    at += count * 4;
  };

  putWord(kCookedMagic);
  putWord(kCookedVersion);
  putWord(cloud.count);
  putWord(degree);
  putFloats(cloud.harmonicScale, 3);
  putFloats(cloud.minimum, 3);
  putFloats(cloud.maximum, 3);
  putWord(0);
  putFloats(cloud.positions.data(), cloud.positions.size());
  putFloats(cloud.covariances.data(), cloud.covariances.size());
  if (!cloud.colours.empty()) {
    std::memcpy(at, cloud.colours.data(), cloud.colours.size() * 4);
    at += cloud.colours.size() * 4;
  }
  if (harmonicBytes > 0) {
    std::memcpy(at, cloud.harmonics.data(), harmonicBytes);
  }
  return out;
}

bool readSplatCooked(const uint8_t *data, size_t length, uint32_t maxDegree,
                     SplatCloud &into, std::string &error) {
  if (length < kCookedHeader) {
    error = "the .osplat is too short to hold a header";
    return false;
  }
  if (readWord(data) != kCookedMagic) {
    error = "not an .osplat file: it does not begin OSPL";
    return false;
  }
  const uint32_t version = readWord(data + 4);
  if (version != kCookedVersion) {
    error = "this .osplat is version " + std::to_string(version) +
            "; this reads version " + std::to_string(kCookedVersion) +
            ", so cook it again";
    return false;
  }
  const uint32_t count = readWord(data + 8);
  const uint32_t fileDegree = readWord(data + 12);
  if (count > 50'000'000u) {
    error = "the .osplat says it holds " + std::to_string(count) + " splats";
    return false;
  }
  if (fileDegree > kSplatMaxHarmonicDegree) {
    error = "the .osplat says its harmonics are degree " +
            std::to_string(fileDegree);
    return false;
  }

  const size_t bytesPerSplat = splatHarmonicBytes(fileDegree);
  const size_t wanted = kCookedHeader + size_t(count) * 9 * 4 +
                        size_t(count) * 4 + size_t(count) * bytesPerSplat;
  if (length < wanted) {
    error = "the .osplat says it holds " + std::to_string(count) +
            " splats, more than the file has room for";
    return false;
  }

  into.count = count;
  into.harmonicDegree = fileDegree;
  into.droppedHigherBands = false;
  std::memcpy(into.harmonicScale, data + 16, 12);
  std::memcpy(into.minimum, data + 28, 12);
  std::memcpy(into.maximum, data + 40, 12);

  const uint8_t *at = data + kCookedHeader;
  into.positions.resize(size_t(count) * 3);
  if (count > 0) std::memcpy(into.positions.data(), at, size_t(count) * 12);
  at += size_t(count) * 12;
  into.covariances.resize(size_t(count) * 6);
  if (count > 0) std::memcpy(into.covariances.data(), at, size_t(count) * 24);
  at += size_t(count) * 24;
  into.colours.resize(count);
  if (count > 0) std::memcpy(into.colours.data(), at, size_t(count) * 4);
  at += size_t(count) * 4;
  into.harmonics.resize(size_t(count) * bytesPerSplat);
  if (!into.harmonics.empty()) {
    std::memcpy(into.harmonics.data(), at, into.harmonics.size());
  }

  // Read at a lower degree than it was cooked at: a splat's coefficients are
  // in band order, so the bands asked for are the bytes at the front of each
  // splat's own, and the rest go.
  const uint32_t degree =
      std::min(fileDegree, std::min(maxDegree, kSplatMaxHarmonicDegree));
  if (degree < fileDegree) {
    const size_t keep = splatHarmonicBytes(degree);
    for (uint32_t i = 0; i < count && keep > 0; i++) {
      std::memmove(&into.harmonics[size_t(i) * keep],
                   &into.harmonics[size_t(i) * bytesPerSplat], keep);
    }
    into.harmonics.resize(size_t(count) * keep);
    for (uint32_t band = degree; band < 3; band++) into.harmonicScale[band] = 0;
    into.harmonicDegree = degree;
    into.droppedHigherBands = true;
  }
  return true;
}

bool loadSplatFile(const std::string &path, uint32_t maxDegree,
                   SplatCloud &into, std::string &error) {
  const SharedBytes bytes = readResource(path);
  if (!bytes) {
    error = "cannot read " + path;
    return false;
  }

  std::string lower = path;
  std::transform(lower.begin(), lower.end(), lower.begin(),
                 [](unsigned char c) { return char(std::tolower(c)); });
  const auto named = [&lower](const char *extension) {
    const size_t length = std::strlen(extension);
    return lower.size() >= length &&
           lower.compare(lower.size() - length, length, extension) == 0;
  };

  if (named(".ply")) {
    return readSplatPly(bytes->data(), bytes->size(), maxDegree, into, error);
  }
  if (named(".spz")) {
    return readSplatSpz(bytes->data(), bytes->size(), maxDegree, into, error);
  }
  if (named(".osplat")) {
    return readSplatCooked(bytes->data(), bytes->size(), maxDegree, into,
                           error);
  }
  return readSplatRecords(bytes->data(), bytes->size(), into, error);
}

void packSplatTexels(const SplatCloud &cloud, std::vector<uint32_t> &texels) {
  const size_t used = size_t(cloud.count) * kSplatTexelsPerSplat;
  const size_t rows = std::max<size_t>(1, (used + kSplatTextureWidth - 1) /
                                              kSplatTextureWidth);
  texels.assign(rows * kSplatTextureWidth * 4, 0u);

  auto bits = [](float value) {
    uint32_t out;
    std::memcpy(&out, &value, 4);
    return out;
  };

  for (uint32_t i = 0; i < cloud.count; i++) {
    const float *p = &cloud.positions[size_t(i) * 3];
    const float *c = &cloud.covariances[size_t(i) * 6];
    uint32_t *t = &texels[size_t(i) * kSplatTexelsPerSplat * 4];
    t[0] = bits(p[0]);
    t[1] = bits(p[1]);
    t[2] = bits(p[2]);
    t[3] = bits(c[0]);
    t[4] = bits(c[1]);
    t[5] = bits(c[2]);
    t[6] = bits(c[3]);
    t[7] = bits(c[4]);
    t[8] = bits(c[5]);
    t[9] = cloud.colours[i];
  }
}

void packSplatHarmonicTexels(const SplatCloud &cloud,
                             std::vector<uint32_t> &texels) {
  const uint32_t perSplat = splatHarmonicTexels(cloud.harmonicDegree);
  const uint32_t bytes = splatHarmonicBytes(cloud.harmonicDegree);
  if (cloud.count == 0 || perSplat == 0 ||
      cloud.harmonics.size() < size_t(cloud.count) * bytes) {
    texels.clear();
    return;
  }

  const size_t used = size_t(cloud.count) * perSplat;
  const size_t rows = std::max<size_t>(1, (used + kSplatTextureWidth - 1) /
                                              kSplatTextureWidth);
  // Filled with 128s, which is a coefficient of nought. Nothing reads the
  // few bytes left over at the end of a splat's texels or the end of the
  // last row, and if anything ever does it reads no colour rather than a
  // coefficient of minus one.
  texels.assign(rows * kSplatTextureWidth * 4, 0x80808080u);

  for (uint32_t i = 0; i < cloud.count; i++) {
    const uint8_t *from = &cloud.harmonics[size_t(i) * bytes];
    uint32_t *to = &texels[size_t(i) * perSplat * 4];
    for (uint32_t b = 0; b < bytes; b++) {
      // Placed by shifting rather than by copying the bytes across, so the
      // byte a coefficient lands in is the one splat.mat shifts back out of
      // it whatever order this processor stores an integer in.
      const uint32_t within = (b % 4) * 8;
      uint32_t &word = to[b / 4];
      word = (word & ~(0xffu << within)) | (uint32_t(from[b]) << within);
    }
  }
}

uint32_t keepMostVisibleSplats(SplatCloud &cloud, uint32_t limit) {
  const uint32_t count = cloud.count;
  if (limit == 0 || limit >= count) return count;

  // What a splat adds to a picture, roughly: how opaque it is, times how much
  // of the screen it can cover. The covariance's eigenvalues are its squared
  // radii, so the sum of their pairwise products is the sum of its three
  // cross-sections' areas squared, near enough — and that sum is the
  // covariance's second invariant, the sum of its principal two-by-two
  // minors, which needs no eigenvalues to work out. Its square root is an
  // area.
  std::vector<float> weight(count);
  for (uint32_t i = 0; i < count; i++) {
    const float *c = &cloud.covariances[size_t(i) * 6];
    const float crossSections = c[0] * c[3] - c[1] * c[1] +
                                c[3] * c[5] - c[4] * c[4] +
                                c[0] * c[5] - c[2] * c[2];
    const float alpha = float(cloud.colours[i] >> 24) / 255.0f;
    const float value = alpha * std::sqrt(std::max(crossSections, 0.0f));
    weight[i] = std::isfinite(value) ? value : 0.0f;
  }

  std::vector<uint32_t> kept(count);
  for (uint32_t i = 0; i < count; i++) kept[i] = i;
  // Ties go to the earlier splat, so the same cloud and limit always keep
  // the same splats.
  std::nth_element(kept.begin(), kept.begin() + limit, kept.end(),
                   [&](uint32_t a, uint32_t b) {
                     return weight[a] > weight[b] ||
                            (weight[a] == weight[b] && a < b);
                   });
  kept.resize(limit);
  // Back in the order they came. A trainer writes neighbours near each other,
  // and that is worth keeping for whatever reads the textures in order.
  std::sort(kept.begin(), kept.end());

  const size_t harmonicBytes =
      cloud.harmonics.empty() ? 0 : splatHarmonicBytes(cloud.harmonicDegree);
  for (int a = 0; a < 3; a++) {
    cloud.minimum[a] = std::numeric_limits<float>::max();
    cloud.maximum[a] = std::numeric_limits<float>::lowest();
  }
  // In place, front to back: each splat kept moves to a slot at or before its
  // own, and every slot it passes over has already been read.
  for (uint32_t to = 0; to < limit; to++) {
    const uint32_t from = kept[to];
    if (from != to) {
      std::memcpy(&cloud.positions[size_t(to) * 3],
                  &cloud.positions[size_t(from) * 3], sizeof(float) * 3);
      std::memcpy(&cloud.covariances[size_t(to) * 6],
                  &cloud.covariances[size_t(from) * 6], sizeof(float) * 6);
      cloud.colours[to] = cloud.colours[from];
      if (harmonicBytes > 0) {
        std::memcpy(&cloud.harmonics[size_t(to) * harmonicBytes],
                    &cloud.harmonics[size_t(from) * harmonicBytes],
                    harmonicBytes);
      }
    }
    // Three standard deviations along an axis is three times the square root
    // of the covariance's diagonal on it, exactly.
    const float *p = &cloud.positions[size_t(to) * 3];
    const float *c = &cloud.covariances[size_t(to) * 6];
    const float reach[3] = {3.0f * std::sqrt(std::max(c[0], 0.0f)),
                            3.0f * std::sqrt(std::max(c[3], 0.0f)),
                            3.0f * std::sqrt(std::max(c[5], 0.0f))};
    for (int a = 0; a < 3; a++) {
      cloud.minimum[a] = std::min(cloud.minimum[a], p[a] - reach[a]);
      cloud.maximum[a] = std::max(cloud.maximum[a], p[a] + reach[a]);
    }
  }

  cloud.count = limit;
  cloud.positions.resize(size_t(limit) * 3);
  cloud.covariances.resize(size_t(limit) * 6);
  cloud.colours.resize(limit);
  if (harmonicBytes > 0) cloud.harmonics.resize(size_t(limit) * harmonicBytes);
  return count;
}

void sortSplats(const float *positions, uint32_t count,
                const SplatSortRequest &request, std::vector<uint32_t> &order,
                std::vector<uint32_t> &scratch) {
  order.clear();
  if (count == 0) return;

  // Keys and indices side by side, ping-ponged between two halves of one
  // buffer: [keys | indices] in scratch, the same in a second block.
  scratch.resize(size_t(count) * 4);
  uint32_t *keys = scratch.data();
  uint32_t *ids = keys + count;
  uint32_t *keysOut = ids + count;
  uint32_t *idsOut = keysOut + count;

  const float dx = request.direction[0];
  const float dy = request.direction[1];
  const float dz = request.direction[2];
  const float *v = request.viewFromModel;
  const float *c = request.clipFromModel;
  const bool cull = request.cull;
  const bool coarse = request.coarse;

  // Every byte histogram a full key needs, in the one pass that makes the
  // keys. A coarse key cannot be made until the range is known, so its two
  // are counted in a second pass.
  uint32_t counts[4][256] = {};
  uint32_t kept = 0;
  float nearest = std::numeric_limits<float>::max();
  float farthest = std::numeric_limits<float>::lowest();
  for (uint32_t i = 0; i < count; i++) {
    const float *p = positions + size_t(i) * 3;
    if (cull) {
      // splat.mat's own test, from the same camera: in front of it — view
      // space looks down -z — and within the guard of the screen.
      const float ahead =
          -(v[2] * p[0] + v[6] * p[1] + v[10] * p[2] + v[14]);
      if (!(ahead > 0.0f)) continue;
      const float guard =
          kSplatCullGuard * (c[3] * p[0] + c[7] * p[1] + c[11] * p[2] + c[15]);
      const float x = c[0] * p[0] + c[4] * p[1] + c[8] * p[2] + c[12];
      const float y = c[1] * p[0] + c[5] * p[1] + c[9] * p[2] + c[13];
      if (!(std::abs(x) <= guard && std::abs(y) <= guard)) continue;
    }
    const float depth = p[0] * dx + p[1] * dy + p[2] * dz;
    ids[kept] = i;
    if (coarse) {
      // The depth itself for now, as bits, made a key once the range is in.
      std::memcpy(&keys[kept], &depth, sizeof(float));
      nearest = std::min(nearest, depth);
      farthest = std::max(farthest, depth);
    } else {
      // Inverted, so that ascending is farthest first.
      const uint32_t key = ~sortableBits(depth);
      keys[kept] = key;
      counts[0][key & 0xff]++;
      counts[1][(key >> 8) & 0xff]++;
      counts[2][(key >> 16) & 0xff]++;
      counts[3][key >> 24]++;
    }
    kept++;
  }

  int passes = 4;
  if (coarse) {
    passes = 2;
    const float span = farthest - nearest;
    const float scale = span > 0.0f ? 65535.0f / span : 0.0f;
    for (uint32_t k = 0; k < kept; k++) {
      float depth;
      std::memcpy(&depth, &keys[k], sizeof(float));
      // A depth that is not a number goes nearest, where it is drawn last and
      // hides least.
      const float q = (depth - nearest) * scale;
      const uint32_t level =
          q >= 0.0f ? (q < 65535.0f ? uint32_t(q) : 65535u) : 0u;
      const uint32_t key = 65535u - level;
      keys[k] = key;
      counts[0][key & 0xff]++;
      counts[1][key >> 8]++;
    }
  }

  for (int pass = 0; pass < passes; pass++) {
    const int shift = pass * 8;
    const uint32_t *histogram = counts[pass];
    // Every key agrees on this byte, so the pass would move nothing.
    if (kept == 0 || histogram[(keys[0] >> shift) & 0xff] == kept) continue;

    uint32_t offsets[256];
    uint32_t total = 0;
    for (int b = 0; b < 256; b++) {
      offsets[b] = total;
      total += histogram[b];
    }
    for (uint32_t i = 0; i < kept; i++) {
      const uint32_t at = offsets[(keys[i] >> shift) & 0xff]++;
      keysOut[at] = keys[i];
      idsOut[at] = ids[i];
    }
    std::swap(keys, keysOut);
    std::swap(ids, idsOut);
  }

  order.assign(ids, ids + kept);
}

namespace {

double millisecondsSince(std::chrono::steady_clock::time_point from) {
  return std::chrono::duration<double, std::milli>(
             std::chrono::steady_clock::now() - from)
      .count();
}

/// Sorts on the thread that asks, and has the answer ready before it
/// returns.
class InlineSplatSorter final : public SplatSorter {
 public:
  InlineSplatSorter(std::shared_ptr<const std::vector<float>> positions,
                    uint32_t count)
      : _positions(std::move(positions)), _count(count) {}

  void request(const SplatSortRequest &request) override {
    const auto from = std::chrono::steady_clock::now();
    sortSplats(_positions->data(), _count, request, _result, _scratch);
    _milliseconds = millisecondsSince(from);
    _ready = true;
  }

  bool busy() override { return false; }

  bool take(std::vector<uint32_t> &order, double &milliseconds) override {
    if (!_ready) return false;
    order.swap(_result);
    milliseconds = _milliseconds;
    _ready = false;
    return true;
  }

 private:
  std::shared_ptr<const std::vector<float>> _positions;
  uint32_t _count;
  bool _ready = false;
  double _milliseconds = 0;
  std::vector<uint32_t> _result;
  std::vector<uint32_t> _scratch;
};

#if ORBLIT_SPLAT_THREADS
/// Sorts on a thread of its own.
///
/// Only the request and the answer cross between the threads, and both are
/// behind one lock; the positions are never written, so the worker reads
/// them without it.
class ThreadSplatSorter final : public SplatSorter {
 public:
  ThreadSplatSorter(std::shared_ptr<const std::vector<float>> positions,
                    uint32_t count)
      : _positions(std::move(positions)), _count(count) {
    _worker = std::thread([this] { run(); });
  }

  ~ThreadSplatSorter() override {
    {
      std::lock_guard<std::mutex> guard(_lock);
      _stopping = true;
    }
    _wake.notify_all();
    if (_worker.joinable()) _worker.join();
  }

  void request(const SplatSortRequest &request) override {
    {
      std::lock_guard<std::mutex> guard(_lock);
      _request = request;
      _pending = true;
    }
    _wake.notify_one();
  }

  bool busy() override {
    std::lock_guard<std::mutex> guard(_lock);
    return _pending || _working;
  }

  bool take(std::vector<uint32_t> &order, double &milliseconds) override {
    std::lock_guard<std::mutex> guard(_lock);
    if (!_ready) return false;
    order.swap(_result);
    milliseconds = _milliseconds;
    _ready = false;
    return true;
  }

 private:
  void run() {
    std::vector<uint32_t> order;
    std::vector<uint32_t> scratch;
    for (;;) {
      SplatSortRequest request;
      {
        std::unique_lock<std::mutex> guard(_lock);
        _wake.wait(guard, [this] { return _stopping || _pending; });
        if (_stopping) return;
        request = _request;
        _pending = false;
        _working = true;
      }

      const auto from = std::chrono::steady_clock::now();
      sortSplats(_positions->data(), _count, request, order, scratch);
      const double took = millisecondsSince(from);

      {
        std::lock_guard<std::mutex> guard(_lock);
        _result.swap(order);
        _milliseconds = took;
        _ready = true;
        _working = false;
      }
    }
  }

  std::shared_ptr<const std::vector<float>> _positions;
  uint32_t _count;

  std::mutex _lock;
  std::condition_variable _wake;
  bool _stopping = false;
  bool _pending = false;
  bool _working = false;
  bool _ready = false;
  SplatSortRequest _request;
  std::vector<uint32_t> _result;
  double _milliseconds = 0;

  std::thread _worker;
};
#endif

}  // namespace

std::unique_ptr<SplatSorter> makeSplatSorter(
    std::shared_ptr<const std::vector<float>> positions, uint32_t count) {
  if (count <= kSplatInlineSortLimit) {
    return std::make_unique<InlineSplatSorter>(std::move(positions), count);
  }
#if ORBLIT_SPLAT_THREADS
  return std::make_unique<ThreadSplatSorter>(std::move(positions), count);
#else
  if (auto worker = makeWorkerSplatSorter(positions, count)) return worker;
  std::fprintf(stderr,
               "[orblit] splats: no sorting worker; sorting %u splats on the "
               "page's own thread\n",
               count);
  return std::make_unique<InlineSplatSorter>(std::move(positions), count);
#endif
}

}  // namespace orblit
