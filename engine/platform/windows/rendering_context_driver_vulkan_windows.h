/**************************************************************************/
/*  rendering_context_driver_vulkan_windows.h                             */

#pragma once

#ifdef VULKAN_ENABLED

#include "backends/vulkan/rendering_context_driver_vulkan.h"

#include <windows.h>

class RenderingContextDriverVulkanWindows : public RenderingContextDriverVulkan {
private:
	const char *_get_platform_surface_extension() const override final;

protected:
	SurfaceID surface_create(const void *p_platform_data) override final;

public:
	struct WindowPlatformData {
		HWND window;
		HINSTANCE instance;
	};

	RenderingContextDriverVulkanWindows();
	~RenderingContextDriverVulkanWindows() override;
};

#endif // VULKAN_ENABLED
