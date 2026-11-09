/**************************************************************************/
/*  rendering_context_driver_vulkan_macos.h                               */

#pragma once

#ifdef VULKAN_ENABLED

#include "backends/vulkan/rendering_context_driver_vulkan.h"

#import <QuartzCore/CAMetalLayer.h>

class RenderingContextDriverVulkanMacOS : public RenderingContextDriverVulkan {
private:
	virtual const char *_get_platform_surface_extension() const override final;

protected:
	SurfaceID surface_create(const void *p_platform_data) override final;

public:
	struct WindowPlatformData {
		CAMetalLayer *const *layer_ptr;
	};

	RenderingContextDriverVulkanMacOS();
	~RenderingContextDriverVulkanMacOS();
};

#endif // VULKAN_ENABLED
