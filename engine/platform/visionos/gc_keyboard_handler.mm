/**************************************************************************/
/*  gc_keyboard_handler.mm                                                */

#import "gc_keyboard_handler.h"

#include "core/os/keyboard.h"
#import "backends/apple_embedded/display_system_apple_embedded.h"
#import "backends/apple_embedded/key_mapping_apple_embedded.h"

#import <GameController/GameController.h>

// Derive a basic US-layout unshifted character from a GCKeyCode.
// Used for key label and unicode generation when UIPress is not available.
// GCKeyCode constants are extern CFIndex globals, so an if/else chain is required (not switch).
static char32_t unshifted_char_from_keycode(GCKeyCode p_keycode) {
	// Letter and digit blocks are guaranteed contiguous by the HID usage table.
	if (p_keycode >= GCKeyCodeKeyA && p_keycode <= GCKeyCodeKeyZ) {
		return 'a' + (p_keycode - GCKeyCodeKeyA);
	}
	if (p_keycode >= GCKeyCodeOne && p_keycode <= GCKeyCodeNine) {
		return '1' + (p_keycode - GCKeyCodeOne);
	}
	if (p_keycode == GCKeyCodeZero) {
		return '0';
	}
	if (p_keycode == GCKeyCodeReturnOrEnter) {
		return '\r';
	}
	if (p_keycode == GCKeyCodeTab) {
		return '\t';
	}
	if (p_keycode == GCKeyCodeSpacebar) {
		return ' ';
	}
	if (p_keycode == GCKeyCodeHyphen) {
		return '-';
	}
	if (p_keycode == GCKeyCodeEqualSign) {
		return '=';
	}
	if (p_keycode == GCKeyCodeOpenBracket) {
		return '[';
	}
	if (p_keycode == GCKeyCodeCloseBracket) {
		return ']';
	}
	if (p_keycode == GCKeyCodeBackslash) {
		return '\\';
	}
	if (p_keycode == GCKeyCodeSemicolon) {
		return ';';
	}
	if (p_keycode == GCKeyCodeQuote) {
		return '\'';
	}
	if (p_keycode == GCKeyCodeGraveAccentAndTilde) {
		return '`';
	}
	if (p_keycode == GCKeyCodeComma) {
		return ',';
	}
	if (p_keycode == GCKeyCodePeriod) {
		return '.';
	}
	if (p_keycode == GCKeyCodeSlash) {
		return '/';
	}
	return 0;
}

// Derive a shifted character for US layout.
static char32_t shifted_char_from_keycode(GCKeyCode p_keycode) {
	if (p_keycode >= GCKeyCodeKeyA && p_keycode <= GCKeyCodeKeyZ) {
		return 'A' + (p_keycode - GCKeyCodeKeyA);
	}
	if (p_keycode == GCKeyCodeOne) {
		return '!';
	}
	if (p_keycode == GCKeyCodeTwo) {
		return '@';
	}
	if (p_keycode == GCKeyCodeThree) {
		return '#';
	}
	if (p_keycode == GCKeyCodeFour) {
		return '$';
	}
	if (p_keycode == GCKeyCodeFive) {
		return '%';
	}
	if (p_keycode == GCKeyCodeSix) {
		return '^';
	}
	if (p_keycode == GCKeyCodeSeven) {
		return '&';
	}
	if (p_keycode == GCKeyCodeEight) {
		return '*';
	}
	if (p_keycode == GCKeyCodeNine) {
		return '(';
	}
	if (p_keycode == GCKeyCodeZero) {
		return ')';
	}
	if (p_keycode == GCKeyCodeHyphen) {
		return '_';
	}
	if (p_keycode == GCKeyCodeEqualSign) {
		return '+';
	}
	if (p_keycode == GCKeyCodeOpenBracket) {
		return '{';
	}
	if (p_keycode == GCKeyCodeCloseBracket) {
		return '}';
	}
	if (p_keycode == GCKeyCodeBackslash) {
		return '|';
	}
	if (p_keycode == GCKeyCodeSemicolon) {
		return ':';
	}
	if (p_keycode == GCKeyCodeQuote) {
		return '"';
	}
	if (p_keycode == GCKeyCodeGraveAccentAndTilde) {
		return '~';
	}
	if (p_keycode == GCKeyCodeComma) {
		return '<';
	}
	if (p_keycode == GCKeyCodePeriod) {
		return '>';
	}
	if (p_keycode == GCKeyCodeSlash) {
		return '?';
	}
	return unshifted_char_from_keycode(p_keycode);
}

GCKeyboardHandler::GCKeyboardHandler() {
}

GCKeyboardHandler::~GCKeyboardHandler() {
	stop();
}

void GCKeyboardHandler::setup_keyboard_handler() {
	GCKeyboard *keyboard = [GCKeyboard coalescedKeyboard];
	if (!keyboard || !keyboard.keyboardInput) {
		return;
	}

	keyboard.keyboardInput.keyChangedHandler = ^(GCKeyboardInput *kbInput, GCDeviceButtonInput *key, GCKeyCode keyCode, BOOL pressed) {
		DisplaySystemAppleEmbedded *ds = DisplaySystemAppleEmbedded::get_singleton();
		if (!ds) {
			return;
		}

		// Skip if the virtual keyboard is active (soft keyboard for text input).
		if (ds->is_keyboard_active()) {
			return;
		}

		Key orblit_key = KeyMappingAppleEmbedded::remap_key(keyCode);
		if (orblit_key == Key::NONE) {
			return;
		}

		KeyLocation location = KeyMappingAppleEmbedded::key_location(keyCode);

		// Build modifier flags compatible with UIKeyModifier constants.
		NSInteger modifier_flags = 0;
		if ([kbInput buttonForKeyCode:GCKeyCodeLeftShift].pressed || [kbInput buttonForKeyCode:GCKeyCodeRightShift].pressed) {
			modifier_flags |= UIKeyModifierShift;
		}
		if ([kbInput buttonForKeyCode:GCKeyCodeLeftControl].pressed || [kbInput buttonForKeyCode:GCKeyCodeRightControl].pressed) {
			modifier_flags |= UIKeyModifierControl;
		}
		if ([kbInput buttonForKeyCode:GCKeyCodeLeftAlt].pressed || [kbInput buttonForKeyCode:GCKeyCodeRightAlt].pressed) {
			modifier_flags |= UIKeyModifierAlternate;
		}
		if ([kbInput buttonForKeyCode:GCKeyCodeLeftGUI].pressed || [kbInput buttonForKeyCode:GCKeyCodeRightGUI].pressed) {
			modifier_flags |= UIKeyModifierCommand;
		}

		// Derive character from key code (US layout approximation).
		char32_t us = unshifted_char_from_keycode(keyCode);
		char32_t typed_char = 0;
		if (pressed) {
			if (modifier_flags & UIKeyModifierShift) {
				typed_char = shifted_char_from_keycode(keyCode);
			} else {
				typed_char = us;
			}
		}

		ds->key(fix_keycode(us, orblit_key), typed_char, fix_key_label(us, orblit_key), orblit_key, modifier_flags, pressed, location);
	};
}

void GCKeyboardHandler::remove_keyboard_handler() {
	GCKeyboard *keyboard = [GCKeyboard coalescedKeyboard];
	if (keyboard && keyboard.keyboardInput) {
		keyboard.keyboardInput.keyChangedHandler = nil;
	}
}

void GCKeyboardHandler::start() {
	if (active) {
		return;
	}
	active = true;

	// Set up handler for already-connected keyboard.
	setup_keyboard_handler();

	// Observe keyboard connect/disconnect.
	keyboard_connect_observer = [[NSNotificationCenter defaultCenter]
			addObserverForName:GCKeyboardDidConnectNotification
						object:nil
						 queue:nil
					usingBlock:^(NSNotification *note) {
						setup_keyboard_handler();
					}];

	keyboard_disconnect_observer = [[NSNotificationCenter defaultCenter]
			addObserverForName:GCKeyboardDidDisconnectNotification
						object:nil
						 queue:nil
					usingBlock:^(NSNotification *note){
							// Keyboard disconnected; handler is automatically invalidated.
					}];
}

void GCKeyboardHandler::stop() {
	if (!active) {
		return;
	}
	active = false;

	remove_keyboard_handler();

	if (keyboard_connect_observer) {
		[[NSNotificationCenter defaultCenter] removeObserver:keyboard_connect_observer];
		keyboard_connect_observer = nil;
	}
	if (keyboard_disconnect_observer) {
		[[NSNotificationCenter defaultCenter] removeObserver:keyboard_disconnect_observer];
		keyboard_disconnect_observer = nil;
	}
}
