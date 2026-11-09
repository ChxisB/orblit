/**************************************************************************/
/*  rendering_device_enums.h                                              */

#pragma once

namespace RenderingDeviceEnums {

// This enum matches VkPhysicalDeviceType (except for `DEVICE_TYPE_MAX`).
// Unlike VkPhysicalDeviceType, DeviceType is exposed to the scripting API.
enum DeviceType {
	DEVICE_TYPE_OTHER,
	DEVICE_TYPE_INTEGRATED_GPU,
	DEVICE_TYPE_DISCRETE_GPU,
	DEVICE_TYPE_VIRTUAL_GPU,
	DEVICE_TYPE_CPU,
	DEVICE_TYPE_MAX
};

} //namespace RenderingDeviceEnums
