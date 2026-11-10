/**************************************************************************/
/*  openxr_spatial_container_self_rendering_extension.h                   */

#pragma once

#include "../../openxr_util.h"
#include "../../util.h"
#include "../openxr_extension_wrapper.h"
#include "openxr_spatial_container_extension.h"
#include "openxr_spatial_container_state.h"

#include <openxr/openxr.h>

// Spatial container self rendering extension
class OpenXRSpatialContainerSelfRenderingExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRSpatialContainerSelfRenderingExtension, OpenXRExtensionWrapper);

public:
	static OpenXRSpatialContainerSelfRenderingExtension *get_singleton();

	OpenXRSpatialContainerSelfRenderingExtension();
	virtual ~OpenXRSpatialContainerSelfRenderingExtension() override;

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;

	virtual void on_instance_created(const XrInstance p_instance) override;
	virtual void on_session_created(const XrSession p_session) override;
	virtual void on_session_destroyed() override;
	virtual void on_process() override;

	virtual void *set_frame_end_info_and_get_next_pointer(void *p_next_pointer) override;
	virtual void *set_projection_layer_and_get_next_pointer(void *p_next_pointer) override;

	bool is_enabled() const;

	// Spatial container rendering mechanism APIs.
	void on_spatial_container_created(const OpenXRSpatialContainerExtension::SpatialContainerData &p_container_data);
	void on_spatial_container_visibility_changed(const OpenXRSpatialContainerExtension::SpatialContainerData &p_container, bool p_visible);
	void on_spatial_container_closed(const OpenXRSpatialContainerExtension::SpatialContainerData &p_container);
	XrSpatialContainerGraphicsPresentationEXT get_spatial_container_graphics_presentation() const;
	XrResult locate_spatial_container_views(RID p_spatial_container_rid, XrSpace p_space, XrView *p_views, bool &r_view_pose_valid);

protected:
	static void register_members();

private:
	// OpenXR API call wrappers
	EXT_PROTO_XRRESULT_FUNC2(xrBeginSpatialContainerRenderingEXT,
			(XrSession), session,
			(const XrSpatialContainerBeginInfoEXT *), begin_info);
	EXT_PROTO_XRRESULT_FUNC2(xrEndSpatialContainerRenderingEXT,
			(XrSession), session,
			(const XrSpatialContainerEndInfoEXT *), end_info);
	EXT_PROTO_XRRESULT_FUNC6(xrLocateSpatialContainerViewsEXT,
			(XrSession), session,
			(const XrSpatialContainerViewsLocateInfoEXT *), locate_info,
			(uint32_t), view_state_count,
			(XrSpatialContainerViewStateEXT *), view_states,
			(uint32_t), view_count,
			(XrView *), views);

	void _add_spatial_container_to_render_set(const OpenXRSpatialContainerExtension::SpatialContainerData &p_container_data);
	void _remove_spatial_container_from_render_set(const XrSpatialContainerEXT &p_container);
	void _cleanup();

	bool _initialize_spatial_container_self_rendering_extension(const XrInstance &p_instance);

	static OpenXRSpatialContainerSelfRenderingExtension *singleton;

	bool spatial_container_self_rendering_ext = false;

	XrSpatialContainerEXT active_spatial_container = XR_NULL_HANDLE;
	XrSpatialContainerViewStateEXT active_view_state = {
		XR_TYPE_SPATIAL_CONTAINER_VIEW_STATE_EXT, // type
		nullptr, // next
		0, // viewStateFlags
		XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO, // viewConfigurationType
		false, // shouldSubmitLayers
		{ 0, 0 }, // recommendedImageExtent
	};
	XrSpatialContainerLayerEXT active_layer = {
		XR_TYPE_SPATIAL_CONTAINER_LAYER_EXT, // type
		nullptr, // next
		XR_NULL_HANDLE, // spatialContainer
		false, // retainPreviousSubmission
		0, // layerCount
		nullptr, // layers
	};

	XrSpatialContainerLayerFrameEndInfoEXT frame_end_info = {
		XR_TYPE_SPATIAL_CONTAINER_LAYER_FRAME_END_INFO_EXT, // type
		nullptr, // next
		0, // containerLayerCount
		nullptr // containerLayers
	};

	XrSpatialContainerCompositionLayerViewConfigurationEXT container_view_config = {
		XR_TYPE_SPATIAL_CONTAINER_COMPOSITION_LAYER_VIEW_CONFIGURATION_EXT, // type
		nullptr, // next
		XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO, // viewConfigurationType
	};

	OpenXRSpatialContainerExtension::SpatialContainerData pending_container_data;
	bool has_pending_container = false;
	const XrCompositionLayerBaseHeader *submit_layers[1] = { nullptr };
};
