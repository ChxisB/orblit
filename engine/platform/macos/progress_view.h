/**************************************************************************/
/*  progress_view.h                                                       */

#pragma once

#include "display_system_macos_base.h"

#import <AppKit/AppKit.h>
#import <Foundation/Foundation.h>

@interface OrblitProgressView : NSView {
	DisplaySystemEnums::ProgressState pr_state;
	float pr_value;
	float pr_offset;
}
- (void)setValue:(float)value;
- (void)setState:(DisplaySystemEnums::ProgressState)state;
@end
