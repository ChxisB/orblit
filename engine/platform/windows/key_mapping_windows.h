/**************************************************************************/
/*  key_mapping_windows.h                                                 */

#pragma once

#include "core/os/keyboard.h"

#include <windows.h>

#include <winuser.h>

class KeyMappingWindows {
	KeyMappingWindows() {}

public:
	static void initialize();

	static Key get_keysym(unsigned int p_code);
	static unsigned int get_scancode(Key p_keycode);
	static Key get_scansym(unsigned int p_code, bool p_extended);
	static bool is_extended_key(unsigned int p_code);
	static KeyLocation get_location(unsigned int p_code, bool p_extended);
};
