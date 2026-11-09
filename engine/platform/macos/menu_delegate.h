/**************************************************************************/
/*  menu_delegate.h                                                       */

#pragma once

#import <AppKit/AppKit.h>
#import <Foundation/Foundation.h>

@interface OrblitMenuDelegate : NSObject <NSMenuDelegate> {
}

- (void)doNothing:(id)sender;

@end
