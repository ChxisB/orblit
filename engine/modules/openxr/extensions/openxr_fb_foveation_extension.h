/**************************************************************************/
/*  openxr_fb_foveation_extension.h                                       */

#pragma once

// This extension implements the FB Foveation extension.
// This is an extension Meta added due to VRS being unavailable on Android.
// Other Android based devices are implementing this as well, see:
// https://github.khronos.org/OpenXR-Inventory/extension_support.html#XR_FB_foveation

#include "../util.h"
#include "openxr_extension_wrapper.h"
#include "openxr_fb_update_swapchain_extension.h"

// Always include this as late as possible.
#include "../openxr_platform_inc.h" // IWYU pragma: keep.

class OpenXRFBFoveationExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRFBFoveationExtension, OpenXRExtensionWrapper);

protected:
	static void register_members() {}

public:
	static OpenXRFBFoveationExtension *get_singleton();

	OpenXRFBFoveationExtension(const String &p_rendering_driver);
	virtual ~OpenXRFBFoveationExtension() override;

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;

	virtual void on_instance_created(const XrInstance p_instance) override;
	virtual void on_instance_destroyed() override;

	virtual void *set_system_properties_and_get_next_pointer(void *p_next_pointer) override;
	virtual void *set_swapchain_create_info_and_get_next_pointer(void *p_next_pointer) override;

	virtual void on_main_swapchains_created() override;

	bool is_enabled() const;

	XrFoveationLevelFB get_foveation_level() const;
	void set_foveation_level(XrFoveationLevelFB p_foveation_level);

	XrFoveationDynamicFB get_foveation_dynamic() const;
	void set_foveation_dynamic(XrFoveationDynamicFB p_foveation_dynamic);

	bool is_foveation_eye_tracked_enabled() const;
	void get_fragment_density_offsets(LocalVector<Vector2i> &r_offsets);

	void set_foveation_with_subsampled_images_enabled(bool p_enabled);
	bool is_foveation_with_subsampled_images_enabled() const;

	void set_foveation_with_subsampled_images_active(bool p_active);

private:
	static OpenXRFBFoveationExtension *singleton;

	// Setup
	String rendering_driver;
	bool fb_foveation_ext = false;
	bool fb_foveation_configuration_ext = false;
	bool fb_foveation_vulkan_ext = false;
	bool meta_foveation_eye_tracked_ext = false;
	bool meta_vulkan_swapchain_create_info_ext = false;

	// Configuration
	XrFoveationLevelFB foveation_level = XR_FOVEATION_LEVEL_NONE_FB;
	XrFoveationDynamicFB foveation_dynamic = XR_FOVEATION_DYNAMIC_DISABLED_FB;
	bool foveation_with_subsampled_images_enabled = false;
	bool foveation_with_subsampled_images_active = false;

	void _update_profile_rt();

	void update_profile();

	// Enable foveation on this swapchain
	XrSwapchainCreateInfoFoveationFB swapchain_create_info_foveation_fb;
	OpenXRFBUpdateSwapchainExtension *swapchain_update_state_ext = nullptr;

	// Enable eye tracked foveation
	XrSystemFoveationEyeTrackedPropertiesMETA meta_foveation_eye_tracked_properties;
	XrFoveationEyeTrackedProfileCreateInfoMETA meta_foveation_eye_tracked_create_info;
#ifdef VULKAN_ENABLED
	XrVulkanSwapchainCreateInfoMETA meta_vulkan_swapchain_create_info;
#endif

	// OpenXR API call wrappers
	EXT_PROTO_XRRESULT_FUNC3(xrCreateFoveationProfileFB, (XrSession), session, (const XrFoveationProfileCreateInfoFB *), create_info, (XrFoveationProfileFB *), profile);
	EXT_PROTO_XRRESULT_FUNC1(xrDestroyFoveationProfileFB, (XrFoveationProfileFB), profile);
	EXT_PROTO_XRRESULT_FUNC2(xrGetFoveationEyeTrackedStateMETA, (XrSession), session, (XrFoveationEyeTrackedStateMETA *), foveationState);
};
