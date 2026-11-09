/**************************************************************************/
/*  content_view.h                                                        */

#pragma once

#include "core/input/input_enums.h"
#include "core/typedefs.h"
#include "systems/display/display_system_enums.h"

#import <AppKit/AppKit.h>
#import <Foundation/Foundation.h>

#if defined(GLES3_ENABLED)
#import <AppKit/NSOpenGLView.h>
#define RootView NSOpenGLView
#else
#define RootView NSView
#endif

#import <QuartzCore/CAMetalLayer.h>

@interface OrblitContentLayerDelegate : NSObject <CALayerDelegate> {
	DisplaySystemEnums::WindowID window_id;
	bool need_redraw;
}

- (void)setWindowID:(DisplaySystemEnums::WindowID)wid;
- (void)setNeedRedraw:(bool)redraw;

@end

ORBLIT_CLANG_WARNING_PUSH_AND_IGNORE("-Wdeprecated-declarations") // OpenGL is deprecated in macOS 10.14.

@interface OrblitContentView : RootView <NSTextInputClient> {
	DisplaySystemEnums::WindowID window_id;
	NSTrackingArea *tracking_area;
	NSMutableAttributedString *marked_text;
	bool ime_input_event_in_progress;
	bool mouse_down_control;
	bool ignore_momentum_scroll;
	bool last_pen_inverted;
	bool ime_suppress_next_keyup;
	id layer_delegate;
	NSMutableSet<NSString *> *registered_observers;
}

- (void)processScrollEvent:(NSEvent *)event button:(MouseButton)button factor:(double)factor;
- (void)processPanEvent:(NSEvent *)event dx:(double)dx dy:(double)dy;
- (void)processMouseEvent:(NSEvent *)event index:(MouseButton)index pressed:(bool)pressed outofstream:(bool)outofstream;
- (void)setWindowID:(DisplaySystemEnums::WindowID)wid;
- (void)updateLayerDelegate;
- (void)cancelComposition;

@end

ORBLIT_CLANG_WARNING_POP
