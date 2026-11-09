/**************************************************************************/
/*  openxr_foveated_inset_viewport.cpp                                    */

#include "openxr_foveated_inset_viewport.h"

#include "core/config/engine.h"
#include "core/object/class_registry.h"
#include "systems/xr/xr_system.h"

#include "modules/openxr/extensions/openxr_foveated_inset_extension.h"

OpenXRFoveatedInsetViewport::OpenXRFoveatedInsetViewport() {
	set_internal_frame_enabled(true);
	set_run_priority(99999);
	set_update_mode(SubViewport::UPDATE_DISABLED);

	OpenXRFoveatedInsetExtension *fi_ext = OpenXRFoveatedInsetExtension::get_singleton();
	if (!fi_ext) {
		// Silently fail, this is likely being called when we instantiate our class for
		// populating ClassRegistry.
		return;
	}

	fi_ext->register_viewport(get_viewport_rid());

	xr_origin = memnew(XROrigin3D);
	xr_origin->set_current(false);
	add_child(xr_origin, false, Entity3D::INTERNAL_MODE_BACK);

	xr_camera = memnew(XRCamera3D);
	xr_camera->set_tracker(XR_TRACKER_INSET);
	xr_origin->add_child(xr_camera, false, Entity3D::INTERNAL_MODE_BACK);
}

OpenXRFoveatedInsetViewport::~OpenXRFoveatedInsetViewport() {
	OpenXRFoveatedInsetExtension *fi_ext = OpenXRFoveatedInsetExtension::get_singleton();
	if (!fi_ext) {
		// Silently fail, this is likely being called after we instantiated our class for
		// populating ClassRegistry.
		return;
	}

	fi_ext->unregister_viewport(get_viewport_rid());
}

void OpenXRFoveatedInsetViewport::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_xr_origin3d"), &OpenXRFoveatedInsetViewport::get_xr_origin3d);
	ClassRegistry::bind_method(D_METHOD("get_xr_camera3d"), &OpenXRFoveatedInsetViewport::get_xr_camera3d);
}

void OpenXRFoveatedInsetViewport::_validate_property(PropertyInfo &p_property) const {
	if (!Engine::get_singleton()->is_editor_hint()) {
		return;
	}
	// Hide properties that are managed by us.
	if (p_property.name == "size" || p_property.name == "size_2d_override" || p_property.name == "size_2d_override_stretch" || p_property.name == "view_count" || p_property.name == "use_xr" || p_property.name == "render_target_update_mode") {
		p_property.usage = PROPERTY_USAGE_NO_EDITOR;
	}
}

void OpenXRFoveatedInsetViewport::_notification(int p_what) {
	if (Engine::get_singleton()->is_editor_hint()) {
		return;
	}

	switch (p_what) {
		case NOTIFICATION_INTERNAL_FRAME: {
			// Update the size of our viewport
			_update_size();

			// Apply our world origin
			XRSystem *xr_system = XRSystem::get_singleton();
			if (xr_system) {
				xr_origin->set_transform(xr_system->get_world_origin());
			}
		} break;
	}
}

void OpenXRFoveatedInsetViewport::_update_size() {
	OpenXRAPI *openxr_api = OpenXRAPI::get_singleton();
	OpenXRFoveatedInsetExtension *fi_ext = OpenXRFoveatedInsetExtension::get_singleton();
	if (openxr_api == nullptr || fi_ext == nullptr) {
		set_update_mode(SubViewport::UPDATE_DISABLED);
		return;
	}

	if (!openxr_api->is_initialized()) {
		set_update_mode(SubViewport::UPDATE_DISABLED);
		return;
	}

	if (openxr_api->get_view_configuration() != XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO_WITH_FOVEATED_INSET) {
		WARN_PRINT_ONCE("View configuration is not set to stereo with foveated inset, or this is not supported.");
		set_update_mode(SubViewport::UPDATE_DISABLED);
		return;
	}

	set_update_mode(SubViewport::UPDATE_ALWAYS);

	Size2i new_size = fi_ext->get_render_size();
	if (get_size() != new_size) {
		set_size(new_size);
	}

	if (get_view_count() != 2) {
		set_view_count(2);
	}
}
