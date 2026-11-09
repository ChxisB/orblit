/**************************************************************************/
/*  openxr_frame_synthesis_extension.h                                    */

#pragma once

#include "../openxr_api.h"
#include "openxr_extension_wrapper.h"

#include <openxr/openxr.h>

class OpenXRFrameSynthesisExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRFrameSynthesisExtension, OpenXRExtensionWrapper);

public:
	static OpenXRFrameSynthesisExtension *get_singleton();

	OpenXRFrameSynthesisExtension();
	virtual ~OpenXRFrameSynthesisExtension() override;

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;

	virtual void on_instance_created(const XrInstance p_instance) override;
	virtual void on_instance_destroyed() override;

	virtual void prepare_view_configuration(uint32_t p_view_count) override;
	virtual void *set_view_configuration_and_get_next_pointer(uint32_t p_view, void *p_next_pointer) override;
	virtual void print_view_configuration_info(uint32_t p_view) const override;

	virtual void on_session_destroyed() override;

	virtual void on_main_swapchains_created() override;
	virtual void on_pre_draw_viewport(RID p_render_target) override;
	virtual void on_post_draw_viewport(RID p_render_target) override;
	virtual void *set_projection_views_and_get_next_pointer(int p_view_index, void *p_next_pointer) override;

	bool is_available() const;

	bool is_enabled() const;
	void set_enabled(bool p_enabled);

	bool get_relax_frame_interval() const;
	void set_relax_frame_interval(bool p_relax_frame_interval);

	void skip_next_frame();

protected:
	static void register_members();

	void _set_render_state_enabled_rt(bool p_enabled);
	void _set_relax_frame_interval_rt(bool p_relax_frame_interval);
	void _set_skip_next_frame_rt();

private:
	enum SwapchainTypes {
		SWAPCHAIN_MOTION_VECTOR,
		SWAPCHAIN_DEPTH,
		SWAPCHAIN_MAX
	};
	void free_swapchains();

	static OpenXRFrameSynthesisExtension *singleton;

	bool frame_synthesis_ext = false;
	bool enabled = true;
	bool relax_frame_interval = false;

	// Frame synthesis render state, only accessible on render thread
	struct RenderState {
		bool enabled = true;
		bool relax_frame_interval = false;
		bool skip_next_frame = false;

		LocalVector<XrFrameSynthesisConfigViewEXT> config_views;
		OpenXRAPI::OpenXRSwapChainInfo swapchains[SWAPCHAIN_MAX];
		LocalVector<XrFrameSynthesisInfoEXT> frame_synthesis_info;
		Transform3D previous_transform;
	} render_state;
};
