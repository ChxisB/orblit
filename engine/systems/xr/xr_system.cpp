/**************************************************************************/
/*  xr_system.cpp                                                         */

#include "xr_system.h"

#include "core/config/project_settings.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "systems/rendering/rendering_system.h"
#include "systems/xr/xr_interface.h"
#include "systems/xr/xr_positional_tracker.h"

XRSystem::XRMode XRSystem::xr_mode = XRMODE_DEFAULT;

XRSystem::XRMode XRSystem::get_xr_mode() {
	return xr_mode;
}

void XRSystem::set_xr_mode(XRSystem::XRMode p_mode) {
	xr_mode = p_mode;
}

XRSystem *XRSystem::singleton = nullptr;

XRSystem *XRSystem::get_singleton() {
	return singleton;
}

void XRSystem::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_world_scale"), &XRSystem::get_world_scale);
	ClassRegistry::bind_method(D_METHOD("set_world_scale", "scale"), &XRSystem::set_world_scale);
	ClassRegistry::bind_method(D_METHOD("get_world_origin"), &XRSystem::get_world_origin);
	ClassRegistry::bind_method(D_METHOD("set_world_origin", "world_origin"), &XRSystem::set_world_origin);
	ClassRegistry::bind_method(D_METHOD("get_reference_frame"), &XRSystem::get_reference_frame);
	ClassRegistry::bind_method(D_METHOD("clear_reference_frame"), &XRSystem::clear_reference_frame);
	ClassRegistry::bind_method(D_METHOD("center_on_hmd", "rotation_mode", "keep_height"), &XRSystem::center_on_hmd);
	ClassRegistry::bind_method(D_METHOD("get_hmd_transform"), &XRSystem::get_hmd_transform);
	ClassRegistry::bind_method(D_METHOD("set_camera_locked_to_origin", "enabled"), &XRSystem::set_camera_locked_to_origin);
	ClassRegistry::bind_method(D_METHOD("is_camera_locked_to_origin"), &XRSystem::is_camera_locked_to_origin);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "world_scale"), "set_world_scale", "get_world_scale");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "world_origin"), "set_world_origin", "get_world_origin");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "camera_locked_to_origin"), "set_camera_locked_to_origin", "is_camera_locked_to_origin");

	ClassRegistry::bind_method(D_METHOD("add_interface", "interface"), &XRSystem::add_interface);
	ClassRegistry::bind_method(D_METHOD("get_interface_count"), &XRSystem::get_interface_count);
	ClassRegistry::bind_method(D_METHOD("remove_interface", "interface"), &XRSystem::remove_interface);
	ClassRegistry::bind_method(D_METHOD("get_interface", "idx"), &XRSystem::get_interface);
	ClassRegistry::bind_method(D_METHOD("get_interfaces"), &XRSystem::get_interfaces);
	ClassRegistry::bind_method(D_METHOD("find_interface", "name"), &XRSystem::find_interface);

	ClassRegistry::bind_method(D_METHOD("add_tracker", "tracker"), &XRSystem::add_tracker);
	ClassRegistry::bind_method(D_METHOD("remove_tracker", "tracker"), &XRSystem::remove_tracker);
	ClassRegistry::bind_method(D_METHOD("get_trackers", "tracker_types"), &XRSystem::get_trackers);
	ClassRegistry::bind_method(D_METHOD("get_tracker", "tracker_name"), &XRSystem::get_tracker);

	ClassRegistry::bind_method(D_METHOD("get_camera_projections", "tracker_name", "aspect", "near", "far"), &XRSystem::get_camera_projections);
	ClassRegistry::bind_method(D_METHOD("get_camera_offsets", "tracker_name"), &XRSystem::get_camera_offsets);

	ClassRegistry::bind_method(D_METHOD("get_primary_interface"), &XRSystem::get_primary_interface);
	ClassRegistry::bind_method(D_METHOD("set_primary_interface", "interface"), &XRSystem::set_primary_interface);

	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "primary_interface"), "set_primary_interface", "get_primary_interface");

	BIND_ENUM_CONSTANT(TRACKER_CAMERA);
	BIND_ENUM_CONSTANT(TRACKER_CONTROLLER);
	BIND_ENUM_CONSTANT(TRACKER_BASESTATION);
	BIND_ENUM_CONSTANT(TRACKER_ANCHOR);
	BIND_ENUM_CONSTANT(TRACKER_HAND);
	BIND_ENUM_CONSTANT(TRACKER_BODY);
	BIND_ENUM_CONSTANT(TRACKER_FACE);
	BIND_ENUM_CONSTANT(TRACKER_ANY_KNOWN);
	BIND_ENUM_CONSTANT(TRACKER_UNKNOWN);
	BIND_ENUM_CONSTANT(TRACKER_ANY);

	BIND_ENUM_CONSTANT(RESET_FULL_ROTATION);
	BIND_ENUM_CONSTANT(RESET_BUT_KEEP_TILT);
	BIND_ENUM_CONSTANT(DONT_RESET_ROTATION);

	ADD_SIGNAL(MethodInfo("reference_frame_changed"));

	ADD_SIGNAL(MethodInfo("interface_added", PropertyInfo(Variant::STRING_NAME, "interface_name")));
	ADD_SIGNAL(MethodInfo("interface_removed", PropertyInfo(Variant::STRING_NAME, "interface_name")));

	ADD_SIGNAL(MethodInfo("tracker_added", PropertyInfo(Variant::STRING_NAME, "tracker_name"), PropertyInfo(Variant::INT, "type")));
	ADD_SIGNAL(MethodInfo("tracker_updated", PropertyInfo(Variant::STRING_NAME, "tracker_name"), PropertyInfo(Variant::INT, "type")));
	ADD_SIGNAL(MethodInfo("tracker_removed", PropertyInfo(Variant::STRING_NAME, "tracker_name"), PropertyInfo(Variant::INT, "type")));

	ADD_SIGNAL(MethodInfo("world_origin_changed"));
}

double XRSystem::get_world_scale() const {
	RenderingSystem *rendering_system = RenderingSystem::get_singleton();

	if (rendering_system && rendering_system->is_on_render_thread()) {
		// Return the value with which we're currently rendering,
		// if we're on the render thread
		return render_state.world_scale;
	} else {
		// Return our current value
		return world_scale;
	}
}

void XRSystem::set_world_scale(double p_world_scale) {
	if (p_world_scale < 0.01) {
		p_world_scale = 0.01;
	} else if (p_world_scale > 1000.0) {
		p_world_scale = 1000.0;
	}

	world_scale = p_world_scale;
	set_render_world_scale(world_scale);
}

void XRSystem::_set_render_world_scale(double p_world_scale) {
	// Must be called from rendering thread!
	ERR_NOT_ON_RENDER_THREAD;

	XRSystem *xr_system = XRSystem::get_singleton();
	ERR_FAIL_NULL(xr_system);
	xr_system->render_state.world_scale = p_world_scale;
}

Transform3D XRSystem::get_world_origin() const {
	RenderingSystem *rendering_system = RenderingSystem::get_singleton();

	if (rendering_system && rendering_system->is_on_render_thread()) {
		// Return the value with which we're currently rendering,
		// if we're on the render thread
		return render_state.world_origin;
	} else {
		// Return our current value
		return world_origin;
	}
}

void XRSystem::set_world_origin(const Transform3D &p_world_origin) {
	world_origin = p_world_origin;
	emit_signal(SNAME("world_origin_changed"));
	set_render_world_origin(world_origin);
}

void XRSystem::_set_render_world_origin(const Transform3D &p_world_origin) {
	// Must be called from rendering thread!
	ERR_NOT_ON_RENDER_THREAD;

	XRSystem *xr_system = XRSystem::get_singleton();
	ERR_FAIL_NULL(xr_system);
	xr_system->render_state.world_origin = p_world_origin;
}

Transform3D XRSystem::get_reference_frame() const {
	RenderingSystem *rendering_system = RenderingSystem::get_singleton();
	ERR_FAIL_NULL_V(rendering_system, reference_frame);

	if (rendering_system->is_on_render_thread()) {
		// Return the value with which we're currently rendering,
		// if we're on the render thread
		return render_state.reference_frame;
	} else {
		// Return our current value
		return reference_frame;
	}
}

void XRSystem::center_on_hmd(RotationMode p_rotation_mode, bool p_keep_height) {
	if (primary_interface.is_null()) {
		return;
	}

	// clear our current reference frame or we'll end up double adjusting it
	reference_frame = Transform3D();

	// requesting our EYE_MONO transform should return our current HMD position
	Transform3D new_reference_frame = primary_interface->get_camera_transform();

	// remove our tilt
	if (p_rotation_mode == 1) {
		// take the Y out of our Z
		new_reference_frame.basis.set_column(2, Vector3(new_reference_frame.basis.rows[0][2], 0.0, new_reference_frame.basis.rows[2][2]).normalized());

		// Y is straight up
		new_reference_frame.basis.set_column(1, Vector3(0.0, 1.0, 0.0));

		// and X is our cross reference
		new_reference_frame.basis.set_column(0, new_reference_frame.basis.get_column(1).cross(new_reference_frame.basis.get_column(2)).normalized());
	} else if (p_rotation_mode == 2) {
		// remove our rotation, we're only interesting in centering on position
		new_reference_frame.basis = Basis();
	}

	// don't negate our height
	if (p_keep_height) {
		new_reference_frame.origin.y = 0.0;
	}

	reference_frame = new_reference_frame.inverse();
	set_render_reference_frame(reference_frame);
	emit_signal(SNAME("reference_frame_changed"));
}

void XRSystem::clear_reference_frame() {
	reference_frame = Transform3D();
	set_render_reference_frame(reference_frame);
	emit_signal(SNAME("reference_frame_changed"));
}

void XRSystem::_set_render_reference_frame(const Transform3D &p_reference_frame) {
	// Must be called from rendering thread!
	ERR_NOT_ON_RENDER_THREAD;

	XRSystem *xr_system = XRSystem::get_singleton();
	ERR_FAIL_NULL(xr_system);
	xr_system->render_state.reference_frame = p_reference_frame;
}

void XRSystem::set_render_world_scale(double p_world_scale) {
	// If we're rendering on a separate thread, we may still be processing the last frame, don't communicate this till we're ready...
	RenderingSystem *rendering_system = RenderingSystem::get_singleton();
	ERR_FAIL_NULL(rendering_system);

	rendering_system->call_on_render_thread(callable_mp_static(&XRSystem::_set_render_world_scale).bind(p_world_scale));
}

void XRSystem::set_render_world_origin(const Transform3D &p_world_origin) {
	// If we're rendering on a separate thread, we may still be processing the last frame, don't communicate this till we're ready...
	RenderingSystem *rendering_system = RenderingSystem::get_singleton();
	ERR_FAIL_NULL(rendering_system);

	rendering_system->call_on_render_thread(callable_mp_static(&XRSystem::_set_render_world_origin).bind(p_world_origin));
}

void XRSystem::set_render_reference_frame(const Transform3D &p_reference_frame) {
	// If we're rendering on a separate thread, we may still be processing the last frame, don't communicate this till we're ready...
	RenderingSystem *rendering_system = RenderingSystem::get_singleton();
	ERR_FAIL_NULL(rendering_system);

	rendering_system->call_on_render_thread(callable_mp_static(&XRSystem::_set_render_reference_frame).bind(p_reference_frame));
}

Transform3D XRSystem::get_hmd_transform() {
	Transform3D hmd_transform;
	if (primary_interface.is_valid()) {
		hmd_transform = primary_interface->get_camera_transform();
	}
	return hmd_transform;
}

void XRSystem::set_camera_locked_to_origin(bool p_enable) {
	camera_locked_to_origin = p_enable;
}

void XRSystem::add_interface(const Ref<XRInterface> &p_interface) {
	ERR_FAIL_COND(p_interface.is_null());

	if (interfaces.has(p_interface)) {
		ERR_PRINT("Interface was already added");
		return;
	}

	interfaces.push_back(p_interface);
	emit_signal(SNAME("interface_added"), p_interface->get_name());
}

void XRSystem::remove_interface(const Ref<XRInterface> &p_interface) {
	ERR_FAIL_COND(p_interface.is_null());

	int idx = interfaces.find(p_interface);
	ERR_FAIL_COND_MSG(idx == -1, "Interface not found.");
	print_verbose("XR: Removed interface \"" + p_interface->get_name() + "\"");
	emit_signal(SNAME("interface_removed"), p_interface->get_name());
	interfaces.remove_at(idx);
}

int XRSystem::get_interface_count() const {
	return interfaces.size();
}

Ref<XRInterface> XRSystem::get_interface(int p_index) const {
	ERR_FAIL_INDEX_V(p_index, (int)interfaces.size(), nullptr);

	return interfaces[p_index];
}

Ref<XRInterface> XRSystem::find_interface(const String &p_name) const {
	for (const Ref<XRInterface> &interface : interfaces) {
		if (interface->get_name() == p_name) {
			return interface;
		}
	}
	return Ref<XRInterface>();
}

TypedArray<Dictionary> XRSystem::get_interfaces() const {
	Array ret;

	for (uint32_t i = 0; i < interfaces.size(); i++) {
		Dictionary iface_info;

		iface_info["id"] = i;
		iface_info["name"] = interfaces[i]->get_name();

		ret.push_back(iface_info);
	}

	return ret;
}

Ref<XRInterface> XRSystem::get_primary_interface() const {
	return primary_interface;
}

void XRSystem::set_primary_interface(const Ref<XRInterface> &p_primary_interface) {
	if (p_primary_interface.is_null()) {
		print_verbose("XR: Clearing primary interface");
		primary_interface.unref();
	} else {
		primary_interface = p_primary_interface;

		print_verbose("XR: Primary interface set to: " + primary_interface->get_name());
	}
}

void XRSystem::add_tracker(const Ref<XRTracker> &p_tracker) {
	ERR_FAIL_COND(p_tracker.is_null());

	StringName tracker_name = p_tracker->get_tracker_name();
	if (trackers.has(tracker_name)) {
		if (trackers[tracker_name] != p_tracker) {
			// We already have a tracker with this name, we're going to replace it
			trackers[tracker_name] = p_tracker;
			emit_signal(SNAME("tracker_updated"), tracker_name, p_tracker->get_tracker_type());
		}
	} else {
		trackers[tracker_name] = p_tracker;
		emit_signal(SNAME("tracker_added"), tracker_name, p_tracker->get_tracker_type());
	}
}

void XRSystem::remove_tracker(const Ref<XRTracker> &p_tracker) {
	ERR_FAIL_COND(p_tracker.is_null());

	StringName tracker_name = p_tracker->get_tracker_name();
	if (trackers.has(tracker_name)) {
		// we send the signal right before removing it
		emit_signal(SNAME("tracker_removed"), p_tracker->get_tracker_name(), p_tracker->get_tracker_type());

		// and remove it
		trackers.erase(tracker_name);
	}
}

Dictionary XRSystem::get_trackers(int p_tracker_types) {
	Dictionary res;

	for (int i = 0; i < trackers.size(); i++) {
		Ref<XRPositionalTracker> tracker = trackers.get_value_at_index(i);
		if (tracker.is_valid() && (tracker->get_tracker_type() & p_tracker_types) != 0) {
			res[tracker->get_tracker_name()] = tracker;
		}
	}

	return res;
}

Ref<XRTracker> XRSystem::get_tracker(const StringName &p_name) const {
	if (trackers.has(p_name)) {
		return trackers[p_name];
	} else {
		// tracker hasn't been registered yet, which is fine, no need to spam the error log...
		return Ref<XRTracker>();
	}
}

TypedArray<Projection> XRSystem::get_camera_projections(const StringName &p_tracker_name, double p_aspect, double p_z_near, double p_z_far) {
	TypedArray<Projection> projections;

	// Try primary interface first.
	if (primary_interface.is_valid()) {
		projections = primary_interface->get_camera_projections(p_tracker_name, p_aspect, p_z_near, p_z_far);
		if (!projections.is_empty()) {
			return projections;
		}
	}

	// Try any other active interfaces.
	for (Ref<XRInterface> interface : interfaces) {
		if (interface != primary_interface && interface->is_initialized()) {
			projections = interface->get_camera_projections(p_tracker_name, p_aspect, p_z_near, p_z_far);
			if (!projections.is_empty()) {
				return projections;
			}
		}
	}

	return projections;
}

TypedArray<Transform3D> XRSystem::get_camera_offsets(const StringName &p_tracker_name) {
	TypedArray<Transform3D> offsets;

	// Try primary interface first.
	if (primary_interface.is_valid()) {
		offsets = primary_interface->get_camera_offsets(p_tracker_name);
		if (!offsets.is_empty()) {
			return offsets;
		}
	}

	// Try any other active interfaces.
	for (Ref<XRInterface> interface : interfaces) {
		if (interface != primary_interface && interface->is_initialized()) {
			offsets = interface->get_camera_offsets(p_tracker_name);
			if (!offsets.is_empty()) {
				return offsets;
			}
		}
	}

	return offsets;
}

PackedStringArray XRSystem::get_suggested_tracker_names() const {
	PackedStringArray arr;

	for (const Ref<XRInterface> &interface : interfaces) {
		PackedStringArray interface_arr = interface->get_suggested_tracker_names();
		for (int i = 0; i < interface_arr.size(); i++) {
			if (!arr.has(interface_arr[i])) {
				arr.push_back(interface_arr[i]);
			}
		}
	}

	if (arr.is_empty()) {
		// no suggestions from our tracker? include our defaults
		arr.push_back(String("head"));
		arr.push_back(String("left_hand"));
		arr.push_back(String("right_hand"));
	}

	return arr;
}

PackedStringArray XRSystem::get_suggested_pose_names(const StringName &p_tracker_name) const {
	PackedStringArray arr;

	for (const Ref<XRInterface> &interface : interfaces) {
		PackedStringArray interface_arr = interface->get_suggested_pose_names(p_tracker_name);
		for (int a = 0; a < interface_arr.size(); a++) {
			if (!arr.has(interface_arr[a])) {
				arr.push_back(interface_arr[a]);
			}
		}
	}

	if (arr.is_empty()) {
		// no suggestions from our tracker? include our defaults
		arr.push_back(String("default"));

		if ((p_tracker_name == "left_hand") || (p_tracker_name == "right_hand")) {
			arr.push_back(String("aim"));
			arr.push_back(String("grip"));
			arr.push_back(String("skeleton"));
		}
	}

	return arr;
}

void XRSystem::_process() {
	// called from our main game loop before we handle physics and game logic
	// note that we can have multiple interfaces active if we have interfaces that purely handle tracking

	// process all active interfaces
	for (const Ref<XRInterface> &interface : interfaces) {
		if (interface.is_null()) {
			// ignore, not a valid reference
		} else if (interface->is_initialized()) {
			interface->process();
		}
	}
}

void XRSystem::pre_render() {
	// called from RendererViewport.draw_viewports right before we start drawing our viewports
	// note that we can have multiple interfaces active if we have interfaces that purely handle tracking

	// process all active interfaces
	for (const Ref<XRInterface> &interface : interfaces) {
		if (interface.is_null()) {
			// ignore, not a valid reference
		} else if (interface->is_initialized()) {
			interface->pre_render();
		}
	}
}

void XRSystem::end_frame() {
	// called from RenderingSystemDefault after Vulkan queues have been submitted

	// process all active interfaces
	for (const Ref<XRInterface> &interface : interfaces) {
		if (interface.is_null()) {
			// ignore, not a valid reference
		} else if (interface->is_initialized()) {
			interface->end_frame();
		}
	}
}

XRSystem::XRSystem() {
	singleton = this;
}

XRSystem::~XRSystem() {
	primary_interface.unref();

	interfaces.clear();
	trackers.clear();

	singleton = nullptr;
}
