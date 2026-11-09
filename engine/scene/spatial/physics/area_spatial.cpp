/**************************************************************************/
/*  area_spatial.cpp                                                           */

#include "area_spatial.h"

#include "core/config/engine.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "systems/audio/audio_system.h"
#include "systems/physics_spatial/physics_system_spatial.h"

void Area3D::set_gravity_space_override_mode(SpaceOverride p_mode) {
	gravity_space_override = p_mode;
	PhysicsSystem3D::get_singleton()->area_set_param(get_rid(), PS3DE::AREA_PARAM_GRAVITY_OVERRIDE_MODE, p_mode);
}

Area3D::SpaceOverride Area3D::get_gravity_space_override_mode() const {
	return gravity_space_override;
}

void Area3D::set_gravity_is_point(bool p_enabled) {
	gravity_is_point = p_enabled;
	PhysicsSystem3D::get_singleton()->area_set_param(get_rid(), PS3DE::AREA_PARAM_GRAVITY_IS_POINT, p_enabled);
}

bool Area3D::is_gravity_a_point() const {
	return gravity_is_point;
}

void Area3D::set_gravity_point_unit_distance(real_t p_scale) {
	gravity_point_unit_distance = p_scale;
	PhysicsSystem3D::get_singleton()->area_set_param(get_rid(), PS3DE::AREA_PARAM_GRAVITY_POINT_UNIT_DISTANCE, p_scale);
}

real_t Area3D::get_gravity_point_unit_distance() const {
	return gravity_point_unit_distance;
}

void Area3D::set_gravity_point_center(const Vector3 &p_center) {
	gravity_vec = p_center;
	PhysicsSystem3D::get_singleton()->area_set_param(get_rid(), PS3DE::AREA_PARAM_GRAVITY_VECTOR, p_center);
}

const Vector3 &Area3D::get_gravity_point_center() const {
	return gravity_vec;
}

void Area3D::set_gravity_direction(const Vector3 &p_direction) {
	gravity_vec = p_direction;
	PhysicsSystem3D::get_singleton()->area_set_param(get_rid(), PS3DE::AREA_PARAM_GRAVITY_VECTOR, p_direction);
}

const Vector3 &Area3D::get_gravity_direction() const {
	return gravity_vec;
}

void Area3D::set_gravity(real_t p_gravity) {
	gravity = p_gravity;
	PhysicsSystem3D::get_singleton()->area_set_param(get_rid(), PS3DE::AREA_PARAM_GRAVITY, p_gravity);
}

real_t Area3D::get_gravity() const {
	return gravity;
}

void Area3D::set_linear_damp_space_override_mode(SpaceOverride p_mode) {
	linear_damp_space_override = p_mode;
	PhysicsSystem3D::get_singleton()->area_set_param(get_rid(), PS3DE::AREA_PARAM_LINEAR_DAMP_OVERRIDE_MODE, p_mode);
}

Area3D::SpaceOverride Area3D::get_linear_damp_space_override_mode() const {
	return linear_damp_space_override;
}

void Area3D::set_angular_damp_space_override_mode(SpaceOverride p_mode) {
	angular_damp_space_override = p_mode;
	PhysicsSystem3D::get_singleton()->area_set_param(get_rid(), PS3DE::AREA_PARAM_ANGULAR_DAMP_OVERRIDE_MODE, p_mode);
}

Area3D::SpaceOverride Area3D::get_angular_damp_space_override_mode() const {
	return angular_damp_space_override;
}

void Area3D::set_linear_damp(real_t p_linear_damp) {
	linear_damp = p_linear_damp;
	PhysicsSystem3D::get_singleton()->area_set_param(get_rid(), PS3DE::AREA_PARAM_LINEAR_DAMP, p_linear_damp);
}

real_t Area3D::get_linear_damp() const {
	return linear_damp;
}

void Area3D::set_angular_damp(real_t p_angular_damp) {
	angular_damp = p_angular_damp;
	PhysicsSystem3D::get_singleton()->area_set_param(get_rid(), PS3DE::AREA_PARAM_ANGULAR_DAMP, p_angular_damp);
}

real_t Area3D::get_angular_damp() const {
	return angular_damp;
}

void Area3D::set_priority(int p_priority) {
	priority = p_priority;
	PhysicsSystem3D::get_singleton()->area_set_param(get_rid(), PS3DE::AREA_PARAM_PRIORITY, p_priority);
}

int Area3D::get_priority() const {
	return priority;
}

void Area3D::set_wind_force_magnitude(real_t p_wind_force_magnitude) {
	wind_force_magnitude = p_wind_force_magnitude;
	if (is_inside_stage()) {
		_initialize_wind();
	}
}

real_t Area3D::get_wind_force_magnitude() const {
	return wind_force_magnitude;
}

void Area3D::set_wind_attenuation_factor(real_t p_wind_force_attenuation_factor) {
	wind_attenuation_factor = p_wind_force_attenuation_factor;
	if (is_inside_stage()) {
		_initialize_wind();
	}
}

real_t Area3D::get_wind_attenuation_factor() const {
	return wind_attenuation_factor;
}

void Area3D::set_wind_source_path(const EntityPath &p_wind_source_path) {
	wind_source_path = p_wind_source_path;
	if (is_inside_stage()) {
		_initialize_wind();
	}
}

const EntityPath &Area3D::get_wind_source_path() const {
	return wind_source_path;
}

void Area3D::_initialize_wind() {
	real_t temp_magnitude = 0.0;
	Vector3 wind_direction(0., 0., 0.);
	Vector3 wind_source(0., 0., 0.);

	// Overwrite with area-specified info if available
	if (!wind_source_path.is_empty()) {
		Entity *wind_source_node = get_entity_or_null(wind_source_path);
		ERR_FAIL_NULL_MSG(wind_source_node, "Path to wind source is invalid: '" + String(wind_source_path) + "'.");
		Entity3D *wind_source_node3d = Object::cast_to<Entity3D>(wind_source_node);
		ERR_FAIL_NULL_MSG(wind_source_node3d, "Path to wind source does not point to a Entity3D: '" + String(wind_source_path) + "'.");
		Transform3D global_transform = wind_source_node3d->get_transform();
		wind_direction = -global_transform.basis.get_column(Vector3::AXIS_Z).normalized();
		wind_source = global_transform.origin;
		temp_magnitude = wind_force_magnitude;
	}

	// Set force, source and direction in the physics server.
	PhysicsSystem3D::get_singleton()->area_set_param(get_rid(), PS3DE::AREA_PARAM_WIND_ATTENUATION_FACTOR, wind_attenuation_factor);
	PhysicsSystem3D::get_singleton()->area_set_param(get_rid(), PS3DE::AREA_PARAM_WIND_SOURCE, wind_source);
	PhysicsSystem3D::get_singleton()->area_set_param(get_rid(), PS3DE::AREA_PARAM_WIND_DIRECTION, wind_direction);
	PhysicsSystem3D::get_singleton()->area_set_param(get_rid(), PS3DE::AREA_PARAM_WIND_FORCE_MAGNITUDE, temp_magnitude);
}

void Area3D::_body_enter_tree(ObjectID p_id) {
	Object *obj = ObjectRegistry::get_instance(p_id);
	Entity *node = Object::cast_to<Entity>(obj);
	ERR_FAIL_NULL(node);

	HashMap<ObjectID, BodyState>::Iterator E = body_map.find(p_id);
	ERR_FAIL_COND(!E);
	ERR_FAIL_COND(E->value.in_tree);

	E->value.in_tree = true;
	emit_signal(SceneStringName(body_entered), node);
	for (int i = 0; i < E->value.shapes.size(); i++) {
		emit_signal(SceneStringName(body_shape_entered), E->value.rid, node, E->value.shapes[i].body_shape, E->value.shapes[i].area_shape);
	}
}

void Area3D::_body_exit_tree(ObjectID p_id) {
	Object *obj = ObjectRegistry::get_instance(p_id);
	Entity *node = Object::cast_to<Entity>(obj);
	ERR_FAIL_NULL(node);
	HashMap<ObjectID, BodyState>::Iterator E = body_map.find(p_id);
	ERR_FAIL_COND(!E);
	ERR_FAIL_COND(!E->value.in_tree);
	E->value.in_tree = false;
	emit_signal(SceneStringName(body_exited), node);
	for (int i = 0; i < E->value.shapes.size(); i++) {
		emit_signal(SceneStringName(body_shape_exited), E->value.rid, node, E->value.shapes[i].body_shape, E->value.shapes[i].area_shape);
	}
}

void Area3D::_body_inout(int p_status, const RID &p_body, ObjectID p_instance, int p_body_shape, int p_area_shape) {
	bool body_in = p_status == PS3DE::AREA_BODY_ADDED;
	ObjectID objid = p_instance;

	// Exit early if instance is invalid.
	if (objid.is_null()) {
		lock_callback();
		locked = true;
		// Emit the appropriate signals.
		if (body_in) {
			emit_signal(SceneStringName(body_shape_entered), p_body, (Entity *)nullptr, p_body_shape, p_area_shape);
		} else {
			emit_signal(SceneStringName(body_shape_exited), p_body, (Entity *)nullptr, p_body_shape, p_area_shape);
		}
		locked = false;
		unlock_callback();
		return;
	}

	Object *obj = ObjectRegistry::get_instance(objid);
	Entity *node = Object::cast_to<Entity>(obj);

	HashMap<ObjectID, BodyState>::Iterator E = body_map.find(objid);

	if (!body_in && !E) {
		return; //likely removed from the tree
	}

	lock_callback();
	locked = true;

	if (body_in) {
		if (!E) {
			E = body_map.insert(objid, BodyState());
			E->value.rid = p_body;
			E->value.rc = 0;
			E->value.in_tree = node && node->is_inside_stage();
			if (node) {
				node->connect(SceneStringName(stage_entered), callable_mp(this, &Area3D::_body_enter_tree).bind(objid));
				node->connect(SceneStringName(stage_exiting), callable_mp(this, &Area3D::_body_exit_tree).bind(objid));
				if (E->value.in_tree) {
					emit_signal(SceneStringName(body_entered), node);
				}
			}
		}
		E->value.rc++;
		if (node) {
			E->value.shapes.insert(ShapePair(p_body_shape, p_area_shape));
		}

		if (!node || E->value.in_tree) {
			emit_signal(SceneStringName(body_shape_entered), p_body, node, p_body_shape, p_area_shape);
		}

	} else {
		E->value.rc--;

		if (node) {
			E->value.shapes.erase(ShapePair(p_body_shape, p_area_shape));
		}

		bool in_tree = E->value.in_tree;
		if (E->value.rc == 0) {
			body_map.remove(E);
			if (node) {
				node->disconnect(SceneStringName(stage_entered), callable_mp(this, &Area3D::_body_enter_tree));
				node->disconnect(SceneStringName(stage_exiting), callable_mp(this, &Area3D::_body_exit_tree));
				if (in_tree) {
					emit_signal(SceneStringName(body_exited), obj);
				}
			}
		}
		if (!node || in_tree) {
			emit_signal(SceneStringName(body_shape_exited), p_body, obj, p_body_shape, p_area_shape);
		}
	}

	locked = false;
	unlock_callback();
}

void Area3D::_clear_monitoring() {
	ERR_FAIL_COND_MSG(locked, "This function can't be used during the in/out signal.");

	{
		HashMap<ObjectID, BodyState> bmcopy(body_map);
		body_map.clear();
		//disconnect all monitored stuff

		for (const KeyValue<ObjectID, BodyState> &E : bmcopy) {
			Object *obj = ObjectRegistry::get_instance(E.key);
			Entity *node = Object::cast_to<Entity>(obj);

			if (!node) { //node may have been deleted in previous frame or at other legitimate point
				continue;
			}
			//ERR_CONTINUE(!node);

			node->disconnect(SceneStringName(stage_entered), callable_mp(this, &Area3D::_body_enter_tree));
			node->disconnect(SceneStringName(stage_exiting), callable_mp(this, &Area3D::_body_exit_tree));

			if (!E.value.in_tree) {
				continue;
			}

			for (int i = 0; i < E.value.shapes.size(); i++) {
				emit_signal(SceneStringName(body_shape_exited), E.value.rid, node, E.value.shapes[i].body_shape, E.value.shapes[i].area_shape);
			}

			emit_signal(SceneStringName(body_exited), node);
		}
	}

	{
		HashMap<ObjectID, AreaState> bmcopy(area_map);
		area_map.clear();
		//disconnect all monitored stuff

		for (const KeyValue<ObjectID, AreaState> &E : bmcopy) {
			Object *obj = ObjectRegistry::get_instance(E.key);
			Entity *node = Object::cast_to<Entity>(obj);

			if (!node) { //node may have been deleted in previous frame or at other legitimate point
				continue;
			}
			//ERR_CONTINUE(!node);

			node->disconnect(SceneStringName(stage_entered), callable_mp(this, &Area3D::_area_enter_tree));
			node->disconnect(SceneStringName(stage_exiting), callable_mp(this, &Area3D::_area_exit_tree));

			if (!E.value.in_tree) {
				continue;
			}

			for (int i = 0; i < E.value.shapes.size(); i++) {
				emit_signal(SceneStringName(area_shape_exited), E.value.rid, node, E.value.shapes[i].area_shape, E.value.shapes[i].self_shape);
			}

			emit_signal(SceneStringName(area_exited), obj);
		}
	}
}

void Area3D::_space_changed(const RID &p_new_space) {
	if (p_new_space.is_null()) {
		_clear_monitoring();
	}
}

void Area3D::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			_initialize_wind();
		} break;
	}
}

void Area3D::set_monitoring(bool p_enable) {
	ERR_FAIL_COND_MSG(locked, "Function blocked during in/out signal. Use set_deferred(\"monitoring\", true/false).");

	if (p_enable == monitoring) {
		return;
	}

	monitoring = p_enable;

	if (monitoring) {
		PhysicsSystem3D::get_singleton()->area_set_monitor_callback(get_rid(), callable_mp(this, &Area3D::_body_inout));
		PhysicsSystem3D::get_singleton()->area_set_area_monitor_callback(get_rid(), callable_mp(this, &Area3D::_area_inout));
	} else {
		PhysicsSystem3D::get_singleton()->area_set_monitor_callback(get_rid(), Callable());
		PhysicsSystem3D::get_singleton()->area_set_area_monitor_callback(get_rid(), Callable());
		_clear_monitoring();
	}
}

void Area3D::_area_enter_tree(ObjectID p_id) {
	Object *obj = ObjectRegistry::get_instance(p_id);
	Entity *node = Object::cast_to<Entity>(obj);
	ERR_FAIL_NULL(node);

	HashMap<ObjectID, AreaState>::Iterator E = area_map.find(p_id);
	ERR_FAIL_COND(!E);
	ERR_FAIL_COND(E->value.in_tree);

	E->value.in_tree = true;
	emit_signal(SceneStringName(area_entered), node);
	for (int i = 0; i < E->value.shapes.size(); i++) {
		emit_signal(SceneStringName(area_shape_entered), E->value.rid, node, E->value.shapes[i].area_shape, E->value.shapes[i].self_shape);
	}
}

void Area3D::_area_exit_tree(ObjectID p_id) {
	Object *obj = ObjectRegistry::get_instance(p_id);
	Entity *node = Object::cast_to<Entity>(obj);
	ERR_FAIL_NULL(node);
	HashMap<ObjectID, AreaState>::Iterator E = area_map.find(p_id);
	ERR_FAIL_COND(!E);
	ERR_FAIL_COND(!E->value.in_tree);
	E->value.in_tree = false;
	emit_signal(SceneStringName(area_exited), node);
	for (int i = 0; i < E->value.shapes.size(); i++) {
		emit_signal(SceneStringName(area_shape_exited), E->value.rid, node, E->value.shapes[i].area_shape, E->value.shapes[i].self_shape);
	}
}

void Area3D::_area_inout(int p_status, const RID &p_area, ObjectID p_instance, int p_area_shape, int p_self_shape) {
	bool area_in = p_status == PS3DE::AREA_BODY_ADDED;
	ObjectID objid = p_instance;

	// Exit if instance is invalid.
	if (objid.is_null()) {
		lock_callback();
		locked = true;
		// Emit the appropriate signals.
		if (area_in) {
			emit_signal(SceneStringName(area_shape_entered), p_area, (Entity *)nullptr, p_area_shape, p_self_shape);
		} else {
			emit_signal(SceneStringName(area_shape_exited), p_area, (Entity *)nullptr, p_area_shape, p_self_shape);
		}
		locked = false;
		unlock_callback();
		return;
	}

	Object *obj = ObjectRegistry::get_instance(objid);
	Entity *node = Object::cast_to<Entity>(obj);

	HashMap<ObjectID, AreaState>::Iterator E = area_map.find(objid);

	if (!area_in && !E) {
		return; //likely removed from the tree
	}

	lock_callback();
	locked = true;

	if (area_in) {
		if (!E) {
			E = area_map.insert(objid, AreaState());
			E->value.rid = p_area;
			E->value.rc = 0;
			E->value.in_tree = node && node->is_inside_stage();
			if (node) {
				node->connect(SceneStringName(stage_entered), callable_mp(this, &Area3D::_area_enter_tree).bind(objid));
				node->connect(SceneStringName(stage_exiting), callable_mp(this, &Area3D::_area_exit_tree).bind(objid));
				if (E->value.in_tree) {
					emit_signal(SceneStringName(area_entered), node);
				}
			}
		}
		E->value.rc++;
		if (node) {
			E->value.shapes.insert(AreaShapePair(p_area_shape, p_self_shape));
		}

		if (!node || E->value.in_tree) {
			emit_signal(SceneStringName(area_shape_entered), p_area, node, p_area_shape, p_self_shape);
		}

	} else {
		E->value.rc--;

		if (node) {
			E->value.shapes.erase(AreaShapePair(p_area_shape, p_self_shape));
		}

		bool in_tree = E->value.in_tree;
		if (E->value.rc == 0) {
			area_map.remove(E);
			if (node) {
				node->disconnect(SceneStringName(stage_entered), callable_mp(this, &Area3D::_area_enter_tree));
				node->disconnect(SceneStringName(stage_exiting), callable_mp(this, &Area3D::_area_exit_tree));
				if (in_tree) {
					emit_signal(SceneStringName(area_exited), obj);
				}
			}
		}
		if (!node || in_tree) {
			emit_signal(SceneStringName(area_shape_exited), p_area, obj, p_area_shape, p_self_shape);
		}
	}

	locked = false;
	unlock_callback();
}

bool Area3D::is_monitoring() const {
	return monitoring;
}

TypedArray<Entity3D> Area3D::get_overlapping_bodies() const {
	TypedArray<Entity3D> ret;
	ERR_FAIL_COND_V_MSG(!monitoring, ret, "Can't find overlapping bodies when monitoring is off.");
	ret.resize(body_map.size());
	int idx = 0;
	for (const KeyValue<ObjectID, BodyState> &E : body_map) {
		Object *obj = ObjectRegistry::get_instance(E.key);
		if (obj) {
			ret[idx] = obj;
			idx++;
		}
	}

	ret.resize(idx);
	return ret;
}

bool Area3D::has_overlapping_bodies() const {
	ERR_FAIL_COND_V_MSG(!monitoring, false, "Can't find overlapping bodies when monitoring is off.");
	return !body_map.is_empty();
}

void Area3D::set_monitorable(bool p_enable) {
	ERR_FAIL_COND_MSG(locked || (is_inside_stage() && PhysicsSystem3D::get_singleton()->is_flushing_queries()), "Function blocked during in/out signal. Use set_deferred(\"monitorable\", true/false).");

	if (p_enable == monitorable) {
		return;
	}

	monitorable = p_enable;

	PhysicsSystem3D::get_singleton()->area_set_monitorable(get_rid(), monitorable);
}

bool Area3D::is_monitorable() const {
	return monitorable;
}

TypedArray<Area3D> Area3D::get_overlapping_areas() const {
	TypedArray<Area3D> ret;
	ERR_FAIL_COND_V_MSG(!monitoring, ret, "Can't find overlapping areas when monitoring is off.");
	ret.resize(area_map.size());
	int idx = 0;
	for (const KeyValue<ObjectID, AreaState> &E : area_map) {
		Object *obj = ObjectRegistry::get_instance(E.key);
		if (obj) {
			ret[idx] = obj;
			idx++;
		}
	}
	ret.resize(idx);
	return ret;
}

bool Area3D::has_overlapping_areas() const {
	ERR_FAIL_COND_V_MSG(!monitoring, false, "Can't find overlapping areas when monitoring is off.");
	return !area_map.is_empty();
}

bool Area3D::overlaps_area(RequiredParam<Entity> p_area) const {
	EXTRACT_PARAM_OR_FAIL_V(check_area, p_area, false);
	HashMap<ObjectID, AreaState>::ConstIterator E = area_map.find(check_area->get_instance_id());
	if (!E) {
		return false;
	}
	return E->value.in_tree;
}

bool Area3D::overlaps_body(RequiredParam<Entity> p_body) const {
	EXTRACT_PARAM_OR_FAIL_V(body, p_body, false);
	HashMap<ObjectID, BodyState>::ConstIterator E = body_map.find(body->get_instance_id());
	if (!E) {
		return false;
	}
	return E->value.in_tree;
}

void Area3D::set_audio_bus_override(bool p_override) {
	audio_bus_override = p_override;
}

bool Area3D::is_overriding_audio_bus() const {
	return audio_bus_override;
}

void Area3D::set_audio_bus_name(const StringName &p_audio_bus) {
	audio_bus = p_audio_bus;
}

StringName Area3D::get_audio_bus_name() const {
	for (int i = 0; i < AudioSystem::get_singleton()->get_bus_count(); i++) {
		if (AudioSystem::get_singleton()->get_bus_name(i) == audio_bus) {
			return audio_bus;
		}
	}
	return SceneStringName(Master);
}

void Area3D::set_use_reverb_bus(bool p_enable) {
	use_reverb_bus = p_enable;
}

bool Area3D::is_using_reverb_bus() const {
	return use_reverb_bus;
}

void Area3D::set_reverb_bus_name(const StringName &p_audio_bus) {
	reverb_bus = p_audio_bus;
}

StringName Area3D::get_reverb_bus_name() const {
	for (int i = 0; i < AudioSystem::get_singleton()->get_bus_count(); i++) {
		if (AudioSystem::get_singleton()->get_bus_name(i) == reverb_bus) {
			return reverb_bus;
		}
	}
	return SceneStringName(Master);
}

void Area3D::set_reverb_amount(float p_amount) {
	reverb_amount = p_amount;
}

float Area3D::get_reverb_amount() const {
	return reverb_amount;
}

void Area3D::set_reverb_uniformity(float p_uniformity) {
	reverb_uniformity = p_uniformity;
}

float Area3D::get_reverb_uniformity() const {
	return reverb_uniformity;
}

void Area3D::_validate_property(PropertyInfo &p_property) const {
	if (!Engine::get_singleton()->is_editor_hint()) {
		return;
	}
	if (p_property.name.begins_with("gravity") && p_property.name != "gravity_space_override") {
		if (gravity_space_override == SPACE_OVERRIDE_DISABLED) {
			p_property.usage = PROPERTY_USAGE_NO_EDITOR;
		} else {
			if (gravity_is_point) {
				if (p_property.name == "gravity_direction") {
					p_property.usage = PROPERTY_USAGE_NO_EDITOR;
				}
			} else {
				if (p_property.name.begins_with("gravity_point_")) {
					p_property.usage = PROPERTY_USAGE_NO_EDITOR;
				}
			}
		}
	} else if (p_property.name.begins_with("linear_damp") && p_property.name != "linear_damp_space_override") {
		if (linear_damp_space_override == SPACE_OVERRIDE_DISABLED) {
			p_property.usage = PROPERTY_USAGE_NO_EDITOR;
		}
	} else if (p_property.name.begins_with("angular_damp") && p_property.name != "angular_damp_space_override") {
		if (angular_damp_space_override == SPACE_OVERRIDE_DISABLED) {
			p_property.usage = PROPERTY_USAGE_NO_EDITOR;
		}
	}
}

void Area3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_gravity_space_override_mode", "space_override_mode"), &Area3D::set_gravity_space_override_mode);
	ClassRegistry::bind_method(D_METHOD("get_gravity_space_override_mode"), &Area3D::get_gravity_space_override_mode);

	ClassRegistry::bind_method(D_METHOD("set_gravity_is_point", "enable"), &Area3D::set_gravity_is_point);
	ClassRegistry::bind_method(D_METHOD("is_gravity_a_point"), &Area3D::is_gravity_a_point);

	ClassRegistry::bind_method(D_METHOD("set_gravity_point_unit_distance", "distance_scale"), &Area3D::set_gravity_point_unit_distance);
	ClassRegistry::bind_method(D_METHOD("get_gravity_point_unit_distance"), &Area3D::get_gravity_point_unit_distance);

	ClassRegistry::bind_method(D_METHOD("set_gravity_point_center", "center"), &Area3D::set_gravity_point_center);
	ClassRegistry::bind_method(D_METHOD("get_gravity_point_center"), &Area3D::get_gravity_point_center);

	ClassRegistry::bind_method(D_METHOD("set_gravity_direction", "direction"), &Area3D::set_gravity_direction);
	ClassRegistry::bind_method(D_METHOD("get_gravity_direction"), &Area3D::get_gravity_direction);

	ClassRegistry::bind_method(D_METHOD("set_gravity", "gravity"), &Area3D::set_gravity);
	ClassRegistry::bind_method(D_METHOD("get_gravity"), &Area3D::get_gravity);

	ClassRegistry::bind_method(D_METHOD("set_linear_damp_space_override_mode", "space_override_mode"), &Area3D::set_linear_damp_space_override_mode);
	ClassRegistry::bind_method(D_METHOD("get_linear_damp_space_override_mode"), &Area3D::get_linear_damp_space_override_mode);

	ClassRegistry::bind_method(D_METHOD("set_angular_damp_space_override_mode", "space_override_mode"), &Area3D::set_angular_damp_space_override_mode);
	ClassRegistry::bind_method(D_METHOD("get_angular_damp_space_override_mode"), &Area3D::get_angular_damp_space_override_mode);

	ClassRegistry::bind_method(D_METHOD("set_angular_damp", "angular_damp"), &Area3D::set_angular_damp);
	ClassRegistry::bind_method(D_METHOD("get_angular_damp"), &Area3D::get_angular_damp);

	ClassRegistry::bind_method(D_METHOD("set_linear_damp", "linear_damp"), &Area3D::set_linear_damp);
	ClassRegistry::bind_method(D_METHOD("get_linear_damp"), &Area3D::get_linear_damp);

	ClassRegistry::bind_method(D_METHOD("set_priority", "priority"), &Area3D::set_priority);
	ClassRegistry::bind_method(D_METHOD("get_priority"), &Area3D::get_priority);

	ClassRegistry::bind_method(D_METHOD("set_wind_force_magnitude", "wind_force_magnitude"), &Area3D::set_wind_force_magnitude);
	ClassRegistry::bind_method(D_METHOD("get_wind_force_magnitude"), &Area3D::get_wind_force_magnitude);

	ClassRegistry::bind_method(D_METHOD("set_wind_attenuation_factor", "wind_attenuation_factor"), &Area3D::set_wind_attenuation_factor);
	ClassRegistry::bind_method(D_METHOD("get_wind_attenuation_factor"), &Area3D::get_wind_attenuation_factor);

	ClassRegistry::bind_method(D_METHOD("set_wind_source_path", "wind_source_path"), &Area3D::set_wind_source_path);
	ClassRegistry::bind_method(D_METHOD("get_wind_source_path"), &Area3D::get_wind_source_path);

	ClassRegistry::bind_method(D_METHOD("set_monitorable", "enable"), &Area3D::set_monitorable);
	ClassRegistry::bind_method(D_METHOD("is_monitorable"), &Area3D::is_monitorable);

	ClassRegistry::bind_method(D_METHOD("set_monitoring", "enable"), &Area3D::set_monitoring);
	ClassRegistry::bind_method(D_METHOD("is_monitoring"), &Area3D::is_monitoring);

	ClassRegistry::bind_method(D_METHOD("get_overlapping_bodies"), &Area3D::get_overlapping_bodies);
	ClassRegistry::bind_method(D_METHOD("get_overlapping_areas"), &Area3D::get_overlapping_areas);

	ClassRegistry::bind_method(D_METHOD("has_overlapping_bodies"), &Area3D::has_overlapping_bodies);
	ClassRegistry::bind_method(D_METHOD("has_overlapping_areas"), &Area3D::has_overlapping_areas);

	ClassRegistry::bind_method(D_METHOD("overlaps_body", "body"), &Area3D::overlaps_body);
	ClassRegistry::bind_method(D_METHOD("overlaps_area", "area"), &Area3D::overlaps_area);

	ClassRegistry::bind_method(D_METHOD("set_audio_bus_override", "enable"), &Area3D::set_audio_bus_override);
	ClassRegistry::bind_method(D_METHOD("is_overriding_audio_bus"), &Area3D::is_overriding_audio_bus);

	ClassRegistry::bind_method(D_METHOD("set_audio_bus_name", "name"), &Area3D::set_audio_bus_name);
	ClassRegistry::bind_method(D_METHOD("get_audio_bus_name"), &Area3D::get_audio_bus_name);

	ClassRegistry::bind_method(D_METHOD("set_use_reverb_bus", "enable"), &Area3D::set_use_reverb_bus);
	ClassRegistry::bind_method(D_METHOD("is_using_reverb_bus"), &Area3D::is_using_reverb_bus);

	ClassRegistry::bind_method(D_METHOD("set_reverb_bus_name", "name"), &Area3D::set_reverb_bus_name);
	ClassRegistry::bind_method(D_METHOD("get_reverb_bus_name"), &Area3D::get_reverb_bus_name);

	ClassRegistry::bind_method(D_METHOD("set_reverb_amount", "amount"), &Area3D::set_reverb_amount);
	ClassRegistry::bind_method(D_METHOD("get_reverb_amount"), &Area3D::get_reverb_amount);

	ClassRegistry::bind_method(D_METHOD("set_reverb_uniformity", "amount"), &Area3D::set_reverb_uniformity);
	ClassRegistry::bind_method(D_METHOD("get_reverb_uniformity"), &Area3D::get_reverb_uniformity);

	ADD_SIGNAL(MethodInfo("body_shape_entered", PropertyInfo(Variant::RID, "body_rid"), PropertyInfo(Variant::OBJECT, "body", PROPERTY_HINT_RESOURCE_TYPE, Entity3D::get_static_class_name()), PropertyInfo(Variant::INT, "body_shape_index"), PropertyInfo(Variant::INT, "local_shape_index")));
	ADD_SIGNAL(MethodInfo("body_shape_exited", PropertyInfo(Variant::RID, "body_rid"), PropertyInfo(Variant::OBJECT, "body", PROPERTY_HINT_RESOURCE_TYPE, Entity3D::get_static_class_name()), PropertyInfo(Variant::INT, "body_shape_index"), PropertyInfo(Variant::INT, "local_shape_index")));
	ADD_SIGNAL(MethodInfo("body_entered", PropertyInfo(Variant::OBJECT, "body", PROPERTY_HINT_RESOURCE_TYPE, Entity3D::get_static_class_name())));
	ADD_SIGNAL(MethodInfo("body_exited", PropertyInfo(Variant::OBJECT, "body", PROPERTY_HINT_RESOURCE_TYPE, Entity3D::get_static_class_name())));

	ADD_SIGNAL(MethodInfo("area_shape_entered", PropertyInfo(Variant::RID, "area_rid"), PropertyInfo(Variant::OBJECT, "area", PROPERTY_HINT_RESOURCE_TYPE, Area3D::get_static_class_name()), PropertyInfo(Variant::INT, "area_shape_index"), PropertyInfo(Variant::INT, "local_shape_index")));
	ADD_SIGNAL(MethodInfo("area_shape_exited", PropertyInfo(Variant::RID, "area_rid"), PropertyInfo(Variant::OBJECT, "area", PROPERTY_HINT_RESOURCE_TYPE, Area3D::get_static_class_name()), PropertyInfo(Variant::INT, "area_shape_index"), PropertyInfo(Variant::INT, "local_shape_index")));
	ADD_SIGNAL(MethodInfo("area_entered", PropertyInfo(Variant::OBJECT, "area", PROPERTY_HINT_RESOURCE_TYPE, Area3D::get_static_class_name())));
	ADD_SIGNAL(MethodInfo("area_exited", PropertyInfo(Variant::OBJECT, "area", PROPERTY_HINT_RESOURCE_TYPE, Area3D::get_static_class_name())));

	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "monitoring"), "set_monitoring", "is_monitoring");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "monitorable"), "set_monitorable", "is_monitorable");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "priority", PROPERTY_HINT_RANGE, "0,100000,1,or_greater,or_less"), "set_priority", "get_priority");

	ADD_GROUP("Gravity", "gravity_");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "gravity_space_override", PROPERTY_HINT_ENUM, "Disabled,Combine,Combine-Replace,Replace,Replace-Combine", PROPERTY_USAGE_DEFAULT | PROPERTY_USAGE_UPDATE_ALL_IF_MODIFIED), "set_gravity_space_override_mode", "get_gravity_space_override_mode");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "gravity_point", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_DEFAULT | PROPERTY_USAGE_UPDATE_ALL_IF_MODIFIED), "set_gravity_is_point", "is_gravity_a_point");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "gravity_point_unit_distance", PROPERTY_HINT_RANGE, "0,1024,0.001,or_greater,exp,suffix:m"), "set_gravity_point_unit_distance", "get_gravity_point_unit_distance");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "gravity_point_center", PROPERTY_HINT_NONE, "suffix:m"), "set_gravity_point_center", "get_gravity_point_center");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "gravity_direction"), "set_gravity_direction", "get_gravity_direction");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "gravity", PROPERTY_HINT_RANGE, U"-32,32,0.001,or_less,or_greater,suffix:m/s\u00B2"), "set_gravity", "get_gravity");

	ADD_GROUP("Linear Damp", "linear_damp_");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "linear_damp_space_override", PROPERTY_HINT_ENUM, "Disabled,Combine,Combine-Replace,Replace,Replace-Combine", PROPERTY_USAGE_DEFAULT | PROPERTY_USAGE_UPDATE_ALL_IF_MODIFIED), "set_linear_damp_space_override_mode", "get_linear_damp_space_override_mode");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "linear_damp", PROPERTY_HINT_RANGE, "0,100,0.001,or_greater"), "set_linear_damp", "get_linear_damp");

	ADD_GROUP("Angular Damp", "angular_damp_");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "angular_damp_space_override", PROPERTY_HINT_ENUM, "Disabled,Combine,Combine-Replace,Replace,Replace-Combine", PROPERTY_USAGE_DEFAULT | PROPERTY_USAGE_UPDATE_ALL_IF_MODIFIED), "set_angular_damp_space_override_mode", "get_angular_damp_space_override_mode");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "angular_damp", PROPERTY_HINT_RANGE, "0,100,0.001,or_greater"), "set_angular_damp", "get_angular_damp");

	ADD_GROUP("Wind", "wind_");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "wind_force_magnitude", PROPERTY_HINT_RANGE, "0,10,0.001,or_greater"), "set_wind_force_magnitude", "get_wind_force_magnitude");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "wind_attenuation_factor", PROPERTY_HINT_RANGE, "0.0,3.0,0.001,or_greater"), "set_wind_attenuation_factor", "get_wind_attenuation_factor");
	ADD_PROPERTY(PropertyInfo(Variant::ENTITY_PATH, "wind_source_path", PROPERTY_HINT_ENTITY_PATH_VALID_TYPES, "Entity3D"), "set_wind_source_path", "get_wind_source_path");

	ADD_GROUP("Audio Bus", "audio_bus_");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "audio_bus_override"), "set_audio_bus_override", "is_overriding_audio_bus");
	ADD_PROPERTY(PropertyInfo(Variant::STRING_NAME, "audio_bus_name", PROPERTY_HINT_AUDIO_BUS), "set_audio_bus_name", "get_audio_bus_name");

	ADD_GROUP("Reverb Bus", "reverb_bus_");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "reverb_bus_enabled", PROPERTY_HINT_GROUP_ENABLE), "set_use_reverb_bus", "is_using_reverb_bus");
	ADD_PROPERTY(PropertyInfo(Variant::STRING_NAME, "reverb_bus_name", PROPERTY_HINT_AUDIO_BUS), "set_reverb_bus_name", "get_reverb_bus_name");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "reverb_bus_amount", PROPERTY_HINT_RANGE, "0,1,0.01"), "set_reverb_amount", "get_reverb_amount");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "reverb_bus_uniformity", PROPERTY_HINT_RANGE, "0,1,0.01"), "set_reverb_uniformity", "get_reverb_uniformity");

	BIND_ENUM_CONSTANT(SPACE_OVERRIDE_DISABLED);
	BIND_ENUM_CONSTANT(SPACE_OVERRIDE_COMBINE);
	BIND_ENUM_CONSTANT(SPACE_OVERRIDE_COMBINE_REPLACE);
	BIND_ENUM_CONSTANT(SPACE_OVERRIDE_REPLACE);
	BIND_ENUM_CONSTANT(SPACE_OVERRIDE_REPLACE_COMBINE);
}

Area3D::Area3D() :
		CollisionObject3D(PhysicsSystem3D::get_singleton()->area_create(), true) {
	audio_bus = SceneStringName(Master);
	reverb_bus = SceneStringName(Master);
	set_gravity(9.8);
	set_gravity_direction(Vector3(0, -1, 0));
	set_monitoring(true);
	set_monitorable(true);
}

Area3D::~Area3D() {
}
