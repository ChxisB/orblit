/**************************************************************************/
/*  vulkan_hooks.cpp                                                      */

#include "vulkan_hooks.h"

VulkanHooks *VulkanHooks::singleton = nullptr;

VulkanHooks::VulkanHooks() {
	if (singleton == nullptr) {
		singleton = this;
	}
}

VulkanHooks::~VulkanHooks() {
	if (singleton == this) {
		singleton = nullptr;
	}
}
