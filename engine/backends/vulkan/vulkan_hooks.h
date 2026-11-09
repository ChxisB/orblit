/**************************************************************************/
/*  vulkan_hooks.h                                                        */

#pragma once

#include "core/containers/local_vector.h"
#include "core/math/vector2i.h"

#include <backends/vulkan/vulkan.h>

class VulkanHooks {
private:
	static VulkanHooks *singleton;

public:
	VulkanHooks();
	virtual ~VulkanHooks();
	virtual bool create_vulkan_instance(const VkInstanceCreateInfo *p_vulkan_create_info, VkInstance *r_instance) = 0;
	virtual bool get_physical_device(VkPhysicalDevice *r_device) = 0;
	virtual bool create_vulkan_device(const VkDeviceCreateInfo *p_device_create_info, VkDevice *r_device) = 0;
	virtual void set_direct_queue_family_and_index(uint32_t p_queue_family_index, uint32_t p_queue_index) = 0;
	virtual bool use_fragment_density_offsets() = 0;
	virtual void get_fragment_density_offsets(LocalVector<VkOffset2D> &r_offsets, const Vector2i &p_granularity) = 0;
	virtual bool use_subsampled_images() = 0;
	static VulkanHooks *get_singleton() { return singleton; }
};
