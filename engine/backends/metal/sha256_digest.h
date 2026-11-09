/**************************************************************************/
/*  sha256_digest.h                                                       */

#pragma once

#include "core/containers/hashfuncs.h"
#include "core/containers/local_vector.h"

#include <CommonCrypto/CommonDigest.h>
#include <simd/simd.h>
#include <zlib.h>

struct SHA256Digest {
	unsigned char data[CC_SHA256_DIGEST_LENGTH];

	static constexpr size_t serialized_size() { return CC_SHA256_DIGEST_LENGTH; }

	uint32_t hash() const {
		uint32_t c = crc32(0, data, CC_SHA256_DIGEST_LENGTH);
		return c;
	}

	SHA256Digest() {
		bzero(data, CC_SHA256_DIGEST_LENGTH);
	}

	SHA256Digest(const char *p_hash) {
		memcpy(data, p_hash, CC_SHA256_DIGEST_LENGTH);
	}

	SHA256Digest(const char *p_data, size_t p_length) {
		CC_SHA256(p_data, (CC_LONG)p_length, data);
	}

	_FORCE_INLINE_ uint32_t short_sha() const {
		return __builtin_bswap32(*(uint32_t *)&data[0]);
	}

	LocalVector<uint8_t> serialize() const {
		LocalVector<uint8_t> result;
		result.resize(CC_SHA256_DIGEST_LENGTH);
		memcpy(result.ptr(), data, CC_SHA256_DIGEST_LENGTH);
		return result;
	}

	static SHA256Digest deserialize(LocalVector<uint8_t> p_ser) {
		return SHA256Digest((const char *)p_ser.ptr());
	}
};

template <>
struct HashMapComparatorDefault<SHA256Digest> {
	static bool compare(const SHA256Digest &p_lhs, const SHA256Digest &p_rhs) {
		return memcmp(p_lhs.data, p_rhs.data, CC_SHA256_DIGEST_LENGTH) == 0;
	}
};
