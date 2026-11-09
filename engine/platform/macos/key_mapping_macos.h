/**************************************************************************/
/*  key_mapping_macos.h                                                   */

#pragma once

#include "core/os/keyboard.h"

class KeyMappingMacOS {
	KeyMappingMacOS() {}

	static bool is_numpad_key(unsigned int p_key);

public:
	static void initialize();

	// Mappings input.
	static Key translate_key(unsigned int p_key);
	static unsigned int unmap_key(Key p_key);
	static Key remap_key(unsigned int p_key, unsigned int p_state, bool p_unicode);
	static KeyLocation translate_location(unsigned int p_key);

	// Mapping for menu shortcuts.
	static String keycode_get_native_string(Key p_keycode);
	static unsigned int keycode_get_native_mask(Key p_keycode);
};
