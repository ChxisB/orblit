/**************************************************************************/
/*  android_keys_utils.cpp                                                */

#include "android_keys_utils.h"

Key orblit_code_from_android_code(unsigned int p_code) {
	for (int i = 0; android_orblit_code_pairs[i].android_code != AKEYCODE_MAX; i++) {
		if (android_orblit_code_pairs[i].android_code == p_code) {
			return android_orblit_code_pairs[i].orblit_code;
		}
	}
	return Key::UNKNOWN;
}

KeyLocation orblit_location_from_android_code(unsigned int p_code) {
	for (int i = 0; android_orblit_location_pairs[i].android_code != AKEYCODE_MAX; i++) {
		if (android_orblit_location_pairs[i].android_code == p_code) {
			return android_orblit_location_pairs[i].orblit_code;
		}
	}
	return KeyLocation::UNSPECIFIED;
}
