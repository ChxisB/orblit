/**************************************************************************/
/*  status_item.mm                                                        */

#import "status_item.h"

#import "display_system_macos.h"

@implementation OrblitStatusItemDelegate

- (id)init {
	self = [super init];
	return self;
}

- (IBAction)click:(id)sender {
	NSEvent *current_event = [NSApp currentEvent];
	MouseButton index = MouseButton::LEFT;
	if (current_event) {
		if (current_event.type == NSEventTypeLeftMouseDown) {
			index = MouseButton::LEFT;
		} else if (current_event.type == NSEventTypeRightMouseDown) {
			index = MouseButton::RIGHT;
		} else if (current_event.type == NSEventTypeOtherMouseDown) {
			if ((int)[current_event buttonNumber] == 2) {
				index = MouseButton::MIDDLE;
			} else if ((int)[current_event buttonNumber] == 3) {
				index = MouseButton::MB_XBUTTON1;
			} else if ((int)[current_event buttonNumber] == 4) {
				index = MouseButton::MB_XBUTTON2;
			}
		}
	}

	DisplaySystemMacOS *ds = (DisplaySystemMacOS *)DisplaySystem::get_singleton();
	if (!ds) {
		return;
	}

	if (cb.is_valid()) {
		Variant v_button = index;
		Variant v_pos = ds->mouse_get_position();
		const Variant *v_args[2] = { &v_button, &v_pos };
		Variant ret;
		Callable::CallError ce;
		cb.callp((const Variant **)&v_args, 2, ret, ce);
		if (ce.error != Callable::CallError::CALL_OK) {
			ERR_PRINT(vformat("Failed to execute status indicator callback: %s.", Variant::get_callable_error_text(cb, v_args, 2, ce)));
		}
	}
}

- (void)setCallback:(const Callable &)callback {
	cb = callback;
}

@end
