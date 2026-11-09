/**************************************************************************/
/*  app_delegate_service_visionos.h                                       */

#pragma once

#import "render_mode_visionos.h"

#import "backends/apple_embedded/app_delegate_service_apple_embedded.h"

#import <CompositorServices/CompositorServices.h>

typedef NS_ENUM(NSInteger, GDTRenderMode) {
	GDTRenderModeWindowed = RenderModeVisionOS::WINDOWED,
	GDTRenderModeCompositorServices = RenderModeVisionOS::COMPOSITOR_SERVICES
};

typedef NS_ENUM(NSInteger, GDTImmersionStyle) {
	GDTImmersionStyleFull,
	GDTImmersionStyleMixed,
	GDTImmersionStyleProgressive
};

typedef NS_ENUM(NSInteger, GDTVisibility) {
	GDTVisibilityAutomatic,
	GDTVisibilityVisible,
	GDTVisibilityHidden
};

@interface GDTAppDelegateServiceVisionOS : GDTAppDelegateService

@property(class, nonatomic, assign) GDTRenderMode renderMode;
@property(class, nonatomic, weak, nullable) cp_layer_renderer_t layerRenderer;
@property(class, nonatomic, strong, nullable) cp_layer_renderer_capabilities_t layerRendererCapabilities;

@property(class, nonatomic, assign, readonly) BOOL isDynamicRenderQualityEnabled;
@property(class, nonatomic, assign, readonly) float maxRenderQuality;

@property(class, nonatomic, assign, readonly) GDTVisibility initialUpperLimbVisibility;
@property(class, nonatomic, assign, readonly) GDTVisibility initialPersistentSystemOverlays;

@property(class, nonatomic, assign) GDTImmersionStyle immersionStyle;

@property(class, nonatomic, assign) GDTVisibility upperLimbVisibility;

@property(class, nonatomic, assign) GDTVisibility persistentSystemOverlays;

@end
