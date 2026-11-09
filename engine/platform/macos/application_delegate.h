/**************************************************************************/
/*  application_delegate.h                                                */

#pragma once

#import <AppKit/AppKit.h>
#import <Foundation/Foundation.h>

class OS_MacOS_NSApp;

@interface OrblitApplicationDelegate : NSObject <NSUserInterfaceItemSearching, NSApplicationDelegate, NSMenuItemValidation>

- (OrblitApplicationDelegate *)initWithOS:(OS_MacOS_NSApp *)os;

- (bool)getHighContrast;
- (bool)getReduceMotion;
- (bool)getReduceTransparency;
- (bool)getVoiceOver;
@end
