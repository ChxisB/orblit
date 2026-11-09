/**************************************************************************/
/*  key_mapping_xkb.h                                                     */

#pragma once

#include "core/containers/hash_map.h"
#include "core/os/keyboard.h"

typedef uint32_t xkb_keycode_t;
typedef uint32_t xkb_keysym_t;

class KeyMappingXKB {
	struct HashMapHasherKeys {
		static _FORCE_INLINE_ uint32_t hash(Key p_key) { return hash_fmix32(static_cast<uint32_t>(p_key)); }
		static _FORCE_INLINE_ uint32_t hash(unsigned p_key) { return hash_fmix32(p_key); }
	};

	static inline HashMap<xkb_keycode_t, Key, HashMapHasherKeys> xkb_keycode_map;
	static inline HashMap<unsigned int, Key, HashMapHasherKeys> scancode_map;
	static inline HashMap<Key, unsigned int, HashMapHasherKeys> scancode_map_inv;
	static inline HashMap<unsigned int, KeyLocation, HashMapHasherKeys> location_map;

	KeyMappingXKB() {}

public:
	static void initialize();

	static Key get_keycode(xkb_keysym_t p_keysym);
	static xkb_keycode_t get_xkb_keycode(Key p_keycode);
	static Key get_scancode(unsigned int p_code);
	static KeyLocation get_location(unsigned int p_code);
};
