/**************************************************************************/
/*  application.mm                                                        */

#import "application.h"

#import "display_system_macos.h"
#import "application_delegate.h"
#import "os_macos.h"

OrblitApplication *OrblitApp = nil;

@interface OrblitApplication ()
- (void)forceUnbundledWindowActivationHackStep1;
- (void)forceUnbundledWindowActivationHackStep2;
- (void)forceUnbundledWindowActivationHackStep3;
@end

@implementation OrblitApplication

- (OrblitApplication *)init {
	self = [super init];

	OrblitApp = self;

	return self;
}

- (OrblitApplicationDelegate *)orblitDelegate {
	return (OrblitApplicationDelegate *)self.delegate;
}

- (void)activateApplication {
	[NSApp activateIgnoringOtherApps:YES];
	NSString *nsappname = [[[NSBundle mainBundle] infoDictionary] objectForKey:@"CFBundleName"];
	const char *bundled_id = getenv("__CFBundleIdentifier");
	NSString *nsbundleid_env = [NSString stringWithUTF8String:(bundled_id != nullptr) ? bundled_id : ""];
	NSString *nsbundleid = [[NSBundle mainBundle] bundleIdentifier];
	if (nsappname == nil || isatty(STDOUT_FILENO) || isatty(STDIN_FILENO) || isatty(STDERR_FILENO) || ![nsbundleid isEqualToString:nsbundleid_env]) {
#ifdef TOOLS_ENABLED
		if (!OS_MacOS::is_debugger_attached()) {
#else
		{
#endif
			// If the executable is started from terminal or is not bundled, macOS WindowServer won't register and activate app window correctly (menu and title bar are grayed out and input ignored).
			[self performSelector:@selector(forceUnbundledWindowActivationHackStep1) withObject:nil afterDelay:0.02];
		}
	}
	// Force reset main menu on macOS 27.
	NSOperatingSystemVersion ver = [NSProcessInfo processInfo].operatingSystemVersion;
	if (ver.majorVersion >= 27) {
		NSMenu *main_menu = NSApp.mainMenu;
		if (main_menu != nil) {
			NSApp.mainMenu = nil;
			NSApp.mainMenu = main_menu;
		}
	}
}

- (void)mediaKeyEvent:(int)key state:(BOOL)state repeat:(BOOL)repeat {
	Key keycode = Key::NONE;
	switch (key) {
		case NX_KEYTYPE_SOUND_UP: {
			keycode = Key::VOLUMEUP;
		} break;
		case NX_KEYTYPE_SOUND_DOWN: {
			keycode = Key::VOLUMEDOWN;
		} break;
		//NX_KEYTYPE_BRIGHTNESS_UP
		//NX_KEYTYPE_BRIGHTNESS_DOWN
		case NX_KEYTYPE_CAPS_LOCK: {
			keycode = Key::CAPSLOCK;
		} break;
		case NX_KEYTYPE_HELP: {
			keycode = Key::HELP;
		} break;
		case NX_POWER_KEY: {
			keycode = Key::STANDBY;
		} break;
		case NX_KEYTYPE_MUTE: {
			keycode = Key::VOLUMEMUTE;
		} break;
		//NX_KEYTYPE_CONTRAST_UP
		//NX_KEYTYPE_CONTRAST_DOWN
		//NX_KEYTYPE_LAUNCH_PANEL
		//NX_KEYTYPE_EJECT
		//NX_KEYTYPE_VIDMIRROR
		//NX_KEYTYPE_FAST
		//NX_KEYTYPE_REWIND
		//NX_KEYTYPE_ILLUMINATION_UP
		//NX_KEYTYPE_ILLUMINATION_DOWN
		//NX_KEYTYPE_ILLUMINATION_TOGGLE
		case NX_KEYTYPE_PLAY: {
			keycode = Key::MEDIAPLAY;
		} break;
		case NX_KEYTYPE_NEXT: {
			keycode = Key::MEDIANEXT;
		} break;
		case NX_KEYTYPE_PREVIOUS: {
			keycode = Key::MEDIAPREVIOUS;
		} break;
		default: {
			keycode = Key::NONE;
		} break;
	}

	DisplaySystemMacOS *ds = Object::cast_to<DisplaySystemMacOS>(DisplaySystem::get_singleton());
	if (ds && keycode != Key::NONE) {
		DisplaySystemMacOS::KeyEvent ke;

		ke.window_id = ds->_get_focused_window_or_popup();
		ke.macos_state = 0;
		ke.pressed = state;
		ke.echo = repeat;
		ke.keycode = keycode;
		ke.physical_keycode = keycode;
		ke.key_label = keycode;
		ke.unicode = 0;
		ke.raw = true;

		ds->push_to_key_event_buffer(ke);
	}
}

- (void)sendEvent:(NSEvent *)event {
	if ([event type] == NSEventTypeSystemDefined && [event subtype] == 8) {
		int keyCode = (([event data1] & 0xFFFF0000) >> 16);
		int keyFlags = ([event data1] & 0x0000FFFF);
		int keyState = (((keyFlags & 0xFF00) >> 8)) == 0xA;
		int keyRepeat = (keyFlags & 0x1);

		[self mediaKeyEvent:keyCode state:keyState repeat:keyRepeat];
	}

	DisplaySystemMacOS *ds = Object::cast_to<DisplaySystemMacOS>(DisplaySystem::get_singleton());
	if (ds) {
		if ([event type] == NSEventTypeLeftMouseDown || [event type] == NSEventTypeRightMouseDown || [event type] == NSEventTypeOtherMouseDown) {
			if (ds->mouse_process_popups()) {
				return;
			}
		}
		ds->send_event(event);
	}

	// From http://cocoadev.com/index.pl?GameKeyboardHandlingAlmost
	// This works around an AppKit bug, where key up events while holding
	// down the command key don't get sent to the key window.
	if ([event type] == NSEventTypeKeyUp && ([event modifierFlags] & NSEventModifierFlagCommand)) {
		[[self keyWindow] sendEvent:event];
	} else {
		[super sendEvent:event];
	}
}

#if __MAC_OS_X_VERSION_MIN_REQUIRED >= 140000
// This option was deprecated in macOS 14.0, replace it with 0 (no options) when min version is 14.0 or higher.
#define NSApplicationActivateIgnoringOtherApps 0
#endif

- (void)forceUnbundledWindowActivationHackStep1 {
	// Step 1: Switch focus to macOS SystemUIServer process.
	// Required to perform step 2, TransformProcessType will fail if app is already the in focus.
	for (NSRunningApplication *app in [NSRunningApplication runningApplicationsWithBundleIdentifier:@"com.apple.systemuiserver"]) {
		[app activateWithOptions:NSApplicationActivateIgnoringOtherApps];
		break;
	}
	[self performSelector:@selector(forceUnbundledWindowActivationHackStep2)
			   withObject:nil
			   afterDelay:0.02];
}

- (void)forceUnbundledWindowActivationHackStep2 {
	// Step 2: Register app as foreground process.
	ProcessSerialNumber psn = { 0, kCurrentProcess };
	(void)TransformProcessType(&psn, kProcessTransformToForegroundApplication);
	[self performSelector:@selector(forceUnbundledWindowActivationHackStep3) withObject:nil afterDelay:0.02];
}

- (void)forceUnbundledWindowActivationHackStep3 {
	// Step 3: Switch focus back to app window.
	[[NSRunningApplication currentApplication] activateWithOptions:NSApplicationActivateIgnoringOtherApps];
}

#if __MAC_OS_X_VERSION_MIN_REQUIRED >= 140000
#undef NSApplicationActivateIgnoringOtherApps
#endif

@end
