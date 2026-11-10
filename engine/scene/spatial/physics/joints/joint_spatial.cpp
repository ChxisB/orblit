/**************************************************************************/
/*  joint_spatial.cpp                                                          */

#include "joint_spatial.h"

#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"

void Joint3D::_disconnect_signals() {
	Entity *node_a = get_entity_or_null(a);
	PhysicsBody3D *body_a = Object::cast_to<PhysicsBody3D>(node_a);
	if (body_a) {
		body_a->disconnect(SceneStringName(stage_exiting), callable_mp(this, &Joint3D::_body_exit_tree));
	}

	Entity *node_b = get_entity_or_null(b);
	PhysicsBody3D *body_b = Object::cast_to<PhysicsBody3D>(node_b);
	if (body_b) {
		body_b->disconnect(SceneStringName(stage_exiting), callable_mp(this, &Joint3D::_body_exit_tree));
	}
}

void Joint3D::_body_exit_tree() {
	_disconnect_signals();
	_update_joint(true);
	update_configuration_warnings();
}

void Joint3D::_update_joint(bool p_only_free) {
	if (ba.is_valid() && bb.is_valid()) {
		PhysicsSystem3D::get_singleton()->body_remove_collision_exception(ba, bb);
		PhysicsSystem3D::get_singleton()->body_remove_collision_exception(bb, ba);
	}

	ba = RID();
	bb = RID();

	configured = false;

	if (p_only_free || !is_inside_stage()) {
		PhysicsSystem3D::get_singleton()->joint_clear(joint);
		warning = String();
		return;
	}

	Entity *node_a = get_entity_or_null(a);
	Entity *node_b = get_entity_or_null(b);

	PhysicsBody3D *body_a = Object::cast_to<PhysicsBody3D>(node_a);
	PhysicsBody3D *body_b = Object::cast_to<PhysicsBody3D>(node_b);

	if (node_a && !body_a && node_b && !body_b) {
		warning = RTR("Node A and Node B must be PhysicsBody3Ds");
	} else if (node_a && !body_a) {
		warning = RTR("Node A must be a PhysicsBody3D");
	} else if (node_b && !body_b) {
		warning = RTR("Node B must be a PhysicsBody3D");
	} else if (!body_a && !body_b) {
		warning = RTR("Joint is not connected to any PhysicsBody3Ds");
	} else if (body_a == body_b) {
		warning = RTR("Node A and Node B must be different PhysicsBody3Ds");
	} else {
		warning = String();
	}

	update_configuration_warnings();

	if (!warning.is_empty()) {
		PhysicsSystem3D::get_singleton()->joint_clear(joint);
		return;
	}

	configured = true;

	if (body_a) {
		_configure_joint(joint, body_a, body_b);
	} else if (body_b) {
		_configure_joint(joint, body_b, nullptr);
	}

	PhysicsSystem3D::get_singleton()->joint_set_solver_priority(joint, solver_priority);

	if (body_a) {
		ba = body_a->get_rid();
		if (!body_a->is_connected(SceneStringName(stage_exiting), callable_mp(this, &Joint3D::_body_exit_tree))) {
			body_a->connect(SceneStringName(stage_exiting), callable_mp(this, &Joint3D::_body_exit_tree));
		}
	}

	if (body_b) {
		bb = body_b->get_rid();
		if (!body_b->is_connected(SceneStringName(stage_exiting), callable_mp(this, &Joint3D::_body_exit_tree))) {
			body_b->connect(SceneStringName(stage_exiting), callable_mp(this, &Joint3D::_body_exit_tree));
		}
	}

	PhysicsSystem3D::get_singleton()->joint_disable_collisions_between_bodies(joint, exclude_from_collision);
}

void Joint3D::set_node_a(const EntityPath &p_node_a) {
	if (a == p_node_a) {
		return;
	}

	if (is_configured()) {
		_disconnect_signals();
	}

	a = p_node_a;
	_update_joint();
}

EntityPath Joint3D::get_node_a() const {
	return a;
}

void Joint3D::set_node_b(const EntityPath &p_node_b) {
	if (b == p_node_b) {
		return;
	}

	if (is_configured()) {
		_disconnect_signals();
	}

	b = p_node_b;
	_update_joint();
}

EntityPath Joint3D::get_node_b() const {
	return b;
}

void Joint3D::set_solver_priority(int p_priority) {
	solver_priority = p_priority;
	if (joint.is_valid()) {
		PhysicsSystem3D::get_singleton()->joint_set_solver_priority(joint, solver_priority);
	}
}

int Joint3D::get_solver_priority() const {
	return solver_priority;
}

void Joint3D::_notification(int p_what) {
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

void Joint3D::set_exclude_nodes_from_collision(bool p_enable) {
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

bool Joint3D::get_exclude_nodes_from_collision() const {
	return exclude_from_collision;
}

PackedStringArray Joint3D::get_configuration_warnings() const {
	PackedStringArray warnings = Entity3D::get_configuration_warnings();

	if (!warning.is_empty()) {
		warnings.push_back(warning);
	}

	return warnings;
}

void Joint3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_node_a", "node"), &Joint3D::set_node_a);
	ClassRegistry::bind_method(D_METHOD("get_node_a"), &Joint3D::get_node_a);

	ClassRegistry::bind_method(D_METHOD("set_node_b", "node"), &Joint3D::set_node_b);
	ClassRegistry::bind_method(D_METHOD("get_node_b"), &Joint3D::get_node_b);

	ClassRegistry::bind_method(D_METHOD("set_solver_priority", "priority"), &Joint3D::set_solver_priority);
	ClassRegistry::bind_method(D_METHOD("get_solver_priority"), &Joint3D::get_solver_priority);

	ClassRegistry::bind_method(D_METHOD("set_exclude_nodes_from_collision", "enable"), &Joint3D::set_exclude_nodes_from_collision);
	ClassRegistry::bind_method(D_METHOD("get_exclude_nodes_from_collision"), &Joint3D::get_exclude_nodes_from_collision);

	ClassRegistry::bind_method(D_METHOD("get_rid"), &Joint3D::get_rid);

	ADD_PROPERTY(PropertyInfo(Variant::ENTITY_PATH, "node_a", PROPERTY_HINT_ENTITY_PATH_VALID_TYPES, "PhysicsBody3D"), "set_node_a", "get_node_a");
	ADD_PROPERTY(PropertyInfo(Variant::ENTITY_PATH, "node_b", PROPERTY_HINT_ENTITY_PATH_VALID_TYPES, "PhysicsBody3D"), "set_node_b", "get_node_b");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "solver_priority", PROPERTY_HINT_RANGE, "1,8,1"), "set_solver_priority", "get_solver_priority");

	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "exclude_nodes_from_collision"), "set_exclude_nodes_from_collision", "get_exclude_nodes_from_collision");
}

Joint3D::Joint3D() {
	set_notify_transform(true);
	joint = PhysicsSystem3D::get_singleton()->joint_create();
}

Joint3D::~Joint3D() {
	ERR_FAIL_NULL(PhysicsSystem3D::get_singleton());
	PhysicsSystem3D::get_singleton()->free_rid(joint);
}
