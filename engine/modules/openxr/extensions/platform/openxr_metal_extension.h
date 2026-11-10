/**************************************************************************/
/*  openxr_metal_extension.h                                              */

#pragma once

#include "../../util.h"
#include "../openxr_extension_wrapper.h"

#include "core/containers/vector.h"

// Always include this as late as possible.
#include "../../openxr_platform_inc.h" // IWYU pragma: keep.

class OpenXRMetalExtension : public OpenXRGraphicsExtensionWrapper {
public:
	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;

	virtual void on_instance_created(const XrInstance p_instance) override;
	virtual void *set_session_create_and_get_next_pointer(void *p_next_pointer) override;

	virtual void get_usable_swapchain_formats(Vector<int64_t> &p_usable_swap_chains) override;
	virtual void get_usable_depth_formats(Vector<int64_t> &p_usable_swap_chains) override;
	virtual String get_swapchain_format_name(int64_t p_swapchain_format) const override;
	virtual bool get_swapchain_image_data(XrSwapchain p_swapchain, int64_t p_swapchain_format, uint32_t p_width, uint32_t p_height, uint32_t p_sample_count, uint32_t p_array_size, void **r_swapchain_graphics_data) override;
	virtual void cleanup_swapchain_graphics_data(void **p_swapchain_graphics_data) override;
	virtual bool create_projection_fov(const XrFovf p_fov, double p_z_near, double p_z_far, Projection &r_camera_matrix) override;
	virtual RID get_texture(void *p_swapchain_graphics_data, int p_image_index) override;
	virtual RID get_density_map(void *p_swapchain_graphics_data, int p_image_index) override { return RID(); }

private:
	static XrGraphicsBindingMetalKHR graphics_binding_metal;

	struct SwapchainGraphicsData {
		bool is_multiview;
		Vector<RID> texture_rids;
	};

	bool check_graphics_api_support();

	EXT_PROTO_XRRESULT_FUNC3(xrGetMetalGraphicsRequirementsKHR, (XrInstance), p_instance, (XrSystemId), p_system_id, (XrGraphicsRequirementsMetalKHR *), p_graphics_requirements)
	EXT_PROTO_XRRESULT_FUNC4(xrEnumerateSwapchainImages, (XrSwapchain), p_swapchain, (uint32_t), p_image_capacity_input, (uint32_t *), p_image_count_output, (XrSwapchainImageBaseHeader *), p_images)
};
