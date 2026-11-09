/**************************************************************************/
/*  openxr_extension_wrapper.cpp                                          */

#include "openxr_extension_wrapper.h"

#include "../openxr_api.h"
#include "../openxr_api_extension.h"

#include "core/object/class_registry.h"

void OpenXRExtensionWrapper::register_members() {
	ORBLIT_VIRTUAL_BIND(_get_requested_extensions, "xr_version");
	ORBLIT_VIRTUAL_BIND(_set_system_properties_and_get_next_pointer, "next_pointer");
	ORBLIT_VIRTUAL_BIND(_set_instance_create_info_and_get_next_pointer, "xr_version", "next_pointer");
	ORBLIT_VIRTUAL_BIND(_set_session_create_and_get_next_pointer, "next_pointer");
	ORBLIT_VIRTUAL_BIND(_set_swapchain_create_info_and_get_next_pointer, "next_pointer");
	ORBLIT_VIRTUAL_BIND(_set_hand_joint_locations_and_get_next_pointer, "hand_index", "next_pointer");
	ORBLIT_VIRTUAL_BIND(_set_projection_views_and_get_next_pointer, "view_index", "next_pointer");
	ORBLIT_VIRTUAL_BIND(_set_frame_wait_info_and_get_next_pointer, "next_pointer");
	ORBLIT_VIRTUAL_BIND(_set_frame_end_info_and_get_next_pointer, "next_pointer");
	ORBLIT_VIRTUAL_BIND(_set_projection_layer_and_get_next_pointer, "next_pointer");
	ORBLIT_VIRTUAL_BIND(_set_view_locate_info_and_get_next_pointer, "next_pointer");
	ORBLIT_VIRTUAL_BIND(_set_spatial_container_views_locate_info_and_get_next_pointer, "spatial_container", "next_pointer");
	ORBLIT_VIRTUAL_BIND(_set_reference_space_create_info_and_get_next_pointer, "reference_space_type", "next_pointer");
	ORBLIT_VIRTUAL_BIND(_set_spatial_container_create_info_and_get_next_pointer, "spatial_container", "next_pointer");
	ORBLIT_VIRTUAL_BIND(_prepare_view_configuration, "view_count");
	ORBLIT_VIRTUAL_BIND(_set_view_configuration_and_get_next_pointer, "view", "next_pointer");
	ORBLIT_VIRTUAL_BIND(_print_view_configuration_info, "view");
	ORBLIT_VIRTUAL_BIND(_get_composition_layer_count);
	ORBLIT_VIRTUAL_BIND(_get_composition_layer, "index");
	ORBLIT_VIRTUAL_BIND(_get_composition_layer_order, "index");
	ORBLIT_VIRTUAL_BIND(_get_suggested_tracker_names);
	ORBLIT_VIRTUAL_BIND(_on_register_metadata, "interaction_profile_metadata");
	ORBLIT_VIRTUAL_BIND(_on_before_instance_created);
	ORBLIT_VIRTUAL_BIND(_on_instance_created, "instance");
	ORBLIT_VIRTUAL_BIND(_on_instance_destroyed);
	ORBLIT_VIRTUAL_BIND(_on_session_created, "session");
	ORBLIT_VIRTUAL_BIND(_on_process);
	ORBLIT_VIRTUAL_BIND(_on_sync_actions);
	ORBLIT_VIRTUAL_BIND(_on_pre_render);
	ORBLIT_VIRTUAL_BIND(_on_main_swapchains_created);
	ORBLIT_VIRTUAL_BIND(_on_pre_draw_viewport, "viewport");
	ORBLIT_VIRTUAL_BIND(_on_post_draw_viewport, "viewport");
	ORBLIT_VIRTUAL_BIND(_on_session_destroyed);
	ORBLIT_VIRTUAL_BIND(_on_state_idle);
	ORBLIT_VIRTUAL_BIND(_on_state_ready);
	ORBLIT_VIRTUAL_BIND(_on_state_synchronized);
	ORBLIT_VIRTUAL_BIND(_on_state_visible);
	ORBLIT_VIRTUAL_BIND(_on_state_focused);
	ORBLIT_VIRTUAL_BIND(_on_state_stopping);
	ORBLIT_VIRTUAL_BIND(_on_state_loss_pending);
	ORBLIT_VIRTUAL_BIND(_on_state_exiting);
	ORBLIT_VIRTUAL_BIND(_on_event_polled, "event");
	ORBLIT_VIRTUAL_BIND(_set_viewport_composition_layer_and_get_next_pointer, "layer", "property_values", "next_pointer");
	ORBLIT_VIRTUAL_BIND(_get_viewport_composition_layer_extension_properties);
	ORBLIT_VIRTUAL_BIND(_get_viewport_composition_layer_extension_property_defaults);
	ORBLIT_VIRTUAL_BIND(_on_viewport_composition_layer_destroyed, "layer");
	ORBLIT_VIRTUAL_BIND(_set_android_surface_swapchain_create_info_and_get_next_pointer, "property_values", "next_pointer");

	ORBLIT_VIRTUAL_BIND(_get_camera_projections, "tracker_name", "aspect", "z_near", "z_far");
	ORBLIT_VIRTUAL_BIND(_get_camera_offsets, "tracker_name");

	ClassRegistry::bind_method(D_METHOD("get_openxr_api"), &OpenXRExtensionWrapper::_native_extension_get_openxr_api);
	ClassRegistry::bind_method(D_METHOD("register_extension_wrapper"), &OpenXRExtensionWrapper::_native_extension_register_extension_wrapper);
}

HashMap<String, bool *> OpenXRExtensionWrapper::get_requested_extensions(XrVersion p_xr_version) {
	Dictionary request_extension;

	if (ORBLIT_VIRTUAL_CALL(_get_requested_extensions, (uint64_t)p_xr_version, request_extension)) {
		HashMap<String, bool *> result;
		for (const KeyValue<Variant, Variant> &kv : request_extension) {
			NativeExtensionPtr<bool> value = VariantCaster<NativeExtensionPtr<bool>>::cast(kv.value);
			result.insert(kv.key, value);
		}
		return result;
	}

	return HashMap<String, bool *>();
}

void *OpenXRExtensionWrapper::set_system_properties_and_get_next_pointer(void *p_next_pointer) {
	uint64_t pointer;

	if (ORBLIT_VIRTUAL_CALL(_set_system_properties_and_get_next_pointer, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return nullptr;
}

void *OpenXRExtensionWrapper::set_instance_create_info_and_get_next_pointer(XrVersion p_xr_version, void *p_next_pointer) {
	uint64_t pointer;

	if (ORBLIT_VIRTUAL_CALL(_set_instance_create_info_and_get_next_pointer, (uint64_t)p_xr_version, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return nullptr;
}

void *OpenXRExtensionWrapper::set_session_create_and_get_next_pointer(void *p_next_pointer) {
	uint64_t pointer;

	if (ORBLIT_VIRTUAL_CALL(_set_session_create_and_get_next_pointer, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return nullptr;
}

void *OpenXRExtensionWrapper::set_swapchain_create_info_and_get_next_pointer(void *p_next_pointer) {
	uint64_t pointer;

	if (ORBLIT_VIRTUAL_CALL(_set_swapchain_create_info_and_get_next_pointer, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return nullptr;
}

void *OpenXRExtensionWrapper::set_hand_joint_locations_and_get_next_pointer(int p_hand_index, void *p_next_pointer) {
	uint64_t pointer;

	if (ORBLIT_VIRTUAL_CALL(_set_hand_joint_locations_and_get_next_pointer, p_hand_index, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return nullptr;
}

void *OpenXRExtensionWrapper::set_projection_views_and_get_next_pointer(int p_view_index, void *p_next_pointer) {
	uint64_t pointer = 0;

	if (ORBLIT_VIRTUAL_CALL(_set_projection_views_and_get_next_pointer, p_view_index, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return nullptr;
}

void *OpenXRExtensionWrapper::set_reference_space_create_info_and_get_next_pointer(int p_reference_space_type, void *p_next_pointer) {
	uint64_t pointer = 0;

	if (ORBLIT_VIRTUAL_CALL(_set_reference_space_create_info_and_get_next_pointer, p_reference_space_type, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return nullptr;
}

void *OpenXRExtensionWrapper::set_frame_wait_info_and_get_next_pointer(void *p_next_pointer) {
	uint64_t pointer = 0;

	if (ORBLIT_VIRTUAL_CALL(_set_frame_wait_info_and_get_next_pointer, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return nullptr;
}

void *OpenXRExtensionWrapper::set_frame_end_info_and_get_next_pointer(void *p_next_pointer) {
	uint64_t pointer = 0;

	if (ORBLIT_VIRTUAL_CALL(_set_frame_end_info_and_get_next_pointer, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return nullptr;
}

void *OpenXRExtensionWrapper::set_projection_layer_and_get_next_pointer(void *p_next_pointer) {
	uint64_t pointer = 0;

	if (ORBLIT_VIRTUAL_CALL(_set_projection_layer_and_get_next_pointer, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return nullptr;
}

void *OpenXRExtensionWrapper::set_spatial_container_create_info_and_get_next_pointer(RID p_spatial_container, void *p_next_pointer) {
	uint64_t pointer = 0;

	if (ORBLIT_VIRTUAL_CALL(_set_spatial_container_create_info_and_get_next_pointer, p_spatial_container, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return nullptr;
}

void OpenXRExtensionWrapper::prepare_view_configuration(uint32_t p_view_count) {
	ORBLIT_VIRTUAL_CALL(_prepare_view_configuration, p_view_count);
}

void *OpenXRExtensionWrapper::set_view_configuration_and_get_next_pointer(uint32_t p_view, void *p_next_pointer) {
	uint64_t pointer = 0;

	if (ORBLIT_VIRTUAL_CALL(_set_view_configuration_and_get_next_pointer, p_view, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return nullptr;
}

void OpenXRExtensionWrapper::print_view_configuration_info(uint32_t p_view) const {
	ORBLIT_VIRTUAL_CALL(_print_view_configuration_info, p_view);
}

void *OpenXRExtensionWrapper::set_view_locate_info_and_get_next_pointer(void *p_next_pointer) {
	uint64_t pointer = 0;

	if (ORBLIT_VIRTUAL_CALL(_set_view_locate_info_and_get_next_pointer, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return nullptr;
}

void *OpenXRExtensionWrapper::set_spatial_container_views_locate_info_and_get_next_pointer(RID p_spatial_container, void *p_next_pointer) {
	uint64_t pointer = 0;

	if (ORBLIT_VIRTUAL_CALL(_set_spatial_container_views_locate_info_and_get_next_pointer, p_spatial_container, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return nullptr;
}

PackedStringArray OpenXRExtensionWrapper::get_suggested_tracker_names() {
	PackedStringArray ret;

	if (ORBLIT_VIRTUAL_CALL(_get_suggested_tracker_names, ret)) {
		return ret;
	}

	return PackedStringArray();
}

int OpenXRExtensionWrapper::get_composition_layer_count() {
	int count = 0;
	ORBLIT_VIRTUAL_CALL(_get_composition_layer_count, count);
	return count;
}

XrCompositionLayerBaseHeader *OpenXRExtensionWrapper::get_composition_layer(int p_index) {
	uint64_t pointer;

	if (ORBLIT_VIRTUAL_CALL(_get_composition_layer, p_index, pointer)) {
		return reinterpret_cast<XrCompositionLayerBaseHeader *>(pointer);
	}

	return nullptr;
}

int OpenXRExtensionWrapper::get_composition_layer_order(int p_index) {
	int order = 0;
	ORBLIT_VIRTUAL_CALL(_get_composition_layer_order, p_index, order);
	return order;
}

void OpenXRExtensionWrapper::on_register_metadata(OpenXRInteractionProfileMetadata *p_interaction_profile_metadata) {
	if (ORBLIT_VIRTUAL_CALL(_on_register_metadata, p_interaction_profile_metadata)) {
		return;
	}
}

void OpenXRExtensionWrapper::on_before_instance_created() {
	ORBLIT_VIRTUAL_CALL(_on_before_instance_created);
}

void OpenXRExtensionWrapper::on_instance_created(const XrInstance p_instance) {
	uint64_t instance = (uint64_t)p_instance;
	ORBLIT_VIRTUAL_CALL(_on_instance_created, instance);
}

void OpenXRExtensionWrapper::on_instance_destroyed() {
	ORBLIT_VIRTUAL_CALL(_on_instance_destroyed);
}

void OpenXRExtensionWrapper::on_session_created(const XrSession p_session) {
	uint64_t session = (uint64_t)p_session;
	ORBLIT_VIRTUAL_CALL(_on_session_created, session);
}

void OpenXRExtensionWrapper::on_process() {
	ORBLIT_VIRTUAL_CALL(_on_process);
}

void OpenXRExtensionWrapper::on_sync_actions() {
	ORBLIT_VIRTUAL_CALL(_on_sync_actions);
}

void OpenXRExtensionWrapper::on_pre_render() {
	ORBLIT_VIRTUAL_CALL(_on_pre_render);
}

void OpenXRExtensionWrapper::on_main_swapchains_created() {
	ORBLIT_VIRTUAL_CALL(_on_main_swapchains_created);
}

void OpenXRExtensionWrapper::on_session_destroyed() {
	ORBLIT_VIRTUAL_CALL(_on_session_destroyed);
}

void OpenXRExtensionWrapper::on_pre_draw_viewport(RID p_render_target) {
	ORBLIT_VIRTUAL_CALL(_on_pre_draw_viewport, p_render_target);
}

void OpenXRExtensionWrapper::on_post_draw_viewport(RID p_render_target) {
	ORBLIT_VIRTUAL_CALL(_on_post_draw_viewport, p_render_target);
}

void OpenXRExtensionWrapper::on_state_idle() {
	ORBLIT_VIRTUAL_CALL(_on_state_idle);
}

void OpenXRExtensionWrapper::on_state_ready() {
	ORBLIT_VIRTUAL_CALL(_on_state_ready);
}

void OpenXRExtensionWrapper::on_state_synchronized() {
	ORBLIT_VIRTUAL_CALL(_on_state_synchronized);
}

void OpenXRExtensionWrapper::on_state_visible() {
	ORBLIT_VIRTUAL_CALL(_on_state_visible);
}

void OpenXRExtensionWrapper::on_state_focused() {
	ORBLIT_VIRTUAL_CALL(_on_state_focused);
}

void OpenXRExtensionWrapper::on_state_stopping() {
	ORBLIT_VIRTUAL_CALL(_on_state_stopping);
}

void OpenXRExtensionWrapper::on_state_loss_pending() {
	ORBLIT_VIRTUAL_CALL(_on_state_loss_pending);
}

void OpenXRExtensionWrapper::on_state_exiting() {
	ORBLIT_VIRTUAL_CALL(_on_state_exiting);
}

bool OpenXRExtensionWrapper::on_event_polled(const XrEventDataBuffer &p_event) {
	bool event_polled;

	if (ORBLIT_VIRTUAL_CALL(_on_event_polled, NativeExtensionPtr<const void>(&p_event), event_polled)) {
		return event_polled;
	}

	return false;
}

void *OpenXRExtensionWrapper::set_viewport_composition_layer_and_get_next_pointer(const XrCompositionLayerBaseHeader *p_layer, const Dictionary &p_property_values, void *p_next_pointer) {
	uint64_t pointer = 0;

	if (ORBLIT_VIRTUAL_CALL(_set_viewport_composition_layer_and_get_next_pointer, NativeExtensionPtr<const void>(p_layer), p_property_values, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return p_next_pointer;
}

void OpenXRExtensionWrapper::on_viewport_composition_layer_destroyed(const XrCompositionLayerBaseHeader *p_layer) {
	ORBLIT_VIRTUAL_CALL(_on_viewport_composition_layer_destroyed, NativeExtensionPtr<const void>(p_layer));
}

void OpenXRExtensionWrapper::get_viewport_composition_layer_extension_properties(List<PropertyInfo> *p_property_list) {
	TypedArray<Dictionary> properties;

	if (ORBLIT_VIRTUAL_CALL(_get_viewport_composition_layer_extension_properties, properties)) {
		for (int i = 0; i < properties.size(); i++) {
			p_property_list->push_back(PropertyInfo::from_dict(properties[i]));
		}
	}
}

Dictionary OpenXRExtensionWrapper::get_viewport_composition_layer_extension_property_defaults() {
	Dictionary property_defaults;
	ORBLIT_VIRTUAL_CALL(_get_viewport_composition_layer_extension_property_defaults, property_defaults);
	return property_defaults;
}

void *OpenXRExtensionWrapper::set_android_surface_swapchain_create_info_and_get_next_pointer(const Dictionary &p_property_values, void *p_next_pointer) {
	uint64_t pointer = 0;

	if (ORBLIT_VIRTUAL_CALL(_set_android_surface_swapchain_create_info_and_get_next_pointer, p_property_values, NativeExtensionPtr<void>(p_next_pointer), pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return p_next_pointer;
}

TypedArray<Projection> OpenXRExtensionWrapper::get_camera_projections(const StringName &p_tracker_name, double p_aspect, double p_z_near, double p_z_far) {
	TypedArray<Projection> camera_projections;

	ORBLIT_VIRTUAL_CALL(_get_camera_projections, p_tracker_name, p_aspect, p_z_near, p_z_far, camera_projections);

	return camera_projections;
}

TypedArray<Transform3D> OpenXRExtensionWrapper::get_camera_offsets(const StringName &p_tracker_name) {
	TypedArray<Transform3D> camera_offsets;

	ORBLIT_VIRTUAL_CALL(_get_camera_offsets, p_tracker_name, camera_offsets);

	return camera_offsets;
}

Ref<OpenXRAPIExtension> OpenXRExtensionWrapper::_native_extension_get_openxr_api() {
	return openxr_api_extension;
}

void OpenXRExtensionWrapper::_native_extension_register_extension_wrapper() {
	OpenXRAPI::register_extension_wrapper(this);
}

OpenXRExtensionWrapper::OpenXRExtensionWrapper() {
	openxr_api_extension.instantiate();
}

OpenXRExtensionWrapper::~OpenXRExtensionWrapper() {
}
