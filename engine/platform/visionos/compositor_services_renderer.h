/**************************************************************************/
/*  compositor_services_renderer.h                                        */

#pragma once

#import "backends/apple_embedded/renderer.h"

#import "modules/visionos_xr/visionos_definitions.h"

#import <CompositorServices/CompositorServices.h>

@class SpatialEventObjC;

@interface GDTCompositorServicesRenderer : GDTRenderer

- (instancetype)initWithLayerRenderer:(cp_layer_renderer_t)layer_renderer
						 capabilities:(cp_layer_renderer_capabilities_t)capabilities;

- (void)updateXRInterface;

- (void)startRenderLoop;
- (void)renderFrame;
- (void)worldRecentered;

- (void)onSpatialEvent:(SpatialEventObjC *)event;

@end
