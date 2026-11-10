/**************************************************************************/
/*  view_controller.h                                                     */

#pragma once

#import <UIKit/UIKit.h>

@class GDTView;
@class GDTKeyboardInputView;

@interface GDTViewController : UIViewController

@property(nonatomic, readonly, strong) GDTView *orblitView;
@property(nonatomic, readonly, strong) GDTKeyboardInputView *keyboardView;

#ifdef IOS_ENABLED
- (void)propagateUIPreferencesToRootViewController;
#endif

@end
