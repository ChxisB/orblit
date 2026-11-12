/**************************************************************************/
/*  area_spatial.cpp                                                           */

#include "area_spatial.h"

#include "body_spatial.h"
#include "soft_body_spatial.h"
#include "space_spatial.h"

OrblitArea3D::BodyKey::BodyKey(OrblitSoftBody3D *p_body, uint32_t p_body_shape, uint32_t p_area_shape) {
	rid = p_body->get_self();
	instance_id = p_body->get_instance_id();
	body_shape = p_body_shape;
	area_shape = p_area_shape;
}

OrblitArea3D::BodyKey::BodyKey(OrblitBody3D *p_body, uint32_t p_body_shape, uint32_t p_area_shape) {
	rid = p_body->get_self();
	instance_id = p_body->get_instance_id();
	body_shape = p_body_shape;
	area_shape = p_area_shape;
}

OrblitArea3D::BodyKey::BodyKey(OrblitArea3D *p_body, uint32_t p_body_shape, uint32_t p_area_shape) {
	rid = p_body->get_self();
	instance_id = p_body->get_instance_id();
	body_shape = p_body_shape;
	area_shape = p_area_shape;
}

void OrblitArea3D::_shapes_changed() {
	if (!moved_list.in_list() && get_space()) {
		get_space()->area_add_to_moved_list(&moved_list);
	}
}

void OrblitArea3D::set_transform(const Transform3D &p_transform) {
	if (!moved_list.in_list() && get_space()) {
		get_space()->area_add_to_moved_list(&moved_list);
	}

	_set_transform(p_transform);
	_set_inv_transform(p_transform.affine_inverse());
}

void OrblitArea3D::set_space(OrblitSpace3D *p_space) {
	if (get_space()) {
		if (monitor_query_list.in_list()) {
			get_space()->area_remove_from_monitor_query_list(&monitor_query_list);
		}
		if (moved_list.in_list()) {
			get_space()->area_remove_from_moved_list(&moved_list);
		}
	}

	monitored_bodies.clear();
	monitored_areas.clear();

	_set_space(p_space);

	if (!moved_list.in_list() && get_space()) {
		get_space()->area_add_to_moved_list(&moved_list);
	}
}

void OrblitArea3D::set_monitor_callback(const Callable &p_callback) {
	_unregister_shapes();

	monitor_callback = p_callback;

	monitored_bodies.clear();
	monitored_areas.clear();

	_shape_changed();

	if (!moved_list.in_list() && get_space()) {
		get_space()->area_add_to_moved_list(&moved_list);
	}
}

void OrblitArea3D::set_area_monitor_callback(const Callable &p_callback) {
	_unregister_shapes();

	area_monitor_callback = p_callback;

	monitored_bodies.clear();
	monitored_areas.clear();

	_shape_changed();

	if (!moved_list.in_list() && get_space()) {
		get_space()->area_add_to_moved_list(&moved_list);
	}
}

void OrblitArea3D::_set_space_override_mode(PS3DE::AreaSpaceOverrideMode &r_mode, PS3DE::AreaSpaceOverrideMode p_new_mode) {
	bool do_override = p_new_mode != PS3DE::AREA_SPACE_OVERRIDE_DISABLED;
	if (do_override == (r_mode != PS3DE::AREA_SPACE_OVERRIDE_DISABLED)) {
		return;
	}
	_unregister_shapes();
	r_mode = p_new_mode;
	_shape_changed();
}

void OrblitArea3D::set_param(PS3DE::AreaParameter p_param, const Variant &p_value) {
	switch (p_param) {
		case PS3DE::AREA_PARAM_GRAVITY_OVERRIDE_MODE:
			_set_space_override_mode(gravity_override_mode, (PS3DE::AreaSpaceOverrideMode)(int)p_value);
			break;
		case PS3DE::AREA_PARAM_GRAVITY:
			gravity = p_value;
			break;
		case PS3DE::AREA_PARAM_GRAVITY_VECTOR:
			gravity_vector = p_value;
			break;
		case PS3DE::AREA_PARAM_GRAVITY_IS_POINT:
			gravity_is_point = p_value;
			break;
		case PS3DE::AREA_PARAM_GRAVITY_POINT_UNIT_DISTANCE:
			gravity_point_unit_distance = p_value;
			break;
		case PS3DE::AREA_PARAM_LINEAR_DAMP_OVERRIDE_MODE:
			_set_space_override_mode(linear_damping_override_mode, (PS3DE::AreaSpaceOverrideMode)(int)p_value);
			break;
		case PS3DE::AREA_PARAM_LINEAR_DAMP:
			linear_damp = p_value;
			break;
		case PS3DE::AREA_PARAM_ANGULAR_DAMP_OVERRIDE_MODE:
			_set_space_override_mode(angular_damping_override_mode, (PS3DE::AreaSpaceOverrideMode)(int)p_value);
			break;
		case PS3DE::AREA_PARAM_ANGULAR_DAMP:
			angular_damp = p_value;
			break;
		case PS3DE::AREA_PARAM_PRIORITY:
			priority = p_value;
			break;
		case PS3DE::AREA_PARAM_WIND_FORCE_MAGNITUDE:
			ERR_FAIL_COND_MSG(wind_force_magnitude < 0, "Wind force magnitude must be a non-negative real number, but a negative number was specified.");
			wind_force_magnitude = p_value;
			break;
		case PS3DE::AREA_PARAM_WIND_SOURCE:
			wind_source = p_value;
			break;
		case PS3DE::AREA_PARAM_WIND_DIRECTION:
			wind_direction = p_value;
			break;
		case PS3DE::AREA_PARAM_WIND_ATTENUATION_FACTOR:
			ERR_FAIL_COND_MSG(wind_attenuation_factor < 0, "Wind attenuation factor must be a non-negative real number, but a negative number was specified.");
			wind_attenuation_factor = p_value;
			break;
	}
}

Variant OrblitArea3D::get_param(PS3DE::AreaParameter p_param) const {
	switch (p_param) {
		case PS3DE::AREA_PARAM_GRAVITY_OVERRIDE_MODE:
			return gravity_override_mode;
		case PS3DE::AREA_PARAM_GRAVITY:
			return gravity;
		case PS3DE::AREA_PARAM_GRAVITY_VECTOR:
			return gravity_vector;
		case PS3DE::AREA_PARAM_GRAVITY_IS_POINT:
			return gravity_is_point;
		case PS3DE::AREA_PARAM_GRAVITY_POINT_UNIT_DISTANCE:
			return gravity_point_unit_distance;
		case PS3DE::AREA_PARAM_LINEAR_DAMP_OVERRIDE_MODE:
			return linear_damping_override_mode;
		case PS3DE::AREA_PARAM_LINEAR_DAMP:
			return linear_damp;
		case PS3DE::AREA_PARAM_ANGULAR_DAMP_OVERRIDE_MODE:
			return angular_damping_override_mode;
		case PS3DE::AREA_PARAM_ANGULAR_DAMP:
			return angular_damp;
		case PS3DE::AREA_PARAM_PRIORITY:
			return priority;
		case PS3DE::AREA_PARAM_WIND_FORCE_MAGNITUDE:
			return wind_force_magnitude;
		case PS3DE::AREA_PARAM_WIND_SOURCE:
			return wind_source;
		case PS3DE::AREA_PARAM_WIND_DIRECTION:
			return wind_direction;
		case PS3DE::AREA_PARAM_WIND_ATTENUATION_FACTOR:
			return wind_attenuation_factor;
	}

	return Variant();
}

void OrblitArea3D::_queue_monitor_update() {
	ERR_FAIL_NULL(get_space());

	if (!monitor_query_list.in_list()) {
		get_space()->area_add_to_monitor_query_list(&monitor_query_list);
	}
}

void OrblitArea3D::set_monitorable(bool p_monitorable) {
	if (monitorable == p_monitorable) {
		return;
	}

	monitorable = p_monitorable;
	_set_static(!monitorable);
	_shapes_changed();
}

void OrblitArea3D::_send_monitor_event(const Callable &p_callback, PS3DE::AreaBodyStatus p_status, const BodyKey &p_key) {
	Variant res[5];
	Variant *resptr[5];
	for (int i = 0; i < 5; i++) {
		resptr[i] = &res[i];
	}
	res[0] = p_status;
	res[1] = p_key.rid;
	res[2] = p_key.instance_id;
	res[3] = p_key.body_shape;
	res[4] = p_key.area_shape;

	Callable::CallError ce;
	Variant ret;
	p_callback.callp((const Variant **)resptr, 5, ret, ce);

	if (ce.error != Callable::CallError::CALL_OK) {
		ERR_PRINT_ONCE("Error calling monitor callback method " + Variant::get_callable_error_text(p_callback, (const Variant **)resptr, 5, ce));
	}
}

void OrblitArea3D::_flush_monitor_events(HashMap<BodyKey, BodyState, BodyKey> &p_events, Callable &p_callback) {
	if (p_callback.is_null() || p_events.is_empty()) {
		return;
	}
	if (!p_callback.is_valid()) {
		p_events.clear();
		p_callback = Callable();
		return;
	}

	// Added events go first. The stage counts the shapes a body touches, so a body
	// that moves between two shapes of one area would otherwise leave and enter again.
	for (int pass = 0; pass < 2; pass++) {
		const bool added_pass = pass == 0;
		for (HashMap<BodyKey, BodyState, BodyKey>::Iterator E = p_events.begin(); E;) {
			HashMap<BodyKey, BodyState, BodyKey>::Iterator next = E;
			++next;
			const int state = E->value.state;
			if (state != 0 && (state > 0) != added_pass) {
				E = next;
				continue;
			}
			const BodyKey key = E->key;
			p_events.remove(E);
			E = next;
			if (state != 0) {
				_send_monitor_event(p_callback, state > 0 ? PS3DE::AREA_BODY_ADDED : PS3DE::AREA_BODY_REMOVED, key);
			}
		}
	}
}

void OrblitArea3D::call_queries() {
	_flush_monitor_events(monitored_bodies, monitor_callback);
	_flush_monitor_events(monitored_areas, area_monitor_callback);
}

void OrblitArea3D::compute_gravity(const Vector3 &p_position, Vector3 &r_gravity) const {
	if (is_gravity_point()) {
		const real_t gr_unit_dist = get_gravity_point_unit_distance();
		Vector3 v = get_transform().xform(get_gravity_vector()) - p_position;
		if (gr_unit_dist > 0) {
			const real_t v_length_sq = v.length_squared();
			if (v_length_sq > 0) {
				const real_t gravity_strength = get_gravity() * gr_unit_dist * gr_unit_dist / v_length_sq;
				r_gravity = v.normalized() * gravity_strength;
			} else {
				r_gravity = Vector3();
			}
		} else {
			r_gravity = v.normalized() * get_gravity();
		}
	} else {
		r_gravity = get_gravity_vector() * get_gravity();
	}
}

OrblitArea3D::OrblitArea3D() :
		OrblitCollisionObject3D(TYPE_AREA),
		monitor_query_list(this),
		moved_list(this) {
	_set_static(true); //areas are never active
	set_ray_pickable(false);
}

OrblitArea3D::~OrblitArea3D() {
}
