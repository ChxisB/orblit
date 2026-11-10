/**************************************************************************/
/*  joint_planar.cpp                                                          */

#include "joint_planar.h"

#include "core/config/engine.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "scene/planar/physics/physics_body_planar.h"
#include "scene/stage/stage.h"
#include "systems/physics_planar/physics_system_planar.h"

void Joint2D::_disconnect_signals() {
	Entity *node_a = get_entity_or_null(a);
	PhysicsBody2D *body_a = Object::cast_to<PhysicsBody2D>(node_a);
	if (body_a) {
		body_a->disconnect(SceneStringName(stage_exiting), callable_mp(this, &Joint2D::_body_exit_tree));
	}

	Entity *node_b = get_entity_or_null(b);
	PhysicsBody2D *body_b = Object::cast_to<PhysicsBody2D>(node_b);
	if (body_b) {
		body_b->disconnect(SceneStringName(stage_exiting), callable_mp(this, &Joint2D::_body_exit_tree));
	}
}

void Joint2D::_body_exit_tree() {
	_disconnect_signals();
	_update_joint(true);
	update_configuration_warnings();
}

void Joint2D::_update_joint(bool p_only_free) {
	if (ba.is_valid() && bb.is_valid() && exclude_from_collision) {
		PhysicsSystem2D::get_singleton()->joint_disable_collisions_between_bodies(joint, false);
	}

	ba = RID();
	bb = RID();
	configured = false;

	if (p_only_free || !is_inside_stage()) {
		PhysicsSystem2D::get_singleton()->joint_clear(joint);
		warning = String();
		return;
	}

	Entity *node_a = get_entity_or_null(a);
	Entity *node_b = get_entity_or_null(b);

	PhysicsBody2D *body_a = Object::cast_to<PhysicsBody2D>(node_a);
	PhysicsBody2D *body_b = Object::cast_to<PhysicsBody2D>(node_b);

	bool valid = false;

	if (node_a && !body_a && node_b && !body_b) {
		warning = RTR("Node A and Node B must be PhysicsBody2Ds");
	} else if (node_a && !body_a) {
		warning = RTR("Node A must be a PhysicsBody2D");
	} else if (node_b && !body_b) {
		warning = RTR("Node B must be a PhysicsBody2D");
	} else if (!body_a || !body_b) {
		warning = RTR("Joint is not connected to two PhysicsBody2Ds");
	} else if (body_a == body_b) {
		warning = RTR("Node A and Node B must be different PhysicsBody2Ds");
	} else {
		warning = String();
		valid = true;
	}

	update_configuration_warnings();

	if (!valid) {
		PhysicsSystem2D::get_singleton()->joint_clear(joint);
		return;
	}

	if (body_a) {
		body_a->force_update_transform();
	}

	if (body_b) {
		body_b->force_update_transform();
	}

	configured = true;

	_configure_joint(joint, body_a, body_b);

	ERR_FAIL_COND_MSG(!joint.is_valid(), "Failed to configure the joint.");

	PhysicsSystem2D::get_singleton()->joint_set_param(joint, PS2DE::JOINT_PARAM_BIAS, bias);

	ba = body_a->get_rid();
	bb = body_b->get_rid();

	if (!body_a->is_connected(SceneStringName(stage_exiting), callable_mp(this, &Joint2D::_body_exit_tree))) {
		body_a->connect(SceneStringName(stage_exiting), callable_mp(this, &Joint2D::_body_exit_tree));
	}
	if (!body_b->is_connected(SceneStringName(stage_exiting), callable_mp(this, &Joint2D::_body_exit_tree))) {
		body_b->connect(SceneStringName(stage_exiting), callable_mp(this, &Joint2D::_body_exit_tree));
	}

	PhysicsSystem2D::get_singleton()->joint_disable_collisions_between_bodies(joint, exclude_from_collision);
}

void Joint2D::set_node_a(const EntityPath &p_node_a) {
	if (a == p_node_a) {
		return;
	}

	if (is_configured()) {
		_disconnect_signals();
	}

	a = p_node_a;
	if (Engine::get_singleton()->is_editor_hint()) {
		// When in editor, the setter may be called as a result of node rename.
		// It happens before the node actually changes its name, which triggers false warning.
		callable_mp(this, &Joint2D::_update_joint).call_deferred(false);
	} else {
		_update_joint();
	}
}

EntityPath Joint2D::get_node_a() const {
	return a;
}

void Joint2D::set_node_b(const EntityPath &p_node_b) {
	if (b == p_node_b) {
		return;
	}

	if (is_configured()) {
		_disconnect_signals();
	}

	b = p_node_b;
	if (Engine::get_singleton()->is_editor_hint()) {
		callable_mp(this, &Joint2D::_update_joint).call_deferred(false);
	} else {
		_update_joint();
	}
}

EntityPath Joint2D::get_node_b() const {
	return b;
}

void Joint2D::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_POST_ENTER_STAGE: {
			if (is_configured()) {
				_disconnect_signals();
			}
			_update_joint();
		} break;

		case NOTIFICATION_EXIT_STAGE: {
			if (is_configured()) {
				_disconnect_signals();
			}
			_update_joint(true);
		} break;
	}
}

void Joint2D::set_bias(real_t p_bias) {
	bias = p_bias;
	if (joint.is_valid()) {
		PhysicsSystem2D::get_singleton()->joint_set_param(joint, PS2DE::JOINT_PARAM_BIAS, bias);
	}
}

real_t Joint2D::get_bias() const {
	return bias;
}

void Joint2D::set_exclude_nodes_from_collision(bool p_enable) {
	if (exclude_from_collision == p_enable) {
		return;
	}
	if (is_configured()) {
		_disconnect_signals();
	}
	_update_joint(true);
	exclude_from_collision = p_enable;
	_update_joint();
}

bool Joint2D::get_exclude_nodes_from_collision() const {
	return exclude_from_collision;
}

PackedStringArray Joint2D::get_configuration_warnings() const {
	PackedStringArray warnings = Entity2D::get_configuration_warnings();

	if (!warning.is_empty()) {
		warnings.push_back(warning);
	}

	return warnings;
}

void Joint2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_node_a", "node"), &Joint2D::set_node_a);
	ClassRegistry::bind_method(D_METHOD("get_node_a"), &Joint2D::get_node_a);

	ClassRegistry::bind_method(D_METHOD("set_node_b", "node"), &Joint2D::set_node_b);
	ClassRegistry::bind_method(D_METHOD("get_node_b"), &Joint2D::get_node_b);

	ClassRegistry::bind_method(D_METHOD("set_bias", "bias"), &Joint2D::set_bias);
	ClassRegistry::bind_method(D_METHOD("get_bias"), &Joint2D::get_bias);

	ClassRegistry::bind_method(D_METHOD("set_exclude_nodes_from_collision", "enable"), &Joint2D::set_exclude_nodes_from_collision);
	ClassRegistry::bind_method(D_METHOD("get_exclude_nodes_from_collision"), &Joint2D::get_exclude_nodes_from_collision);

	ClassRegistry::bind_method(D_METHOD("get_rid"), &Joint2D::get_rid);

	ADD_PROPERTY(PropertyInfo(Variant::ENTITY_PATH, "node_a", PROPERTY_HINT_ENTITY_PATH_VALID_TYPES, "PhysicsBody2D"), "set_node_a", "get_node_a");
	ADD_PROPERTY(PropertyInfo(Variant::ENTITY_PATH, "node_b", PROPERTY_HINT_ENTITY_PATH_VALID_TYPES, "PhysicsBody2D"), "set_node_b", "get_node_b");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "bias", PROPERTY_HINT_RANGE, "0,0.9,0.001"), "set_bias", "get_bias");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "disable_collision"), "set_exclude_nodes_from_collision", "get_exclude_nodes_from_collision");
}

Joint2D::Joint2D() {
	joint = PhysicsSystem2D::get_singleton()->joint_create();
	set_hide_clip_children(true);

#ifdef DEBUG_ENABLED
	PhysicsSystem2D::get_singleton()->connect("_debug_changed", callable_mp((CanvasItem *)this, &CanvasItem::queue_redraw));
#endif
}

Joint2D::~Joint2D() {
	ERR_FAIL_NULL(PhysicsSystem2D::get_singleton());
	PhysicsSystem2D::get_singleton()->free_rid(joint);
}
