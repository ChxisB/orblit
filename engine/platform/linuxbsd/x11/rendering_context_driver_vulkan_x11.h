/**************************************************************************/
/*  rendering_context_driver_vulkan_x11.h                                 */

#pragma once

#ifdef VULKAN_ENABLED

#include "backends/vulkan/rendering_context_driver_vulkan.h"

#include <X11/Xlib.h>

class RenderingContextDriverVulkanX11 : public RenderingContextDriverVulkan {
private:
	virtual const char *_get_platform_surface_extension() const override final;

protected:
	SurfaceID surface_create(const void *p_platform_data) override final;

public:
	struct WindowPlatformData {
		::Window window;
		Display *display;
	};

	RenderingContextDriverVulkanX11();
	~RenderingContextDriverVulkanX11();
};

#endif // VULKAN_ENABLED
