/**************************************************************************/
/*  rendering_context_driver_vulkan_wayland.h                             */

#pragma once

#ifdef VULKAN_ENABLED

#include "backends/vulkan/rendering_context_driver_vulkan.h"

class RenderingContextDriverVulkanWayland : public RenderingContextDriverVulkan {
private:
	virtual const char *_get_platform_surface_extension() const override final;
	// If wp-color-management is supported, we will perform color management externally to the driver.
	// If wp-color-management is not supported, the driver would not be able to perform color management anyway.
	virtual bool is_colorspace_externally_managed() const override final { return true; }

protected:
	SurfaceID surface_create(const void *p_platform_data) override final;

public:
	struct WindowPlatformData {
		struct wl_display *display;
		struct wl_surface *surface;
	};

	RenderingContextDriverVulkanWayland();
	~RenderingContextDriverVulkanWayland();
};

#endif // VULKAN_ENABLED
