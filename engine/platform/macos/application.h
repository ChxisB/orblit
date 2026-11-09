/**************************************************************************/
/*  application.h                                                         */

#pragma once

#import <AppKit/AppKit.h>
#import <Foundation/Foundation.h>
#import <IOKit/hidsystem/ev_keymap.h>

@class OrblitApplicationDelegate;

@interface OrblitApplication : NSApplication

extern "C" OrblitApplication *OrblitApp;

@property(readonly, nonatomic) OrblitApplicationDelegate *orblitDelegate;

- (OrblitApplication *)init;

- (void)activateApplication;
@end
