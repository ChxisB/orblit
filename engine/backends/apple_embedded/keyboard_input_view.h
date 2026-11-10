/**************************************************************************/
/*  keyboard_input_view.h                                                 */

#pragma once

#import <UIKit/UIKit.h>

@interface GDTKeyboardInputView : UITextView

- (BOOL)becomeFirstResponderWithString:(NSString *)existingString cursorStart:(NSInteger)start cursorEnd:(NSInteger)end;

@end
