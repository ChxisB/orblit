/**************************************************************************/
/*  key_mapping_apple_embedded.h                                          */

#pragma once

#include "core/os/keyboard.h"

#import <UIKit/UIKit.h>

class KeyMappingAppleEmbedded {
	KeyMappingAppleEmbedded() {}

public:
	static void initialize();
	static Key remap_key(CFIndex p_keycode);
	static KeyLocation key_location(CFIndex p_keycode);
};
