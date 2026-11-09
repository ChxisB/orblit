/**************************************************************************/
/*  gc_keyboard_handler.h                                                 */

#pragma once

#ifdef __OBJC__
#import <Foundation/Foundation.h>
#endif

class GCKeyboardHandler {
public:
	GCKeyboardHandler();
	~GCKeyboardHandler();

	void start();
	void stop();

private:
#ifdef __OBJC__
	id keyboard_connect_observer = nil;
	id keyboard_disconnect_observer = nil;
#else
	void *keyboard_connect_observer = nullptr;
	void *keyboard_disconnect_observer = nullptr;
#endif

	bool active = false;

	void setup_keyboard_handler();
	void remove_keyboard_handler();
};
