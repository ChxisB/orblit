/**************************************************************************/
/*  view_visionos.mm                                                      */

#include "view_visionos.h"

#include "display_layer_visionos.h"

#include "core/error/error_macros.h"

#import <GameController/GameController.h>

@interface GDTViewVisionOS ()

ORBLIT_CLANG_WARNING_PUSH_AND_IGNORE("-Wobjc-property-synthesis")
@property(strong, nonatomic) CALayer<GDTDisplayLayer> *renderingLayer;
ORBLIT_CLANG_WARNING_POP

@end

@implementation GDTViewVisionOS

- (void)orblit_commonInit {
	[super orblit_commonInit];

	// Enable GamePad handler
	GCEventInteraction *gamepadInteraction = [[GCEventInteraction alloc] init];
	gamepadInteraction.handledEventTypes = GCUIEventTypeGamepad;
	[self addInteraction:gamepadInteraction];
}

- (CALayer<GDTDisplayLayer> *)initializeRenderingForDriver:(NSString *)driverName {
	if (self.renderingLayer) {
		return self.renderingLayer;
	}

	CALayer<GDTDisplayLayer> *layer = [GDTMetalLayer layer];

	layer.frame = self.bounds;
	layer.contentsScale = self.contentScaleFactor;

	[self.layer addSublayer:layer];
	self.renderingLayer = layer;

	[layer initializeDisplayLayer];

	return self.renderingLayer;
}

@end

GDTView *GDTViewCreate() {
	GDTViewVisionOS *view = [GDTViewVisionOS new];
	view.preferredFrameRate = 90;
	return view;
}
