/**************************************************************************/
/*  physical_bone_planar.cpp                                                  */

#include "physical_bone_planar.h"

#include "core/object/class_registry.h"
#include "scene/planar/physics/joints/joint_planar.h"
#include "systems/physics_planar/physics_system_planar.h"

void PhysicalBone2D::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_INTERNAL_STEP: {
			// Position the RigidBody in the correct position.
			if (follow_bone_when_simulating) {
				_position_at_bone2d();
			}

			// Keep the child joint in the correct position.
			if (child_joint && auto_configure_joint) {
				child_joint->set_global_position(get_global_position());
			}
		} break;

		case NOTIFICATION_START: {
			_find_skeleton_parent();
			_find_joint_child();

			// Configure joint.
			if (child_joint && auto_configure_joint) {
				_auto_configure_joint();
			}

			// Simulate physics if set.
			if (simulate_physics) {
				_start_physics_simulation();
			} else {
				_stop_physics_simulation();
			}

			set_internal_step_enabled(true);
		} break;
	}
}

void PhysicalBone2D::_position_at_bone2d() {
	// Reset to Bone2D position
	if (parent_skeleton) {
		Bone2D *bone_to_use = parent_skeleton->get_bone(bone2d_index);
		ERR_FAIL_NULL_MSG(bone_to_use, "It's not possible to position the bone with ID: " + itos(bone2d_index) + ".");
		set_global_transform(bone_to_use->get_global_transform());
	}
}

void PhysicalBone2D::_find_skeleton_parent() {
	Entity *current_parent = get_parent();

	while (current_parent != nullptr) {
		Skeleton2D *potential_skeleton = Object::cast_to<Skeleton2D>(current_parent);
		if (potential_skeleton) {
			parent_skeleton = potential_skeleton;
			break;
		} else {
			PhysicalBone2D *potential_parent_bone = Object::cast_to<PhysicalBone2D>(current_parent);
			if (potential_parent_bone) {
				current_parent = potential_parent_bone->get_parent();
			} else {
				current_parent = nullptr;
			}
		}
	}
}

void PhysicalBone2D::_find_joint_child() {
	for (int i = 0; i < get_child_count(); i++) {
		Entity *child_node = get_child(i);
		Joint2D *potential_joint = Object::cast_to<Joint2D>(child_node);
		if (potential_joint) {
			child_joint = potential_joint;
			break;
		}
	}
}

PackedStringArray PhysicalBone2D::get_configuration_warnings() const {
	PackedStringArray warnings = RigidBody2D::get_configuration_warnings();

	if (!parent_skeleton) {
		warnings.push_back(RTR("A PhysicalBone2D only works with a Skeleton2D or another PhysicalBone2D as a parent node!"));
	}
	if (parent_skeleton && bone2d_index <= -1) {
		warnings.push_back(RTR("A PhysicalBone2D needs to be assigned to a Bone2D node in order to function! Please set a Bone2D node in the inspector."));
	}
	if (!child_joint) {
		PhysicalBone2D *parent_bone = Object::cast_to<PhysicalBone2D>(get_parent());
		if (parent_bone) {
			warnings.push_back(RTR("A PhysicalBone2D node should have a Joint2D-based child node to keep bones connected! Please add a Joint2D-based node as a child to this node!"));
		}
	}

	return warnings;
}

void PhysicalBone2D::_auto_configure_joint() {
	if (!auto_configure_joint) {
		return;
	}

	if (child_joint) {
		// Entity A = parent | Entity B = this node
		Entity *parent_node = get_parent();
		PhysicalBone2D *potential_parent_bone = Object::cast_to<PhysicalBone2D>(parent_node);

		if (potential_parent_bone) {
			child_joint->set_node_a(child_joint->get_path_to(potential_parent_bone));
			child_joint->set_node_b(child_joint->get_path_to(this));
		} else {
			WARN_PRINT("Cannot setup joint without a parent PhysicalBone2D node.");
		}

		// Place the child joint at this node's position.
		child_joint->set_global_position(get_global_position());
	}
}

void PhysicalBone2D::_start_physics_simulation() {
	if (_internal_simulate_physics) {
		return;
	}

	// Reset to Bone2D position.
	_position_at_bone2d();

	// Apply the layers and masks.
	PhysicsSystem2D::get_singleton()->body_set_collision_layer(get_rid(), get_collision_layer());
	PhysicsSystem2D::get_singleton()->body_set_collision_mask(get_rid(), get_collision_mask());
	PhysicsSystem2D::get_singleton()->body_set_collision_priority(get_rid(), get_collision_priority());

	// Apply the correct mode.
	_apply_body_mode();

	_internal_simulate_physics = true;
	set_internal_step_enabled(true);
}

void PhysicalBone2D::_stop_physics_simulation() {
	if (_internal_simulate_physics) {
		_internal_simulate_physics = false;

		// Reset to Bone2D position
		_position_at_bone2d();

		set_internal_step_enabled(false);
		PhysicsSystem2D::get_singleton()->body_set_collision_layer(get_rid(), 0);
		PhysicsSystem2D::get_singleton()->body_set_collision_mask(get_rid(), 0);
		PhysicsSystem2D::get_singleton()->body_set_collision_priority(get_rid(), 1.0);
		PhysicsSystem2D::get_singleton()->body_set_mode(get_rid(), PS2DE::BodyMode::BODY_MODE_STATIC);
	}
}

Joint2D *PhysicalBone2D::get_joint() const {
	return child_joint;
}

bool PhysicalBone2D::get_auto_configure_joint() const {
	return auto_configure_joint;
}

void PhysicalBone2D::set_auto_configure_joint(bool p_auto_configure) {
	auto_configure_joint = p_auto_configure;
	_auto_configure_joint();
}

void PhysicalBone2D::set_simulate_physics(bool p_simulate) {
	if (p_simulate == simulate_physics) {
		return;
	}
	simulate_physics = p_simulate;

	if (simulate_physics) {
		_start_physics_simulation();
	} else {
		_stop_physics_simulation();
	}
}

bool PhysicalBone2D::get_simulate_physics() const {
	return simulate_physics;
}

bool PhysicalBone2D::is_simulating_physics() const {
	return _internal_simulate_physics;
}

void PhysicalBone2D::set_bone2d_nodepath(const EntityPath &p_nodepath) {
	bone2d_nodepath = p_nodepath;
	notify_property_list_changed();
}

EntityPath PhysicalBone2D::get_bone2d_nodepath() const {
	return bone2d_nodepath;
}

void PhysicalBone2D::set_bone2d_index(int p_bone_idx) {
	ERR_FAIL_COND_MSG(p_bone_idx < 0, "Bone index is out of range: The index is too low!");

	if (!is_inside_stage()) {
		bone2d_index = p_bone_idx;
		return;
	}

	if (parent_skeleton) {
		ERR_FAIL_INDEX_MSG(p_bone_idx, parent_skeleton->get_bone_count(), "Passed-in Bone index is out of range!");
		bone2d_index = p_bone_idx;

		bone2d_nodepath = get_path_to(parent_skeleton->get_bone(bone2d_index));
	} else {
		WARN_PRINT("Cannot verify bone index...");
		bone2d_index = p_bone_idx;
	}

	notify_property_list_changed();
}

int PhysicalBone2D::get_bone2d_index() const {
	return bone2d_index;
}

void PhysicalBone2D::set_follow_bone_when_simulating(bool p_follow_bone) {
	follow_bone_when_simulating = p_follow_bone;

	if (_internal_simulate_physics) {
		_stop_physics_simulation();
		_start_physics_simulation();
	}
}

bool PhysicalBone2D::get_follow_bone_when_simulating() const {
	return follow_bone_when_simulating;
}

void PhysicalBone2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_joint"), &PhysicalBone2D::get_joint);
	ClassRegistry::bind_method(D_METHOD("get_auto_configure_joint"), &PhysicalBone2D::get_auto_configure_joint);
	ClassRegistry::bind_method(D_METHOD("set_auto_configure_joint", "auto_configure_joint"), &PhysicalBone2D::set_auto_configure_joint);

	ClassRegistry::bind_method(D_METHOD("set_simulate_physics", "simulate_physics"), &PhysicalBone2D::set_simulate_physics);
	ClassRegistry::bind_method(D_METHOD("get_simulate_physics"), &PhysicalBone2D::get_simulate_physics);
	ClassRegistry::bind_method(D_METHOD("is_simulating_physics"), &PhysicalBone2D::is_simulating_physics);

	ClassRegistry::bind_method(D_METHOD("set_bone2d_nodepath", "nodepath"), &PhysicalBone2D::set_bone2d_nodepath);
	ClassRegistry::bind_method(D_METHOD("get_bone2d_nodepath"), &PhysicalBone2D::get_bone2d_nodepath);
	ClassRegistry::bind_method(D_METHOD("set_bone2d_index", "bone_index"), &PhysicalBone2D::set_bone2d_index);
	ClassRegistry::bind_method(D_METHOD("get_bone2d_index"), &PhysicalBone2D::get_bone2d_index);
	ClassRegistry::bind_method(D_METHOD("set_follow_bone_when_simulating", "follow_bone"), &PhysicalBone2D::set_follow_bone_when_simulating);
	ClassRegistry::bind_method(D_METHOD("get_follow_bone_when_simulating"), &PhysicalBone2D::get_follow_bone_when_simulating);

	ADD_PROPERTY(PropertyInfo(Variant::ENTITY_PATH, "bone2d_nodepath", PROPERTY_HINT_ENTITY_PATH_VALID_TYPES, "Bone2D"), "set_bone2d_nodepath", "get_bone2d_nodepath");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "bone2d_index", PROPERTY_HINT_RANGE, "-1, 1000, 1"), "set_bone2d_index", "get_bone2d_index");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "auto_configure_joint"), "set_auto_configure_joint", "get_auto_configure_joint");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "simulate_physics"), "set_simulate_physics", "get_simulate_physics");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "follow_bone_when_simulating"), "set_follow_bone_when_simulating", "get_follow_bone_when_simulating");
}

PhysicalBone2D::PhysicalBone2D() {
	// Stop the RigidBody from executing its force integration.
	PhysicsSystem2D::get_singleton()->body_set_collision_layer(get_rid(), 0);
	PhysicsSystem2D::get_singleton()->body_set_collision_mask(get_rid(), 0);
	PhysicsSystem2D::get_singleton()->body_set_mode(get_rid(), PS2DE::BodyMode::BODY_MODE_STATIC);

	child_joint = nullptr;
}

PhysicalBone2D::~PhysicalBone2D() {
}
