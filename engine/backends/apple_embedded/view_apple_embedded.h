/**************************************************************************/
/*  view_apple_embedded.h                                                 */

#pragma once

#import <UIKit/UIKit.h>

class String;

@class GDTView;
@class GDTViewRenderer;
@protocol GDTDisplayLayer;

@protocol GDTViewDelegate

- (BOOL)orblitViewFinishedSetup:(GDTView *)view;

@end

@interface GDTView : UIView

@property(weak, nonatomic) GDTViewRenderer *renderer;
@property(weak, nonatomic) id<GDTViewDelegate> delegate;

@property(assign, readonly, nonatomic) BOOL isActive;
@property(assign, nonatomic) BOOL hingeClosed;

@property(assign, nonatomic) BOOL useCADisplayLink;
@property(strong, readonly, nonatomic) CALayer<GDTDisplayLayer> *renderingLayer;
@property(assign, readonly, nonatomic) BOOL canRender;

@property(assign, nonatomic) float preferredFrameRate;

// Can be extended by subclasses
- (void)orblit_commonInit;

// Implemented in subclasses
- (CALayer<GDTDisplayLayer> *)initializeRenderingForDriver:(NSString *)driverName;

- (void)startRendering;
- (void)stopRendering;

@end

// Implemented in subclasses
extern GDTView *GDTViewCreate();
