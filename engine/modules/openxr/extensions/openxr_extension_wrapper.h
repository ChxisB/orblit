/**************************************************************************/
/*  openxr_extension_wrapper.h                                            */

#pragma once

#include "../action_map/openxr_interaction_profile_metadata.h"

#include "core/error/error_macros.h"
#include "core/math/projection.h"
#include "core/object/orblit_virtual.gen.h"
#include "core/object/ref_counted.h"
#include "core/containers/hash_map.h"
#include "core/containers/rid.h"
#include "core/variant/native_ptr.h"
#include "core/variant/typed_array.h"
#include "core/variant/variant.h"

#include <openxr/openxr.h>

class OpenXRAPI;
class OpenXRAPIExtension;
class OpenXRActionMap;

// `OpenXRExtensionWrapper` allows us to implement OpenXR extensions.
class OpenXRExtensionWrapper : public Object {
	ORBLIT_CLASS(OpenXRExtensionWrapper, Object);

	Ref<OpenXRAPIExtension> _native_extension_get_openxr_api();
	void _native_extension_register_extension_wrapper();

protected:
	static void register_members();

	Ref<OpenXRAPIExtension> openxr_api_extension;

public:
	// `get_requested_extensions` should return a list of OpenXR extensions related to this extension.
	// This function can be called multiple times.
	// p_xr_version is the OpenXR version which we're attempting to initialize.
	// Note: when called from the editor, p_xr_version will be 0 and we should return any extension we may initialize.
	// If the bool * is a nullptr this extension is mandatory
	// If the bool * points to a boolean, the boolean will be updated
	// to true if the extension is enabled.
	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_xr_version);

	ORBLIT_VIRTUAL1R(Dictionary, _get_requested_extensions, uint64_t);

	// These functions allow an extension to add entries to a struct chain.
	// `p_next_pointer` points to the last struct that was created for this chain
	// and should be used as the value for the `pNext` pointer in the first struct you add.
	// You should return the pointer to the last struct you define as your result.
	// If you are not adding any structs, just return `p_next_pointer`.
	// See existing extensions for examples of this implementation.
	virtual void *set_system_properties_and_get_next_pointer(void *p_next_pointer); // Add additional data structures when we interrogate OpenXR's system abilities.
	virtual void *set_instance_create_info_and_get_next_pointer(XrVersion p_xr_version, void *p_next_pointer); // Add additional data structures when we create our OpenXR instance.
	virtual void *set_session_create_and_get_next_pointer(void *p_next_pointer); // Add additional data structures when we create our OpenXR session.
	virtual void *set_spatial_container_create_info_and_get_next_pointer(RID p_spatial_container, void *p_next_pointer); // Add additional data structures when we create a spatial container.
	virtual void *set_swapchain_create_info_and_get_next_pointer(void *p_next_pointer); // Add additional data structures when creating OpenXR swap chains.
	virtual void *set_hand_joint_locations_and_get_next_pointer(int p_hand_index, void *p_next_pointer);
	virtual void *set_projection_views_and_get_next_pointer(int p_view_index, void *p_next_pointer);
	virtual void *set_reference_space_create_info_and_get_next_pointer(int p_reference_space_type, void *p_next_pointer);
	// These will only be called for extensions registered via OpenXRApi::register_frame_info_extension().
	virtual void *set_frame_wait_info_and_get_next_pointer(void *p_next_pointer); // Add additional data structures when calling xrWaitFrame
	virtual void *set_view_locate_info_and_get_next_pointer(void *p_next_pointer); // Add additional data structures when calling xrLocateViews
	virtual void *set_spatial_container_views_locate_info_and_get_next_pointer(RID p_spatial_container, void *p_next_pointer); // Add additional data structures when calling xrLocateSpatialContainerViewsEXT
	virtual void *set_frame_end_info_and_get_next_pointer(void *p_next_pointer); // Add additional data structures when calling xrEndFrame
	// This will only be called for extensions registered via OpenXRAPI::register_projection_layer_extension().
	virtual void *set_projection_layer_and_get_next_pointer(void *p_next_pointer); // Add additional data structures to XrCompositionLayerProjection

	virtual void prepare_view_configuration(uint32_t p_view_count);
	virtual void *set_view_configuration_and_get_next_pointer(uint32_t p_view, void *p_next_pointer); // Add additional data structures when calling xrEnumerateViewConfiguration
	virtual void print_view_configuration_info(uint32_t p_view) const;

	virtual TypedArray<Projection> get_camera_projections(const StringName &p_tracker_name, double p_aspect, double p_z_near, double p_z_far);
	virtual TypedArray<Transform3D> get_camera_offsets(const StringName &p_tracker_name);

	//TODO workaround as NativeExtensionPtr<void> return type results in build error in orblit-cpp
	ORBLIT_VIRTUAL1R(uint64_t, _set_system_properties_and_get_next_pointer, NativeExtensionPtr<void>);
	ORBLIT_VIRTUAL2R(uint64_t, _set_instance_create_info_and_get_next_pointer, uint64_t, NativeExtensionPtr<void>);
	ORBLIT_VIRTUAL1R(uint64_t, _set_session_create_and_get_next_pointer, NativeExtensionPtr<void>);
	ORBLIT_VIRTUAL1R(uint64_t, _set_swapchain_create_info_and_get_next_pointer, NativeExtensionPtr<void>);
	ORBLIT_VIRTUAL2R(uint64_t, _set_hand_joint_locations_and_get_next_pointer, int, NativeExtensionPtr<void>);
	ORBLIT_VIRTUAL2R(uint64_t, _set_projection_views_and_get_next_pointer, int, NativeExtensionPtr<void>);
	ORBLIT_VIRTUAL1R(uint64_t, _set_frame_wait_info_and_get_next_pointer, NativeExtensionPtr<void>);
	ORBLIT_VIRTUAL1R(uint64_t, _set_frame_end_info_and_get_next_pointer, NativeExtensionPtr<void>);
	ORBLIT_VIRTUAL1R(uint64_t, _set_projection_layer_and_get_next_pointer, NativeExtensionPtr<void>);
	ORBLIT_VIRTUAL1R(uint64_t, _set_view_locate_info_and_get_next_pointer, NativeExtensionPtr<void>);
	ORBLIT_VIRTUAL2R(uint64_t, _set_reference_space_create_info_and_get_next_pointer, int, NativeExtensionPtr<void>);
	ORBLIT_VIRTUAL2R(uint64_t, _set_spatial_container_create_info_and_get_next_pointer, RID, NativeExtensionPtr<void>);
	ORBLIT_VIRTUAL2R(uint64_t, _set_spatial_container_views_locate_info_and_get_next_pointer, RID, NativeExtensionPtr<void>);
	ORBLIT_VIRTUAL0R(int, _get_composition_layer_count);
	ORBLIT_VIRTUAL1R(uint64_t, _get_composition_layer, int);
	ORBLIT_VIRTUAL1R(int, _get_composition_layer_order, int);
	ORBLIT_VIRTUAL1(_prepare_view_configuration, int);
	ORBLIT_VIRTUAL2R(uint64_t, _set_view_configuration_and_get_next_pointer, uint32_t, NativeExtensionPtr<void>);
	ORBLIT_VIRTUAL1C(_print_view_configuration_info, int);

	ORBLIT_VIRTUAL4R(TypedArray<Projection>, _get_camera_projections, const StringName &, double, double, double);
	ORBLIT_VIRTUAL1R(TypedArray<Transform3D>, _get_camera_offsets, const StringName &);

	virtual PackedStringArray get_suggested_tracker_names();

	ORBLIT_VIRTUAL0R(PackedStringArray, _get_suggested_tracker_names);

	// `on_register_metadata` allows extensions to register additional controller metadata.
	// This function is called even when OpenXRApi is not constructured as the metadata
	// needs to be available to the editor.
	// Also extensions should provide metadata regardless of whether they are supported
	// on the host system as the controller data is used to setup action maps for users
	// who may have access to the relevant hardware.
	virtual void on_register_metadata(OpenXRInteractionProfileMetadata *p_interaction_profile_metadata);

	virtual void on_before_instance_created(); // `on_before_instance_created` is called before we create our OpenXR instance.
	virtual void on_instance_created(const XrInstance p_instance); // `on_instance_created` is called right after we've successfully created our OpenXR instance.
	virtual void on_instance_destroyed(); // `on_instance_destroyed` is called right before we destroy our OpenXR instance.
	virtual void on_session_created(const XrSession p_session); // `on_session_created` is called right after we've successfully created our OpenXR session.
	virtual void on_session_destroyed(); // `on_session_destroyed` is called right before we destroy our OpenXR session.

	// `on_process` is called as part of our OpenXR process handling,
	// this happens right before physics process and normal processing is run.
	// This is when controller data is queried and made available to game logic.
	virtual void on_process();
	virtual void on_sync_actions(); // `on_sync_actions` is called right after we sync our action sets.
	virtual void on_pre_render(); // `on_pre_render` is called right before we start rendering our XR viewports.
	virtual void on_main_swapchains_created(); // `on_main_swapchains_created` is called right after our main swapchains are (re)created.
	virtual void on_pre_draw_viewport(RID p_render_target); // `on_pre_draw_viewport` is called right before we start rendering this viewport
	virtual void on_post_draw_viewport(RID p_render_target); // `on_port_draw_viewport` is called right after we start rendering this viewport (note that on Vulkan draw commands may only be queued)
	virtual void on_post_render() {} // `on_post_render` is called right before we submit our XR viewports to the XR compositor.

	ORBLIT_VIRTUAL1(_on_register_metadata, OpenXRInteractionProfileMetadata *);
	ORBLIT_VIRTUAL0(_on_before_instance_created);
	ORBLIT_VIRTUAL1(_on_instance_created, uint64_t);
	ORBLIT_VIRTUAL0(_on_instance_destroyed);
	ORBLIT_VIRTUAL1(_on_session_created, uint64_t);
	ORBLIT_VIRTUAL0(_on_process);
	ORBLIT_VIRTUAL0(_on_sync_actions);
	ORBLIT_VIRTUAL0(_on_pre_render);
	ORBLIT_VIRTUAL0(_on_main_swapchains_created);
	ORBLIT_VIRTUAL0(_on_session_destroyed);
	ORBLIT_VIRTUAL1(_on_pre_draw_viewport, RID);
	ORBLIT_VIRTUAL1(_on_post_draw_viewport, RID);

	virtual void on_state_idle(); // `on_state_idle` is called when the OpenXR session state is changed to idle.
	virtual void on_state_ready(); // `on_state_ready` is called when the OpenXR session state is changed to ready, this means OpenXR is ready to setup our session.
	virtual void on_state_synchronized(); // `on_state_synchronized` is called when the OpenXR session state is changed to synchronized, note that OpenXR also returns to this state when our application looses focus.
	virtual void on_state_visible(); // `on_state_visible` is called when the OpenXR session state is changed to visible, OpenXR is now ready to receive frames.
	virtual void on_state_focused(); // `on_state_focused` is called when the OpenXR session state is changed to focused, this state is the active state when our game runs.
	virtual void on_state_stopping(); // `on_state_stopping` is called when the OpenXR session state is changed to stopping.
	virtual void on_state_loss_pending(); // `on_state_loss_pending` is called when the OpenXR session state is changed to loss pending.
	virtual void on_state_exiting(); // `on_state_exiting` is called when the OpenXR session state is changed to exiting.

	ORBLIT_VIRTUAL0(_on_state_idle);
	ORBLIT_VIRTUAL0(_on_state_ready);
	ORBLIT_VIRTUAL0(_on_state_synchronized);
	ORBLIT_VIRTUAL0(_on_state_visible);
	ORBLIT_VIRTUAL0(_on_state_focused);
	ORBLIT_VIRTUAL0(_on_state_stopping);
	ORBLIT_VIRTUAL0(_on_state_loss_pending);
	ORBLIT_VIRTUAL0(_on_state_exiting);

	// These will only be called on extensions registered via OpenXRAPI::register_composition_layer_provider().
	virtual int get_composition_layer_count();
	virtual XrCompositionLayerBaseHeader *get_composition_layer(int p_index);
	virtual int get_composition_layer_order(int p_index);

	virtual void *set_viewport_composition_layer_and_get_next_pointer(const XrCompositionLayerBaseHeader *p_layer, const Dictionary &p_property_values, void *p_next_pointer); // Add additional data structures to composition layers created via OpenXRCompositionLayer.
	virtual void on_viewport_composition_layer_destroyed(const XrCompositionLayerBaseHeader *p_layer); // `on_viewport_composition_layer_destroyed` is called when a composition layer created via OpenXRCompositionLayer is destroyed.
	virtual void get_viewport_composition_layer_extension_properties(List<PropertyInfo> *p_property_list); // Get additional property definitions for OpenXRCompositionLayer.
	virtual Dictionary get_viewport_composition_layer_extension_property_defaults(); // Get the default values for the additional property definitions for OpenXRCompositionLayer.
	virtual void *set_android_surface_swapchain_create_info_and_get_next_pointer(const Dictionary &p_property_values, void *p_next_pointer);

	ORBLIT_VIRTUAL3R(uint64_t, _set_viewport_composition_layer_and_get_next_pointer, NativeExtensionPtr<const void>, Dictionary, NativeExtensionPtr<void>);
	ORBLIT_VIRTUAL1(_on_viewport_composition_layer_destroyed, NativeExtensionPtr<const void>);
	ORBLIT_VIRTUAL0R(TypedArray<Dictionary>, _get_viewport_composition_layer_extension_properties);
	ORBLIT_VIRTUAL0R(Dictionary, _get_viewport_composition_layer_extension_property_defaults);
	ORBLIT_VIRTUAL2R(uint64_t, _set_android_surface_swapchain_create_info_and_get_next_pointer, Dictionary, NativeExtensionPtr<void>);

	// `on_event_polled` is called when there is an OpenXR event to process.
	// Should return true if the event was handled, false otherwise.
	virtual bool on_event_polled(const XrEventDataBuffer &event);

	ORBLIT_VIRTUAL1R(bool, _on_event_polled, NativeExtensionPtr<const void>);

	OpenXRExtensionWrapper();
	virtual ~OpenXRExtensionWrapper() override;
};

// `OpenXRGraphicsExtensionWrapper` implements specific logic for each supported graphics API.
class OpenXRGraphicsExtensionWrapper : public OpenXRExtensionWrapper {
public:
	virtual void get_usable_swapchain_formats(Vector<int64_t> &p_usable_swap_chains) = 0; // `get_usable_swapchain_formats` should return a list of usable color formats.
	virtual void get_usable_depth_formats(Vector<int64_t> &p_usable_swap_chains) = 0; // `get_usable_depth_formats` should return a list of usable depth formats.
	virtual String get_swapchain_format_name(int64_t p_swapchain_format) const = 0; // `get_swapchain_format_name` should return the constant name of a given format.
	virtual bool get_swapchain_image_data(XrSwapchain p_swapchain, int64_t p_swapchain_format, uint32_t p_width, uint32_t p_height, uint32_t p_sample_count, uint32_t p_array_size, void **r_swapchain_graphics_data) = 0; // `get_swapchain_image_data` extracts image IDs for the swapchain images and stores there in an implementation dependent data structure.
	virtual void cleanup_swapchain_graphics_data(void **p_swapchain_graphics_data) = 0; // `cleanup_swapchain_graphics_data` cleans up the data held in our implementation dependent data structure and should free up its memory.
	virtual bool create_projection_fov(const XrFovf p_fov, double p_z_near, double p_z_far, Projection &r_camera_matrix) = 0; // `create_projection_fov` creates a proper projection matrix based on asymmetric FOV data provided by OpenXR.
	virtual RID get_texture(void *p_swapchain_graphics_data, int p_image_index) = 0; // `get_texture` returns a Orblit texture RID for the current active texture in our swapchain.
	virtual RID get_density_map(void *p_swapchain_graphics_data, int p_image_index) = 0; // `get_density_map` returns a Orblit texture RID for the current active density map in our swapchain (if any).
};
