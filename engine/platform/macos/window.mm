/**************************************************************************/
/*  window.mm                                                             */

#import "window.h"

#import "display_system_macos.h"

@implementation OrblitWindow

- (id)init {
	self = [super init];
	window_id = DisplaySystemEnums::INVALID_WINDOW_ID;
	anim_duration = -1.0f;
	return self;
}

- (void)setAnimDuration:(NSTimeInterval)duration {
	anim_duration = duration;
}

- (NSTimeInterval)animationResizeTime:(NSRect)newFrame {
	if (anim_duration > 0) {
		return anim_duration;
	} else {
		return [super animationResizeTime:newFrame];
	}
}

- (void)setWindowID:(DisplaySystemEnums::WindowID)wid {
	window_id = wid;
}

- (BOOL)canBecomeKeyWindow {
	// Required for NSWindowStyleMaskBorderless windows.
	DisplaySystemMacOS *ds = (DisplaySystemMacOS *)DisplaySystem::get_singleton();
	if (!ds || !ds->has_window(window_id)) {
		return YES;
	}

	DisplaySystemMacOS::WindowData &wd = ds->get_window(window_id);
	return !wd.no_focus;
}

- (BOOL)canBecomeMainWindow {
	// Required for NSWindowStyleMaskBorderless windows.
	DisplaySystemMacOS *ds = (DisplaySystemMacOS *)DisplaySystem::get_singleton();
	if (!ds || !ds->has_window(window_id)) {
		return YES;
	}

	DisplaySystemMacOS::WindowData &wd = ds->get_window(window_id);
	return !wd.no_focus && !wd.is_popup;
}

@end
