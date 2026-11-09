/**************************************************************************/
/*  menu_delegate.mm                                                      */

#import "menu_delegate.h"

#import "display_system_macos.h"
#import "key_mapping_macos.h"
#import "menu_item.h"
#import "native_menu_macos.h"

@implementation OrblitMenuDelegate

- (void)doNothing:(id)sender {
}

- (void)menuWillOpen:(NSMenu *)menu {
	if (NativeMenu::get_singleton()) {
		NativeMenuMacOS *nmenu = (NativeMenuMacOS *)NativeMenu::get_singleton();
		nmenu->_menu_open(menu);
	}
}

- (void)menuNeedsUpdate:(NSMenu *)menu {
	if (NativeMenu::get_singleton()) {
		NativeMenuMacOS *nmenu = (NativeMenuMacOS *)NativeMenu::get_singleton();
		nmenu->_menu_need_update(menu);
	}
}

- (void)menuDidClose:(NSMenu *)menu {
	if (NativeMenu::get_singleton()) {
		NativeMenuMacOS *nmenu = (NativeMenuMacOS *)NativeMenu::get_singleton();
		nmenu->_menu_close(menu);
	}
}

- (void)menu:(NSMenu *)menu willHighlightItem:(NSMenuItem *)item {
	if (item) {
		OrblitMenuItem *value = [item representedObject];
		if (value && value->hover_callback.is_valid()) {
			// If custom callback is set, use it.
			Variant ret;
			Callable::CallError ce;
			const Variant *args[1] = { &value->meta };

			value->hover_callback.callp(args, 1, ret, ce);
			if (ce.error != Callable::CallError::CALL_OK) {
				ERR_PRINT(vformat("Failed to execute menu hover callback: %s.", Variant::get_callable_error_text(value->hover_callback, args, 1, ce)));
			}
		}
	}
}

- (BOOL)menuHasKeyEquivalent:(NSMenu *)menu forEvent:(NSEvent *)event target:(id *)target action:(SEL *)action {
	NSString *ev_key = [[event charactersIgnoringModifiers] lowercaseString];
	NSUInteger ev_modifiers = [event modifierFlags] & NSEventModifierFlagDeviceIndependentFlagsMask;
	for (int i = 0; i < [menu numberOfItems]; i++) {
		const NSMenuItem *menu_item = [menu itemAtIndex:i];
		if ([menu_item isEnabled] && [[menu_item keyEquivalent] compare:ev_key] == NSOrderedSame) {
			NSUInteger item_modifiers = [menu_item keyEquivalentModifierMask];

			if (ev_modifiers == item_modifiers) {
				OrblitMenuItem *value = [menu_item representedObject];
				if (value) {
					if (value->key_callback.is_valid()) {
						// If custom callback is set, use it.
						Variant ret;
						Callable::CallError ce;
						const Variant *args[1] = { &value->meta };

						value->key_callback.callp(args, 1, ret, ce);
						if (ce.error != Callable::CallError::CALL_OK) {
							ERR_PRINT(vformat("Failed to execute menu key callback: %s.", Variant::get_callable_error_text(value->key_callback, args, 1, ce)));
						}
					} else {
						// Otherwise redirect event to the engine.
						if (DisplaySystem::get_singleton()) {
							if ([[NSApplication sharedApplication] keyWindow].sheet) {
								[[[[NSApplication sharedApplication] keyWindow] sheetParent] sendEvent:event];
							} else {
								[[[NSApplication sharedApplication] keyWindow] sendEvent:event];
							}
						}
					}

					// Suppress default menu action.
					*target = self;
					*action = @selector(doNothing:);
				}
				return YES;
			}
		}
	}
	return NO;
}

@end
