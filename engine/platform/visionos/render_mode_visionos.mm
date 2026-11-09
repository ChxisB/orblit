/**************************************************************************/
/*  render_mode_visionos.mm                                               */

#include "render_mode_visionos.h"

#import "app_delegate_service_visionos.h"

#import <CompositorServices/CompositorServices.h>
#import <Metal/Metal.h>

RenderModeVisionOS::Mode RenderModeVisionOS::get_mode() {
	return static_cast<Mode>(GDTAppDelegateServiceVisionOS.renderMode);
}

void *RenderModeVisionOS::get_compositor_services_device() {
	return (__bridge void *)cp_layer_renderer_get_device(GDTAppDelegateServiceVisionOS.layerRenderer);
}
