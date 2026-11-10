/**************************************************************************/
/*  delta_encoding.h                                                      */

#pragma once

#include "core/containers/span.h"

#include <cstdint>

template <typename T>
class Vector;

class DeltaEncoding {
public:
	static Error encode_delta(Span<uint8_t> p_old_data, Span<uint8_t> p_new_data, Vector<uint8_t> &r_delta, int p_compression_level = 19);
	static Error decode_delta(Span<uint8_t> p_old_data, Span<uint8_t> p_delta, Vector<uint8_t> &r_new_data);
};
