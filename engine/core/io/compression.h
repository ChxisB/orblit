/**************************************************************************/
/*  compression.h                                                         */

#pragma once

#include "core/containers/vector.h"
#include "core/typedefs.h"

class Compression {
public:
	static inline int zlib_level = -1; // Z_DEFAULT_COMPRESSION.
	static inline int gzip_level = -1; // Z_DEFAULT_COMPRESSION.
	static inline int zstd_level = 3;
	static inline bool zstd_long_distance_matching = false;
	static inline int zstd_window_log_size = 27; // ZSTD_WINDOWLOG_LIMIT_DEFAULT.
	static inline int gzip_chunk = 16384;

	enum Mode : int32_t {
		MODE_FASTLZ,
		MODE_DEFLATE,
		MODE_ZSTD,
		MODE_GZIP,
		MODE_BROTLI
	};

	static int64_t compress(uint8_t *p_dst, const uint8_t *p_src, int64_t p_src_size, Mode p_mode = MODE_ZSTD);
	static int64_t get_max_compressed_buffer_size(int64_t p_src_size, Mode p_mode = MODE_ZSTD);
	static int64_t decompress(uint8_t *p_dst, int64_t p_dst_max_size, const uint8_t *p_src, int64_t p_src_size, Mode p_mode = MODE_ZSTD);
	static int decompress_dynamic(Vector<uint8_t> *p_dst_vect, int64_t p_max_dst_size, const uint8_t *p_src, int64_t p_src_size, Mode p_mode);
};
