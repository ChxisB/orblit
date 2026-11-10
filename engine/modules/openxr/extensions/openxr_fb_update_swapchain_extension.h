/**************************************************************************/
/*  openxr_fb_update_swapchain_extension.h                                */

#pragma once

// This extension implements the FB update swapchain extension.
// This is an extension Meta added to further configure the swapchain.
// Other Android based devices are implementing this as well, see:
// https://github.khronos.org/OpenXR-Inventory/extension_support.html#XR_FB_swapchain_update_state

#include "../util.h"
#include "openxr_composition_layer_extension.h"
#include "openxr_extension_wrapper.h"

class OpenXRFBUpdateSwapchainExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRFBUpdateSwapchainExtension, OpenXRExtensionWrapper);

protected:
	static void register_members() {}

	friend class OpenXRFBFoveationExtension;

public:
	static OpenXRFBUpdateSwapchainExtension *get_singleton();

	OpenXRFBUpdateSwapchainExtension(const String &p_rendering_driver);
	virtual ~OpenXRFBUpdateSwapchainExtension() override;

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;

	virtual void on_instance_created(const XrInstance p_instance) override;
	virtual void on_instance_destroyed() override;

	bool is_enabled() const;
	bool is_android_ext_enabled() const;

	void update_swapchain_state(XrSwapchain p_swapchain, const OpenXRCompositionLayerExtension::SwapchainState *p_swapchain_state);

	void update_swapchain_surface_size(XrSwapchain p_swapchain, const Size2i &p_size);

private:
	static OpenXRFBUpdateSwapchainExtension *singleton;

	// Setup
	String rendering_driver;
	bool fb_swapchain_update_state_ext = false;
	bool fb_swapchain_update_state_vulkan_ext = false;
	bool fb_swapchain_update_state_opengles_ext = false;
	bool fb_swapchain_update_state_android_ext = false;

	uint32_t filter_to_gl(OpenXRCompositionLayerExtension::Filter p_filter, OpenXRCompositionLayerExtension::MipmapMode p_mipmap_mode = OpenXRCompositionLayerExtension::MipmapMode::MIPMAP_MODE_DISABLED);
	uint32_t wrap_to_gl(OpenXRCompositionLayerExtension::Wrap p_wrap);
	uint32_t swizzle_to_gl(OpenXRCompositionLayerExtension::Swizzle p_swizzle);

	uint32_t filter_to_vk(OpenXRCompositionLayerExtension::Filter p_filter);
	uint32_t mipmap_mode_to_vk(OpenXRCompositionLayerExtension::MipmapMode p_mipmap);
	uint32_t wrap_to_vk(OpenXRCompositionLayerExtension::Wrap p_wrap);
	uint32_t swizzle_to_vk(OpenXRCompositionLayerExtension::Swizzle p_swizzle);

	// OpenXR API call wrappers
	EXT_PROTO_XRRESULT_FUNC2(xrUpdateSwapchainFB, (XrSwapchain), swapchain, (const XrSwapchainStateBaseHeaderFB *), state);
	EXT_PROTO_XRRESULT_FUNC2(xrGetSwapchainStateFB, (XrSwapchain), swapchain, (XrSwapchainStateBaseHeaderFB *), state);
};
