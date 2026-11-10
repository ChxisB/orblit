/**************************************************************************/
/*  openxr_visibility_mask.cpp                                            */

#include "openxr_visibility_mask.h"

#include "../extensions/openxr_visibility_mask_extension.h"
#include "../openxr_interface.h"

#include "core/object/callable_mp.h"
#include "scene/spatial/xr/xr_nodes.h"
#include "systems/rendering/rendering_system.h"

void OpenXRVisibilityMask::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			OpenXRVisibilityMaskExtension *vis_mask_ext = OpenXRVisibilityMaskExtension::get_singleton();
			if (vis_mask_ext && vis_mask_ext->is_available()) {
				set_base(vis_mask_ext->get_mesh());
			}
		} break;
		case NOTIFICATION_EXIT_STAGE: {
			set_base(RID());
		} break;
	}
}

void OpenXRVisibilityMask::_on_openxr_session_begun() {
	if (is_inside_stage()) {
		OpenXRVisibilityMaskExtension *vis_mask_ext = OpenXRVisibilityMaskExtension::get_singleton();
		if (vis_mask_ext && vis_mask_ext->is_available()) {
			set_base(vis_mask_ext->get_mesh());
		}
	}
}

void OpenXRVisibilityMask::_on_openxr_session_stopping() {
	set_base(RID());
}

PackedStringArray OpenXRVisibilityMask::get_configuration_warnings() const {
	PackedStringArray warnings = VisualInstance3D::get_configuration_warnings();

	if (is_visible() && is_inside_stage()) {
		XRCamera3D *camera = Object::cast_to<XRCamera3D>(get_parent());
		if (camera == nullptr) {
			warnings.push_back(RTR("OpenXR visibility mask must have an XRCamera3D node as their parent."));
		}
	}

	return warnings;
}

AABB OpenXRVisibilityMask::get_aabb() const {
	AABB ret;

	// Make sure it's always visible, this is positioned through its shader.
	ret.position = Vector3(-1000.0, -1000.0, -1000.0);
	ret.size = Vector3(2000.0, 2000.0, 2000.0);

	return ret;
}

OpenXRVisibilityMask::OpenXRVisibilityMask() {
	Ref<OpenXRInterface> openxr_interface = XRSystem::get_singleton()->find_interface("OpenXR");
	if (openxr_interface.is_valid()) {
		openxr_interface->connect("session_begun", callable_mp(this, &OpenXRVisibilityMask::_on_openxr_session_begun));
		openxr_interface->connect("session_stopping", callable_mp(this, &OpenXRVisibilityMask::_on_openxr_session_stopping));
	}

	RS::get_singleton()->instance_geometry_set_cast_shadows_setting(get_instance(), RSE::SHADOW_CASTING_SETTING_OFF);
}

OpenXRVisibilityMask::~OpenXRVisibilityMask() {
	Ref<OpenXRInterface> openxr_interface = XRSystem::get_singleton()->find_interface("OpenXR");
	if (openxr_interface.is_valid()) {
		openxr_interface->disconnect("session_begun", callable_mp(this, &OpenXRVisibilityMask::_on_openxr_session_begun));
		openxr_interface->disconnect("session_stopping", callable_mp(this, &OpenXRVisibilityMask::_on_openxr_session_stopping));
	}
}
