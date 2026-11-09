/**************************************************************************/
/*  view_ios.mm                                                           */

#import "view_ios.h"

#import "display_layer_ios.h"

#include "core/config/project_settings.h"
#include "core/error/error_macros.h"

@interface GDTViewIOS ()

ORBLIT_CLANG_WARNING_PUSH_AND_IGNORE("-Wobjc-property-synthesis")
@property(strong, nonatomic) CALayer<GDTDisplayLayer> *renderingLayer;
ORBLIT_CLANG_WARNING_POP

@end

@implementation GDTViewIOS

- (CALayer<GDTDisplayLayer> *)initializeRenderingForDriver:(NSString *)driverName {
	if (self.renderingLayer) {
		return self.renderingLayer;
	}

	CALayer<GDTDisplayLayer> *layer;

	if ([driverName isEqualToString:@"vulkan"] || [driverName isEqualToString:@"metal"]) {
#if defined(TARGET_OS_SIMULATOR) && TARGET_OS_SIMULATOR
		if (@available(iOS 13, *)) {
			layer = [GDTMetalLayer layer];
		} else {
			return nil;
		}
#else
		layer = [GDTMetalLayer layer];
#endif
#if defined(GLES3_ENABLED)
	} else if ([driverName isEqualToString:@"opengl3"]) {
		ORBLIT_CLANG_WARNING_PUSH_AND_IGNORE("-Wdeprecated-declarations") // OpenGL is deprecated in iOS 12.0.
		layer = [GDTOpenGLLayer layer];
		ORBLIT_CLANG_WARNING_POP
#endif
	} else {
		return nil;
	}

	layer.frame = self.bounds;
	layer.contentsScale = self.contentScaleFactor;

	[self.layer addSublayer:layer];
	self.renderingLayer = layer;

	[layer initializeDisplayLayer];

	return self.renderingLayer;
}

@end

GDTView *GDTViewCreate() {
	GDTViewIOS *view = [GDTViewIOS new];
	if (GLOBAL_GET("display/window/ios/allow_high_refresh_rate")) {
		view.preferredFrameRate = 120;
	} else {
		view.preferredFrameRate = 60;
	}
	return view;
}
