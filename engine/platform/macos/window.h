/**************************************************************************/
/*  window.h                                                              */

#pragma once

#include "systems/display/display_system_enums.h"

#import <AppKit/AppKit.h>
#import <Foundation/Foundation.h>

@interface OrblitWindow : NSWindow {
	DisplaySystemEnums::WindowID window_id;
	NSTimeInterval anim_duration;
}

- (void)setWindowID:(DisplaySystemEnums::WindowID)wid;
- (void)setAnimDuration:(NSTimeInterval)duration;

@end
