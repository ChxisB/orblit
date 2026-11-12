/**************************************************************************/
/*  hashfuncs.cpp                                                         */

#include "hashfuncs.h"

uint32_t hash_murmur3_one_float(float p_in, uint32_t p_seed) {
	union {
		float f;
		uint32_t i;
	} u;

	// Normalize +/- 0.0 and NaN values so they hash the same.
	if (p_in == 0.0f) {
		u.f = 0.0;
	} else if (Math::is_nan(p_in)) {
		u.f = Math::NaN;
	} else {
		u.f = p_in;
	}

	return hash_murmur3_one_32(u.i, p_seed);
}

uint32_t hash_murmur3_one_double(double p_in, uint32_t p_seed) {
	union {
		double d;
		uint64_t i;
	} u;

	// Normalize +/- 0.0 and NaN values so they hash the same.
	if (p_in == 0.0f) {
		u.d = 0.0;
	} else if (Math::is_nan(p_in)) {
		u.d = Math::NaN;
	} else {
		u.d = p_in;
	}

	return hash_murmur3_one_64(u.i, p_seed);
}

uint32_t hash_murmur3_buffer(const void *p_key, int p_length, const uint32_t p_seed) {
	// Although not required, this is a random prime number.
	const uint8_t *data = (const uint8_t *)p_key;
	const int nblocks = p_length / 4;

	uint32_t h1 = p_seed;

	const uint32_t c1 = 0xcc9e2d51;
	const uint32_t c2 = 0x1b873593;

	for (int i = 0; i < nblocks; i++) {
		// The key can be any type, so reading it through a uint32_t pointer breaks strict aliasing.
		uint32_t k1;
		memcpy(&k1, data + i * 4, 4);

		k1 *= c1;
		k1 = hash_rotl32(k1, 15);
		k1 *= c2;

		h1 ^= k1;
		h1 = hash_rotl32(h1, 13);
		h1 = h1 * 5 + 0xe6546b64;
	}

	const uint8_t *tail = data + nblocks * 4;

	uint32_t k1 = 0;

	switch (p_length & 3) {
		case 3:
			k1 ^= tail[2] << 16;
			[[fallthrough]];
		case 2:
			k1 ^= tail[1] << 8;
			[[fallthrough]];
		case 1:
			k1 ^= tail[0];
			k1 *= c1;
			k1 = hash_rotl32(k1, 15);
			k1 *= c2;
			h1 ^= k1;
	};

	// Finalize with additional bit mixing.
	h1 ^= p_length;
	return hash_fmix32(h1);
}

uint32_t hash_djb2_one_float(double p_in, uint32_t p_prev) {
	union {
		double d;
		uint64_t i;
	} u;

	// Normalize +/- 0.0 and NaN values so they hash the same.
	if (p_in == 0.0f) {
		u.d = 0.0;
	} else if (Math::is_nan(p_in)) {
		u.d = Math::NaN;
	} else {
		u.d = p_in;
	}

	return ((p_prev << 5) + p_prev) + hash_one_uint64(u.i);
}

uint64_t hash_djb2_one_float_64(double p_in, uint64_t p_prev) {
	union {
		double d;
		uint64_t i;
	} u;

	// Normalize +/- 0.0 and NaN values so they hash the same.
	if (p_in == 0.0f) {
		u.d = 0.0;
	} else if (Math::is_nan(p_in)) {
		u.d = Math::NaN;
	} else {
		u.d = p_in;
	}

	return ((p_prev << 5) + p_prev) + u.i;
}
