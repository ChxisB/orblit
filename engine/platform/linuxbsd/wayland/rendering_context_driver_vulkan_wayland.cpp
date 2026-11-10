/**************************************************************************/
/*  rendering_context_driver_vulkan_wayland.cpp                           */

#ifdef VULKAN_ENABLED

#include "rendering_context_driver_vulkan_wayland.h"

#include <backends/vulkan/vulkan.h>

const char *RenderingContextDriverVulkanWayland::_get_platform_surface_extension() const {
	return VK_KHR_WAYLAND_SURFACE_EXTENSION_NAME;
}

RenderingContextDriver::SurfaceID RenderingContextDriverVulkanWayland::surface_create(const void *p_platform_data) {
	const WindowPlatformData *wpd = (const WindowPlatformData *)(p_platform_data);

	VkWaylandSurfaceCreateInfoKHR create_info = {};
	create_info.sType = VK_STRUCTURE_TYPE_WAYLAND_SURFACE_CREATE_INFO_KHR;
	create_info.display = wpd->display;
	create_info.surface = wpd->surface;

	VkSurfaceKHR vk_surface = VK_NULL_HANDLE;
	VkResult err = vkCreateWaylandSurfaceKHR(instance_get(), &create_info, get_allocation_callbacks(VK_OBJECT_TYPE_SURFACE_KHR), &vk_surface);
	ERR_FAIL_COND_V(err != VK_SUCCESS, SurfaceID());

	Surface *surface = memnew(Surface);
	surface->vk_surface = vk_surface;
	return SurfaceID(surface);
}

RenderingContextDriverVulkanWayland::RenderingContextDriverVulkanWayland() {
	// Does nothing.
}

RenderingContextDriverVulkanWayland::~RenderingContextDriverVulkanWayland() {
	// Does nothing.
}

#endif // VULKAN_ENABLED
