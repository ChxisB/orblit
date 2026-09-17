// The KTX 2 reader's own checks, in C++ against OrblitKtx2.h.
//
// No GPU, no Filament: the reader is a pure function of bytes, so what is
// checked is what it says about them. Files written here in memory
// (ktx2_fixtures.h) read back as they were written, level for level, with
// zstd and without; every refusal says why; and thousands of damaged files —
// cut short, scribbled on, their headers edited — come back as a refusal or a
// clean read and never as a crash. Build it with -fsanitize=address,undefined
// to have the last of those mean something:
//
//   clang++ -std=c++17 -g -fsanitize=address,undefined -I <Sources> \
//     orblit_ktx2_check.cpp <Sources>/OrblitKtx2.cpp <sdk>/lib/arm64/libzstd.a
//
// Real Basis files are read from ORBLIT_KTX2_SAMPLES when it is set: any
// .ktx2 files there are parsed, and one with mipmaps has its largest levels
// left out and is read again.
//
// Built and run by build.sh beside the importer's checks.

#include "OrblitKtx2.h"

#include <dirent.h>

#include <cstdio>
#include <cstdlib>
#include <fstream>
#include <iterator>
#include <random>
#include <string>
#include <vector>

#include "ktx2_fixtures.h"

namespace {

using namespace orblit;
using fixtures::Rgba;

int failures = 0;

void expect(bool holds, const std::string &what) {
  if (!holds) {
    fprintf(stderr, "FAIL: %s\n", what.c_str());
    failures++;
  }
}

std::vector<uint8_t> level(const std::vector<uint8_t> &file,
                           const ktx2::Header &header, uint32_t index,
                           std::string *why = nullptr) {
  std::vector<uint8_t> out(size_t(ktx2::levelBytes(header, index)));
  const std::string said = ktx2::readLevel(file.data(), file.size(), header,
                                           index, out.data(), out.size());
  if (why != nullptr) *why = said;
  if (!said.empty()) out.clear();
  return out;
}

Rgba rainbow(uint32_t level) {
  return {uint8_t(31 + level * 20), uint8_t(201 - level * 16),
          uint8_t(101 + level * 8), 255};
}

void filesReadBackAsWritten() {
  struct Case {
    const char *what;
    fixtures::Kind kind;
    uint32_t width, height, levels;
    bool zstd;
  };
  const Case cases[] = {
      {"ASTC 4x4 sRGB, zstd", fixtures::astc4x4(true), 64, 32, 7, true},
      {"ASTC 4x4 linear", fixtures::astc4x4(false), 64, 64, 7, false},
      {"BC7 sRGB, zstd", fixtures::bc7(true), 256, 256, 9, true},
      {"BC7 linear", fixtures::bc7(false), 30, 18, 5, false},
      {"BC1, zstd", fixtures::bc1(true), 16, 16, 5, true},
      {"ETC2 RGB8", fixtures::etc2(false), 8, 8, 4, false},
      {"ETC2 RGBA8, zstd", fixtures::etc2Rgba(true), 12, 20, 5, true},
      {"RGBA8 sRGB, zstd", fixtures::rgba8(true), 33, 17, 1, true},
  };
  for (const Case &c : cases) {
    const fixtures::File file =
        fixtures::solid(c.kind, c.width, c.height, c.levels, c.zstd, rainbow);
    const std::vector<uint8_t> bytes = fixtures::write(file);
    ktx2::Header header;
    const std::string why = ktx2::read(bytes.data(), bytes.size(), header);
    expect(why.empty(), std::string(c.what) + " reads: " + why);
    if (!why.empty()) continue;
    expect(!header.basis && header.format != nullptr &&
               header.format->vkFormat == c.kind.vkFormat,
           std::string(c.what) + " says its own format");
    expect(header.width == c.width && header.height == c.height &&
               header.levels == c.levels && header.faces == 1,
           std::string(c.what) + " says its own size and levels");
    expect(header.scheme == (c.zstd ? ktx2::Supercompression::zstd
                                    : ktx2::Supercompression::none),
           std::string(c.what) + " says how it is squeezed");
    expect((header.transfer == ktx2::Transfer::srgb) == header.format->srgb,
           std::string(c.what) + " says its transfer function");
    for (uint32_t i = 0; i < c.levels; i++) {
      std::string said;
      const std::vector<uint8_t> got = level(bytes, header, i, &said);
      expect(got == file.levels[i],
             std::string(c.what) + " level " + std::to_string(i) +
                 " reads back as written " + said);
    }
  }
  printf("orblit_ktx2_check: %zu formats read back level for level\n",
         sizeof cases / sizeof cases[0]);
}

void cubemapsReadEveryFace() {
  // Six faces a level, each its own colour, end to end as KTX 2 lays them.
  fixtures::File file;
  file.kind = fixtures::bc7(false);
  file.width = file.height = 16;
  file.zstd = true;
  file.faces = 6;
  for (uint32_t level = 0; level < 5; level++) {
    const uint32_t side = 16 >> level;
    std::vector<uint8_t> faces;
    for (uint32_t face = 0; face < 6; face++) {
      const std::vector<uint8_t> one =
          fixtures::solidLevel(file.kind, side, side, rainbow(face));
      faces.insert(faces.end(), one.begin(), one.end());
    }
    file.levels.push_back(faces);
  }
  const std::vector<uint8_t> bytes = fixtures::write(file);
  ktx2::Header header;
  const std::string why = ktx2::read(bytes.data(), bytes.size(), header);
  expect(why.empty() && header.faces == 6, "a cubemap reads: " + why);
  bool same = true;
  for (uint32_t i = 0; i < header.levels; i++) {
    same = same && level(bytes, header, i) == file.levels[i];
  }
  expect(same, "every face of every level reads back as written");
}

void theDescriptorIsRead() {
  fixtures::File file =
      fixtures::solid(fixtures::bc7(false), 8, 8, 1, false, rainbow);
  file.transfer = 2;
  file.premultiplied = true;
  const std::vector<uint8_t> bytes = fixtures::write(file);
  ktx2::Header header;
  expect(ktx2::read(bytes.data(), bytes.size(), header).empty(),
         "a premultiplied file reads");
  expect(header.premultiplied, "premultiplied alpha is read");
  expect(header.transfer == ktx2::Transfer::srgb,
         "the descriptor's transfer function wins over the vkFormat's");
  expect(header.colourModel == 134, "the colour model is read");
}

void theLargestLevelsAreLeftOut() {
  const auto header = [](uint32_t w, uint32_t h, uint32_t levels) {
    ktx2::Header made;
    made.width = w;
    made.height = h;
    made.levels = levels;
    return made;
  };
  expect(ktx2::levelsToSkip(header(4096, 4096, 13), 1024) == 2,
         "4096 with mipmaps fits 1024 by leaving out two levels");
  expect(ktx2::levelsToSkip(header(4096, 4096, 13), 0) == 0,
         "no limit leaves nothing out");
  expect(ktx2::levelsToSkip(header(4096, 1024, 13), 1024) == 2,
         "the longer side decides");
  expect(ktx2::levelsToSkip(header(2048, 2048, 1), 1024) == 0,
         "a file without mipmaps has nothing smaller to use");
  expect(ktx2::levelsToSkip(header(4096, 4096, 2), 256) == 1,
         "never leaves out every level");
  expect(ktx2::levelsToSkip(header(1000, 1000, 10), 512) == 1,
         "an odd size halves down");

  for (bool zstd : {false, true}) {
    const fixtures::File file =
        fixtures::solid(fixtures::bc7(true), 128, 64, 8, zstd, rainbow);
    const std::vector<uint8_t> bytes = fixtures::write(file);
    ktx2::Header full;
    ktx2::read(bytes.data(), bytes.size(), full);
    std::vector<uint8_t> smaller;
    const std::string why =
        ktx2::withoutLargestLevels(bytes.data(), bytes.size(), full, 3, smaller);
    expect(why.empty(), "a file can be rewritten without its largest levels");
    ktx2::Header less;
    const std::string again =
        ktx2::read(smaller.data(), smaller.size(), less);
    expect(again.empty(), "and the rewrite reads: " + again);
    expect(less.width == 16 && less.height == 8 && less.levels == 5,
           "the rewrite starts three levels down");
    bool same = true;
    for (uint32_t i = 0; i < less.levels; i++) {
      same = same && level(smaller, less, i) == file.levels[i + 3];
    }
    expect(same, "and each level it keeps is the one it was");
  }
}

void namesAndTwins() {
  expect(ktx2::namesCookedSet("a/wood.ktx2"), "x.ktx2 is a cooked set");
  expect(ktx2::namesCookedSet("a/wood.KTX2"), "whatever its case");
  expect(!ktx2::namesCookedSet("a/wood.astc.ktx2") &&
             !ktx2::namesCookedSet("a/wood.BC.ktx2") &&
             !ktx2::namesCookedSet("a/wood.etc2.ktx2"),
         "a sibling is taken as it is");
  expect(!ktx2::namesCookedSet("a/wood.png") &&
             !ktx2::namesCookedSet("a/env.ktx"),
         "anything else is not a cooked set");
  expect(ktx2::siblingName("a/wood.ktx2", ktx2::Family::astc) ==
             "a/wood.astc.ktx2",
         "the ASTC sibling is named");
  expect(ktx2::siblingName("a/Wood.KTX2", ktx2::Family::etc2) ==
             "a/Wood.etc2.KTX2",
         "keeping the extension as written");

  const ktx2::Format *bc7 = ktx2::formatOf(145);
  const ktx2::Format *bc7s = ktx2::withTransfer(*bc7, true);
  expect(bc7s != nullptr && bc7s->vkFormat == 146 &&
             ktx2::withTransfer(*bc7s, false) == bc7,
         "BC7's sRGB twin is BC7_SRGB and back");
  expect(ktx2::withTransfer(*ktx2::formatOf(141), true) == nullptr,
         "BC5 has no sRGB twin");
  expect(ktx2::withTransfer(*ktx2::formatOf(141), false) ==
             ktx2::formatOf(141),
         "a format is its own twin for its own transfer");
  size_t count = 0;
  const ktx2::Format *all = ktx2::allFormats(&count);
  size_t astc = 0;
  for (size_t i = 0; i < count; i++) {
    if (all[i].family == ktx2::Family::astc) astc++;
  }
  expect(astc == 28, "every ASTC LDR block size, sRGB and linear");
}

std::vector<uint8_t> edited(std::vector<uint8_t> bytes, size_t at,
                            uint32_t value) {
  fixtures::put32(bytes, at, value);
  return bytes;
}

void refusalsSayWhy() {
  const fixtures::File file =
      fixtures::solid(fixtures::astc4x4(true), 16, 16, 5, true, rainbow);
  const std::vector<uint8_t> good = fixtures::write(file);
  const auto refused = [&](const std::vector<uint8_t> &bytes,
                           const char *containing, const char *what) {
    ktx2::Header header;
    const std::string why = ktx2::read(bytes.data(), bytes.size(), header);
    expect(!why.empty() && why.find(containing) != std::string::npos,
           std::string(what) + " is refused saying '" + containing +
               "', said: " + why);
  };

  refused({}, "too short", "nothing");
  refused(std::vector<uint8_t>(good.begin(), good.begin() + 60), "too short",
          "a cut header");
  std::vector<uint8_t> one = good;
  one[5] = 0x31;
  one[6] = 0x31;
  refused(one, "KTX 1", "a KTX 1 file");
  std::vector<uint8_t> png = good;
  png[1] = 'P';
  refused(png, "identifier", "something else");
  refused(edited(good, 44, 3), "zlib", "zlib levels");
  refused(edited(good, 44, 9), "scheme", "an unknown scheme");
  refused(edited(good, 28, 4), "3D", "a 3D texture");
  refused(edited(good, 32, 6), "array", "an array");
  refused(edited(good, 36, 3), "faces", "three faces");
  refused(edited(edited(good, 36, 6), 24, 8), "square", "an uneven cubemap");
  refused(edited(good, 24, 0), "one-dimensional", "a 1D texture");
  refused(edited(good, 20, 32768), "wider", "a texture too wide");
  refused(edited(good, 40, 9), "levels", "more levels than the size has");
  refused(edited(good, 12, 1000054000), "not one this renderer reads",
          "PVRTC");
  refused(edited(good, 12, 0), "undefined", "no format and not Basis");
  refused(edited(good, 16, 2), "bytes", "the wrong type size");
  refused(edited(good, 80 + 8, 1u << 30), "outside the file",
          "a level longer than the file");
  refused(edited(good, 80 + 16, 999), "unpacks to", "the wrong unpacked size");
  refused(edited(good, 44, 0), "is", "zstd levels said to be raw");

  // A big-enough claim is refused before anything is allocated for it.
  fixtures::File huge =
      fixtures::solid(fixtures::rgba8(false), 1, 1, 1, true, rainbow);
  std::vector<uint8_t> hugeBytes = fixtures::write(huge);
  fixtures::put32(hugeBytes, 12, 109);
  fixtures::put32(hugeBytes, 16, 4);
  fixtures::put32(hugeBytes, 20, 16384);
  fixtures::put32(hugeBytes, 24, 16384);
  fixtures::put64(hugeBytes, 80 + 16, uint64_t(16384) * 16384 * 16);
  refused(hugeBytes, "more than any device", "sixteen gigabytes of floats");

  // A level whose zstd frame is damaged reads as a refusal of that level.
  ktx2::Header header;
  ktx2::read(good.data(), good.size(), header);
  std::vector<uint8_t> scribbled = good;
  const ktx2::Level &largest = header.index[0];
  for (uint64_t i = 4; i < largest.length; i++) {
    scribbled[size_t(largest.offset + i)] ^= 0x5A;
  }
  std::string why;
  level(scribbled, header, 0, &why);
  expect(!why.empty(), "a damaged zstd level is refused: " + why);
  std::vector<uint8_t> notZstd = good;
  notZstd[size_t(largest.offset)] = 0;
  level(notZstd, header, 0, &why);
  expect(why.find("not a zstd frame") != std::string::npos,
         "a level that is not zstd at all says so: " + why);

  std::string wrongRoom;
  std::vector<uint8_t> small(3);
  wrongRoom = ktx2::readLevel(good.data(), good.size(), header, 0,
                              small.data(), small.size());
  expect(!wrongRoom.empty(), "a level is never written into the wrong room");
  printf("orblit_ktx2_check: refusals say why\n");
}

/// Reads whatever ktx2::read accepts all the way down, as the queue would.
bool readWhole(const std::vector<uint8_t> &bytes) {
  ktx2::Header header;
  if (!ktx2::read(bytes.data(), bytes.size(), header).empty()) return false;
  if (header.basis) return true;
  for (uint32_t i = 0; i < header.levels; i++) level(bytes, header, i);
  std::vector<uint8_t> rewritten;
  if (header.levels > 1) {
    ktx2::withoutLargestLevels(bytes.data(), bytes.size(), header, 1,
                               rewritten);
  }
  return true;
}

void damagedFilesNeverCrash() {
  std::vector<std::vector<uint8_t>> seeds;
  for (bool zstd : {false, true}) {
    seeds.push_back(fixtures::write(
        fixtures::solid(fixtures::bc7(true), 64, 64, 7, zstd, rainbow)));
    seeds.push_back(fixtures::write(
        fixtures::solid(fixtures::astc4x4(false), 20, 12, 5, zstd, rainbow)));
    seeds.push_back(fixtures::write(
        fixtures::solid(fixtures::rgba8(true), 9, 7, 4, zstd, rainbow)));
  }

  // Every length a file can be cut to.
  size_t cuts = 0;
  for (const std::vector<uint8_t> &seed : seeds) {
    for (size_t length = 0; length < seed.size(); length++) {
      readWhole(std::vector<uint8_t>(seed.begin(), seed.begin() + length));
      cuts++;
    }
  }

  // And scribbling: bytes anywhere, bytes in the header and index, whole
  // fields replaced with numbers chosen to overflow.
  std::mt19937 random(2026);
  const uint32_t kNasty[] = {0,          1,          2,          3,
                             6,          7,          0xFF,       0x7FFFFFFF,
                             0x80000000, 0xFFFFFFFF, 16384,      16385};
  size_t mutated = 0;
  size_t accepted = 0;
  const int kMutations =
      getenv("ORBLIT_KTX2_MUTATIONS") ? atoi(getenv("ORBLIT_KTX2_MUTATIONS"))
                                      : 3000;
  for (int i = 0; i < kMutations; i++) {
    std::vector<uint8_t> bytes = seeds[random() % seeds.size()];
    const int edits = 1 + int(random() % 6);
    for (int e = 0; e < edits; e++) {
      switch (random() % 4) {
        case 0:
          bytes[random() % bytes.size()] = uint8_t(random());
          break;
        case 1:
          bytes[random() % std::min<size_t>(bytes.size(), 200)] =
              uint8_t(random());
          break;
        case 2: {
          const size_t at = random() % std::min<size_t>(bytes.size() - 8, 180);
          fixtures::put32(bytes, at,
                          kNasty[random() % (sizeof kNasty / sizeof *kNasty)]);
          break;
        }
        case 3:
          bytes.resize(1 + random() % bytes.size());
          if (bytes.size() < 16) bytes.resize(16);
          break;
      }
    }
    if (readWhole(bytes)) accepted++;
    mutated++;
  }
  printf("orblit_ktx2_check: %zu cut files and %zu scribbled ones read "
         "without a crash (%zu of the scribbled still readable)\n",
         cuts, mutated, accepted);
}

void realFilesRead() {
  const char *dir = getenv("ORBLIT_KTX2_SAMPLES");
  if (dir == nullptr) {
    printf("orblit_ktx2_check: ORBLIT_KTX2_SAMPLES is not set, so no real "
           "Basis files were read\n");
    return;
  }
  DIR *listing = opendir(dir);
  if (listing == nullptr) {
    expect(false, std::string("ORBLIT_KTX2_SAMPLES opens: ") + dir);
    return;
  }
  size_t read = 0;
  size_t rewritten = 0;
  while (dirent *entry = readdir(listing)) {
    const std::string name = entry->d_name;
    if (name.size() < 5 || name.substr(name.size() - 5) != ".ktx2") continue;
    std::ifstream in(std::string(dir) + "/" + name, std::ios::binary);
    const std::vector<uint8_t> bytes((std::istreambuf_iterator<char>(in)),
                                     std::istreambuf_iterator<char>());
    ktx2::Header header;
    const std::string why = ktx2::read(bytes.data(), bytes.size(), header);
    expect(why.empty() && header.basis, name + " reads as Basis: " + why);
    read++;
    if (header.levels > 2) {
      std::vector<uint8_t> smaller;
      expect(ktx2::withoutLargestLevels(bytes.data(), bytes.size(), header, 2,
                                        smaller)
                 .empty(),
             name + " can be rewritten without two levels");
      ktx2::Header less;
      expect(ktx2::read(smaller.data(), smaller.size(), less).empty() &&
                 less.basis && less.levels == header.levels - 2 &&
                 less.width == header.width >> 2,
             name + " rewritten reads as the smaller Basis file");
      rewritten++;
    }
  }
  closedir(listing);
  printf("orblit_ktx2_check: %zu real Basis files read, %zu rewritten "
         "without their largest levels\n",
         read, rewritten);
}

}  // namespace

int main() {
  filesReadBackAsWritten();
  cubemapsReadEveryFace();
  theDescriptorIsRead();
  theLargestLevelsAreLeftOut();
  namesAndTwins();
  refusalsSayWhy();
  damagedFilesNeverCrash();
  realFilesRead();
  if (failures > 0) {
    fprintf(stderr, "orblit_ktx2_check: %d failed\n", failures);
    return 1;
  }
  printf("orblit_ktx2_check: passed\n");
  return 0;
}
