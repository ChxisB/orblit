/**************************************************************************/
/*  view_renderer.h                                                       */

#pragma once

#import "backends/apple_embedded/renderer.h"

#import <UIKit/UIKit.h>

@interface GDTViewRenderer : GDTRenderer

- (void)renderOnView:(UIView *)view;

@end
