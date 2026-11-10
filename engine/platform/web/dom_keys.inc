/**************************************************************************/
/*  dom_keys.inc                                                          */

#include "core/os/keyboard.h"

// See https://w3c.github.io/uievents-code/#code-value-tables
Key dom_code2orblit_scancode(EM_UTF8 const p_code[32], EM_UTF8 const p_key[32], bool p_physical) {
#define DOM2ORBLIT(p_str, p_orblit_code) \
	if (memcmp((const void *)p_str, (void *)(p_physical ? p_code : p_key), strlen(p_str) + 1) == 0) { \
		return Key::p_orblit_code; \
	}

	// Numpad section.
	DOM2ORBLIT("NumLock", NUMLOCK);
	DOM2ORBLIT("Numpad0", KP_0);
	DOM2ORBLIT("Numpad1", KP_1);
	DOM2ORBLIT("Numpad2", KP_2);
	DOM2ORBLIT("Numpad3", KP_3);
	DOM2ORBLIT("Numpad4", KP_4);
	DOM2ORBLIT("Numpad5", KP_5);
	DOM2ORBLIT("Numpad6", KP_6);
	DOM2ORBLIT("Numpad7", KP_7);
	DOM2ORBLIT("Numpad8", KP_8);
	DOM2ORBLIT("Numpad9", KP_9);
	DOM2ORBLIT("NumpadAdd", KP_ADD);
	DOM2ORBLIT("NumpadBackspace", BACKSPACE);
	DOM2ORBLIT("Clear", CLEAR); // NumLock on macOS.
	DOM2ORBLIT("NumpadClear", CLEAR);
	DOM2ORBLIT("NumpadClearEntry", CLEAR);
	//DOM2ORBLIT("NumpadComma", UNKNOWN);
	DOM2ORBLIT("NumpadDecimal", KP_PERIOD);
	DOM2ORBLIT("NumpadDivide", KP_DIVIDE);
	DOM2ORBLIT("NumpadEnter", KP_ENTER);
	DOM2ORBLIT("NumpadEqual", EQUAL);
	//DOM2ORBLIT("NumpadHash", UNKNOWN);
	//DOM2ORBLIT("NumpadMemoryAdd", UNKNOWN);
	//DOM2ORBLIT("NumpadMemoryClear", UNKNOWN);
	//DOM2ORBLIT("NumpadMemoryRecall", UNKNOWN);
	//DOM2ORBLIT("NumpadMemoryStore", UNKNOWN);
	//DOM2ORBLIT("NumpadMemorySubtract", UNKNOWN);
	DOM2ORBLIT("NumpadMultiply", KP_MULTIPLY);
	DOM2ORBLIT("NumpadParenLeft", PARENLEFT);
	DOM2ORBLIT("NumpadParenRight", PARENRIGHT);
	DOM2ORBLIT("NumpadStar", KP_MULTIPLY); // or ASTERISK ?
	DOM2ORBLIT("NumpadSubtract", KP_SUBTRACT);

	// Alphanumeric section.
	DOM2ORBLIT("Backquote", QUOTELEFT);
	DOM2ORBLIT("Backslash", BACKSLASH);
	DOM2ORBLIT("BracketLeft", BRACKETLEFT);
	DOM2ORBLIT("BracketRight", BRACKETRIGHT);
	DOM2ORBLIT("Comma", COMMA);
	DOM2ORBLIT("Digit0", KEY_0);
	DOM2ORBLIT("Digit1", KEY_1);
	DOM2ORBLIT("Digit2", KEY_2);
	DOM2ORBLIT("Digit3", KEY_3);
	DOM2ORBLIT("Digit4", KEY_4);
	DOM2ORBLIT("Digit5", KEY_5);
	DOM2ORBLIT("Digit6", KEY_6);
	DOM2ORBLIT("Digit7", KEY_7);
	DOM2ORBLIT("Digit8", KEY_8);
	DOM2ORBLIT("Digit9", KEY_9);
	DOM2ORBLIT("Equal", EQUAL);
	DOM2ORBLIT("IntlBackslash", BACKSLASH);
	//DOM2ORBLIT("IntlRo", UNKNOWN);
	DOM2ORBLIT("IntlYen", YEN);

	DOM2ORBLIT("KeyA", A);
	DOM2ORBLIT("KeyB", B);
	DOM2ORBLIT("KeyC", C);
	DOM2ORBLIT("KeyD", D);
	DOM2ORBLIT("KeyE", E);
	DOM2ORBLIT("KeyF", F);
	DOM2ORBLIT("KeyG", G);
	DOM2ORBLIT("KeyH", H);
	DOM2ORBLIT("KeyI", I);
	DOM2ORBLIT("KeyJ", J);
	DOM2ORBLIT("KeyK", K);
	DOM2ORBLIT("KeyL", L);
	DOM2ORBLIT("KeyM", M);
	DOM2ORBLIT("KeyN", N);
	DOM2ORBLIT("KeyO", O);
	DOM2ORBLIT("KeyP", P);
	DOM2ORBLIT("KeyQ", Q);
	DOM2ORBLIT("KeyR", R);
	DOM2ORBLIT("KeyS", S);
	DOM2ORBLIT("KeyT", T);
	DOM2ORBLIT("KeyU", U);
	DOM2ORBLIT("KeyV", V);
	DOM2ORBLIT("KeyW", W);
	DOM2ORBLIT("KeyX", X);
	DOM2ORBLIT("KeyY", Y);
	DOM2ORBLIT("KeyZ", Z);

	DOM2ORBLIT("Minus", MINUS);
	DOM2ORBLIT("Period", PERIOD);
	DOM2ORBLIT("Quote", APOSTROPHE);
	DOM2ORBLIT("Semicolon", SEMICOLON);
	DOM2ORBLIT("Slash", SLASH);

	// Functional keys in the Alphanumeric section.
	DOM2ORBLIT("Alt", ALT);
	DOM2ORBLIT("AltLeft", ALT);
	DOM2ORBLIT("AltRight", ALT);
	DOM2ORBLIT("Backspace", BACKSPACE);
	DOM2ORBLIT("CapsLock", CAPSLOCK);
	DOM2ORBLIT("ContextMenu", MENU);
	DOM2ORBLIT("Control", CTRL);
	DOM2ORBLIT("ControlLeft", CTRL);
	DOM2ORBLIT("ControlRight", CTRL);
	DOM2ORBLIT("Enter", ENTER);
	DOM2ORBLIT("Meta", META);
	DOM2ORBLIT("MetaLeft", META);
	DOM2ORBLIT("MetaRight", META);
	DOM2ORBLIT("OSLeft", META); // Command on macOS.
	DOM2ORBLIT("OSRight", META); // Command on macOS.
	DOM2ORBLIT("Shift", SHIFT);
	DOM2ORBLIT("ShiftLeft", SHIFT);
	DOM2ORBLIT("ShiftRight", SHIFT);
	DOM2ORBLIT("Space", SPACE);
	DOM2ORBLIT("Tab", TAB);

	// ControlPad section.
	DOM2ORBLIT("Delete", KEY_DELETE);
	DOM2ORBLIT("End", END);
	DOM2ORBLIT("Help", HELP);
	DOM2ORBLIT("Home", HOME);
	DOM2ORBLIT("Insert", INSERT);
	DOM2ORBLIT("PageDown", PAGEDOWN);
	DOM2ORBLIT("PageUp", PAGEUP);

	// ArrowPad section.
	DOM2ORBLIT("ArrowDown", DOWN);
	DOM2ORBLIT("ArrowLeft", LEFT);
	DOM2ORBLIT("ArrowRight", RIGHT);
	DOM2ORBLIT("ArrowUp", UP);

	// Function section.
	DOM2ORBLIT("Escape", ESCAPE);
	DOM2ORBLIT("F1", F1);
	DOM2ORBLIT("F2", F2);
	DOM2ORBLIT("F3", F3);
	DOM2ORBLIT("F4", F4);
	DOM2ORBLIT("F5", F5);
	DOM2ORBLIT("F6", F6);
	DOM2ORBLIT("F7", F7);
	DOM2ORBLIT("F8", F8);
	DOM2ORBLIT("F9", F9);
	DOM2ORBLIT("F10", F10);
	DOM2ORBLIT("F11", F11);
	DOM2ORBLIT("F12", F12);
	//DOM2ORBLIT("Fn", UNKNOWN); // never actually fired, but included in the standard draft.
	//DOM2ORBLIT("FnLock", UNKNOWN);
	DOM2ORBLIT("PrintScreen", PRINT);
	DOM2ORBLIT("ScrollLock", SCROLLLOCK);
	DOM2ORBLIT("Pause", PAUSE);

	// Media keys section.
	DOM2ORBLIT("BrowserBack", BACK);
	DOM2ORBLIT("BrowserFavorites", FAVORITES);
	DOM2ORBLIT("BrowserForward", FORWARD);
	DOM2ORBLIT("BrowserHome", OPENURL);
	DOM2ORBLIT("BrowserRefresh", REFRESH);
	DOM2ORBLIT("BrowserSearch", SEARCH);
	DOM2ORBLIT("BrowserStop", STOP);
	//DOM2ORBLIT("Eject", UNKNOWN);
	DOM2ORBLIT("LaunchApp1", LAUNCH0);
	DOM2ORBLIT("LaunchApp2", LAUNCH1);
	DOM2ORBLIT("LaunchMail", LAUNCHMAIL);
	DOM2ORBLIT("MediaPlayPause", MEDIAPLAY);
	DOM2ORBLIT("MediaSelect", LAUNCHMEDIA);
	DOM2ORBLIT("MediaStop", MEDIASTOP);
	DOM2ORBLIT("MediaTrackNext", MEDIANEXT);
	DOM2ORBLIT("MediaTrackPrevious", MEDIAPREVIOUS);
	//DOM2ORBLIT("Power", UNKNOWN);
	//DOM2ORBLIT("Sleep", UNKNOWN);
	DOM2ORBLIT("AudioVolumeDown", VOLUMEDOWN);
	DOM2ORBLIT("AudioVolumeMute", VOLUMEMUTE);
	DOM2ORBLIT("AudioVolumeUp", VOLUMEUP);
	//DOM2ORBLIT("WakeUp", UNKNOWN);

	// Printable ASCII.
	uint8_t b0 = (uint8_t)p_key[0];
	uint8_t b1 = (uint8_t)p_key[1];
	if (b0 >= 0x20 && b0 < 0x7F) { // ASCII.
		if (b0 > 0x60 && b0 < 0x7B) { // Lowercase ASCII.
			b0 -= 32;
		}
		return (Key)b0;
	} else if (b0 == 0xC2 && b1 == 0xA5) {
		return Key::YEN;
	} else if (b0 == 0xC2 && b1 == 0xA7) {
		return Key::SECTION;
	}

	return Key::NONE;
#undef DOM2ORBLIT
}

KeyLocation dom_code2orblit_key_location(EM_UTF8 const p_code[32]) {
#define DOM2ORBLIT(m_str, m_orblit_code) \
	if (memcmp((const void *)m_str, (void *)p_code, strlen(m_str) + 1) == 0) { \
		return KeyLocation::m_orblit_code; \
	}

	DOM2ORBLIT("AltLeft", LEFT);
	DOM2ORBLIT("AltRight", RIGHT);
	DOM2ORBLIT("ControlLeft", LEFT);
	DOM2ORBLIT("ControlRight", RIGHT);
	DOM2ORBLIT("MetaLeft", LEFT);
	DOM2ORBLIT("MetaRight", RIGHT);
	DOM2ORBLIT("OSLeft", LEFT);
	DOM2ORBLIT("OSRight", RIGHT);
	DOM2ORBLIT("ShiftLeft", LEFT);
	DOM2ORBLIT("ShiftRight", RIGHT);

	return KeyLocation::UNSPECIFIED;
#undef DOM2ORBLIT
}
