/**************************************************************************/
/*  window_delegate.h                                                     */

#pragma once

#include "systems/display/display_system_enums.h"

#import <AppKit/AppKit.h>
#import <Foundation/Foundation.h>

class DisplaySystemMacOS;

@interface OrblitWindowDelegate : NSObject <NSWindowDelegate>

- (void)setWindowID:(DisplaySystemEnums::WindowID)wid;

- (instancetype)initWithDisplaySystem:(DisplaySystemMacOS *)p_ds;

@end
