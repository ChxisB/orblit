// A check on a cooked surface package, run before Filament sees the bytes.
//
// Filament answers a package it cannot use by aborting the process: a
// truncated file, a material version from another matc, and a package built
// for another backend all end in a failed postcondition rather than a null
// material. The editor recooks on save, so a half-written file is a case that
// happens, and it must cost a note rather than the session.
//
// The container is a list of chunks (an eight character type, a 32 bit size,
// the bytes) with a CRC-32 chunk last that covers everything before it. A
// whole file passes the checksum, so the walk only has to find the version
// and a shader for this backend.
#pragma once

#include <backend/DriverEnums.h>
#include <filament/MaterialChunkType.h>
#include <filament/MaterialEnums.h>

#include <cstddef>
#include <cstdint>
#include <cstring>
#include <string>

namespace orblit {

namespace package_detail {

inline uint32_t crc32Of(const uint8_t *bytes, size_t length) {
  uint32_t crc = 0xFFFFFFFFu;
  for (size_t i = 0; i < length; i++) {
    crc ^= bytes[i];
    for (int bit = 0; bit < 8; bit++) {
      crc = (crc >> 1) ^ (0xEDB88320u & (0u - (crc & 1u)));
    }
  }
  return ~crc;
}

/// Whether a chunk of this type holds this backend's shaders.
inline bool carriesShaders(uint64_t type, filament::backend::Backend backend) {
  using filament::backend::Backend;
  switch (backend) {
    case Backend::METAL:
      return type == filamat::MaterialMetal ||
             type == filamat::MaterialMetalLibrary;
    case Backend::VULKAN:
      return type == filamat::MaterialSpirv;
    case Backend::OPENGL:
      return type == filamat::MaterialGlsl || type == filamat::MaterialEssl1;
    case Backend::WEBGPU:
      return type == filamat::MaterialWgsl;
    default:
      return true;
  }
}

}  // namespace package_detail

/// Empty when Filament can be handed these bytes, otherwise a sentence that
/// says what is wrong with them.
inline std::string surfacePackageProblem(const uint8_t *bytes, size_t length,
                                         filament::backend::Backend backend) {
  constexpr size_t kChunkHead = sizeof(uint64_t) + sizeof(uint32_t);
  constexpr size_t kCrcChunk = kChunkHead + sizeof(uint32_t);
  if (length < kCrcChunk) return "The package is too short to be a material.";

  uint64_t lastType = 0;
  std::memcpy(&lastType, bytes + length - kCrcChunk, sizeof(lastType));
  if (lastType != filamat::MaterialCrc32) {
    return "The package has no checksum, so it may be unfinished.";
  }
  uint32_t stored = 0;
  std::memcpy(&stored, bytes + length - sizeof(stored), sizeof(stored));
  if (package_detail::crc32Of(bytes, length - kCrcChunk) != stored) {
    return "The package does not match its checksum, so it may be unfinished.";
  }

  bool versioned = false;
  bool shaded = false;
  size_t at = 0;
  while (at + kChunkHead <= length - kCrcChunk) {
    uint64_t type = 0;
    uint32_t size = 0;
    std::memcpy(&type, bytes + at, sizeof(type));
    std::memcpy(&size, bytes + at + sizeof(type), sizeof(size));
    at += kChunkHead;
    if (size > length - kCrcChunk - at) {
      return "A chunk of the package runs past its end.";
    }
    if (type == filamat::MaterialVersion && size == sizeof(uint32_t)) {
      uint32_t version = 0;
      std::memcpy(&version, bytes + at, sizeof(version));
      if (version != filament::MATERIAL_VERSION) {
        return "The package was cooked by matc " + std::to_string(version) +
               " and this build reads " +
               std::to_string(filament::MATERIAL_VERSION) + ".";
      }
      versioned = true;
    }
    shaded = shaded || package_detail::carriesShaders(type, backend);
    at += size;
  }
  if (!versioned) return "The package does not say which matc made it.";
  if (!shaded) return "The package holds no shaders for this graphics backend.";
  return "";
}

}  // namespace orblit
