/**************************************************************************/
/*  jolt_object_spatial.cpp                                                    */

#include "jolt_object_spatial.h"

#include "../jolt_physics_system_spatial.h"
#include "../spaces/jolt_space_spatial.h"
#include "jolt_group_filter.h"

void JoltObject3D::_remove_from_space() {
	if (!in_space()) {
		return;
	}

	space->remove_object(jolt_body->GetID());
	jolt_body = nullptr;
}

void JoltObject3D::_reset_space() {
	ERR_FAIL_NULL(space);

	_space_changing();
	_remove_from_space();
	_add_to_space();
	_space_changed();
}

void JoltObject3D::_update_object_layer() {
	if (!in_space()) {
		return;
	}

	space->get_body_iface().SetObjectLayer(jolt_body->GetID(), _get_object_layer());
}

void JoltObject3D::_collision_layer_changed() {
	_update_object_layer();
}

void JoltObject3D::_collision_mask_changed() {
	_update_object_layer();
}

JoltObject3D::JoltObject3D(ObjectType p_object_type) :
		object_type(p_object_type) {
}

JoltObject3D::~JoltObject3D() = default;

void JoltObject3D::set_space(JoltSpace3D *p_space) {
	if (space == p_space) {
		return;
	}

	_space_changing();

	if (space != nullptr) {
		_remove_from_space();
	}

	space = p_space;

	if (space != nullptr) {
		_add_to_space();
	}

	_space_changed();
}

void JoltObject3D::set_collision_layer(uint32_t p_layer) {
	if (p_layer == collision_layer) {
		return;
	}

	collision_layer = p_layer;

	_collision_layer_changed();
}

void JoltObject3D::set_collision_mask(uint32_t p_mask) {
	if (p_mask == collision_mask) {
		return;
	}

	collision_mask = p_mask;

	_collision_mask_changed();
}

bool JoltObject3D::can_collide_with(const JoltObject3D &p_other) const {
	return (collision_mask & p_other.get_collision_layer()) != 0;
}

bool JoltObject3D::can_interact_with(const JoltObject3D &p_other) const {
	if (const JoltBody3D *other_body = p_other.as_body()) {
		return can_interact_with(*other_body);
	} else if (const JoltArea3D *other_area = p_other.as_area()) {
		return can_interact_with(*other_area);
	} else if (const JoltSoftBody3D *other_soft_body = p_other.as_soft_body()) {
		return can_interact_with(*other_soft_body);
	} else {
		ERR_FAIL_V_MSG(false, vformat("Unhandled object type: '%d'. This should not happen. Please report this.", p_other.get_type()));
	}
}

String JoltObject3D::to_string() const {
	static const String fallback_name = "<unknown>";

	if (JoltPhysicsSystem3D::get_singleton()->is_on_separate_thread()) {
		return fallback_name; // Calling `Object::to_string` is not thread-safe.
	}

	Object *instance = get_instance_id().is_valid() ? ObjectRegistry::get_instance(get_instance_id()) : nullptr;
	return instance != nullptr ? instance->to_string() : fallback_name;
}
