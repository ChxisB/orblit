// The flat arrays a host sends for author-written surfaces, read into specs.
//
// Two hosts send them: the C ABI, for every platform that reaches the core
// through it, and the Objective-C wrapper, which holds the core directly.
// Both read the same layout, so the reading is here once.
#pragma once

#include <cstddef>
#include <cstdint>
#include <cstring>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

#include "OrblitRendererTypes.h"
#include "orblit_renderer.h"

namespace orblit {

namespace custom_detail {

inline void remember(std::string &into, const void *bytes, size_t length) {
  into.append(static_cast<const char *>(bytes), length);
  into.push_back('\0');
}

}  // namespace custom_detail

/// The flat arrays of one set_custom_materials call, read front to back.
struct CustomArrays {
  std::vector<std::string> names;
  const int32_t *widths;
  const int32_t *maps;
  const float *values;
  size_t floatTotal;
  std::vector<std::string> paths;
  const int32_t *srgb;
  size_t entry = 0;
  size_t floats = 0;

  /// Reads the next `entries` entries into `spec`: ORBLIT_OK, or the reason
  /// the arrays do not agree with each other.
  int read(CustomSpec &spec, int32_t entries) {
    custom_detail::remember(spec.fingerprint, spec.package.data(),
                            spec.package.size());
    for (int32_t n = 0; n < entries; n++, entry++) {
      if (entry >= names.size()) return ORBLIT_ERROR_LENGTH;
      const int32_t width = widths[entry];
      if (width < 0 || width > 4) return ORBLIT_ERROR_RANGE;
      custom_detail::remember(spec.fingerprint, names[entry].data(),
                              names[entry].size());
      custom_detail::remember(spec.fingerprint, &width, sizeof(width));
      if (width == 0) {
        const int32_t at = maps[entry];
        if (at < 0 || size_t(at) >= paths.size()) return ORBLIT_ERROR_RANGE;
        spec.maps.push_back({names[entry], paths[at], srgb[at] != 0});
        custom_detail::remember(spec.fingerprint, paths[at].data(),
                                paths[at].size());
        custom_detail::remember(spec.fingerprint, &srgb[at], sizeof(srgb[at]));
        continue;
      }
      if (floats + size_t(width) > floatTotal) return ORBLIT_ERROR_LENGTH;
      CustomSpec::Value value;
      value.name = names[entry];
      value.width = width;
      std::memcpy(value.numbers, values + floats, sizeof(float) * size_t(width));
      custom_detail::remember(spec.fingerprint, value.numbers,
                              sizeof(float) * size_t(width));
      spec.values.push_back(value);
      floats += size_t(width);
    }
    return ORBLIT_OK;
  }

  /// Every surface, by key, and a check that no entry or float was left over.
  int readAll(const std::vector<int64_t> &keys,
              const std::vector<std::string> &packages,
              const int32_t *entryCounts,
              std::unordered_map<int64_t, CustomSpec> &out) {
    for (size_t i = 0; i < keys.size(); i++) {
      if (entryCounts[i] < 0) return ORBLIT_ERROR_RANGE;
      CustomSpec spec;
      spec.package = packages[i];
      const int status = read(spec, entryCounts[i]);
      if (status != ORBLIT_OK) return status;
      out[keys[i]] = std::move(spec);
    }
    return entry == names.size() && floats == floatTotal ? ORBLIT_OK
                                                         : ORBLIT_ERROR_LENGTH;
  }
};

}  // namespace orblit
