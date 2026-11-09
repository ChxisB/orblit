/**************************************************************************/
/*  rendering_context_driver_vulkan_x11.cpp                               */

#ifdef VULKAN_ENABLED

#include "rendering_context_driver_vulkan_x11.h"

#include <backends/vulkan/vulkan.h>

const char *RenderingContextDriverVulkanX11::_get_platform_surface_extension() const {
	return VK_KHR_XLIB_SURFACE_EXTENSION_NAME;
}

RenderingContextDriver::SurfaceID RenderingContextDriverVulkanX11::surface_create(const void *p_platform_data) {
	const WindowPlatformData *wpd = (const WindowPlatformData *)(p_platform_data);

	VkXlibSurfaceCreateInfoKHR create_info = {};
	create_info.sType = VK_STRUCTURE_TYPE_XLIB_SURFACE_CREATE_INFO_KHR;
	create_info.dpy = wpd->display;
	create_info.window = wpd->window;

	VkSurfaceKHR vk_surface = VK_NULL_HANDLE;
	VkResult err = vkCreateXlibSurfaceKHR(instance_get(), &create_info, get_allocation_callbacks(VK_OBJECT_TYPE_SURFACE_KHR), &vk_surface);
	ERR_FAIL_COND_V(err != VK_SUCCESS, SurfaceID());

	Surface *surface = memnew(Surface);
	surface->vk_surface = vk_surface;
	return SurfaceID(surface);
}

RenderingContextDriverVulkanX11::RenderingContextDriverVulkanX11() {
	// Does nothing.
}

RenderingContextDriverVulkanX11::~RenderingContextDriverVulkanX11() {
	// Does nothing.
}

#endif // VULKAN_ENABLED
