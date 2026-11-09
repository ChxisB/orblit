/**************************************************************************/
/*  gltf_physics_body.h                                                   */

#pragma once

#include "core/io/resource.h"
#include "scene/spatial/physics/collision_object_spatial.h"

// GLTFPhysicsBody is an intermediary between Orblit's physics body nodes
// and the OMI_physics_body extension.
// https://github.com/omigroup/gltf-extensions/tree/main/extensions/2.0/OMI_physics_body

class GLTFPhysicsBody : public Resource {
	ORBLIT_CLASS(GLTFPhysicsBody, Resource)

public:
	// These values map to Orblit's physics body types.
	// When importing, the body type will be set to the closest match, and
	// user code can change this to make Orblit generate a different node type.
	// When exporting, this will be squashed down to one of "static",
	// "kinematic", or "dynamic" motion types, or the "trigger" property.
	enum class PhysicsBodyType {
		STATIC,
		ANIMATABLE,
		CHARACTER,
		RIGID,
		VEHICLE,
		TRIGGER,
	};

protected:
	static void register_members();

private:
	PhysicsBodyType body_type = PhysicsBodyType::RIGID;
	real_t mass = 1.0;
	Vector3 linear_velocity;
	Vector3 angular_velocity;
	Vector3 center_of_mass;
	Vector3 inertia_diagonal;
	Quaternion inertia_orientation;

public:
	String get_body_type() const;
	void set_body_type(const String &p_body_type);

	PhysicsBodyType get_physics_body_type() const;
	void set_physics_body_type(PhysicsBodyType p_body_type);

	real_t get_mass() const;
	void set_mass(real_t p_mass);

	Vector3 get_linear_velocity() const;
	void set_linear_velocity(const Vector3 &p_linear_velocity);

	Vector3 get_angular_velocity() const;
	void set_angular_velocity(const Vector3 &p_angular_velocity);

	Vector3 get_center_of_mass() const;
	void set_center_of_mass(const Vector3 &p_center_of_mass);

	Vector3 get_inertia_diagonal() const;
	void set_inertia_diagonal(const Vector3 &p_inertia_diagonal);

	Quaternion get_inertia_orientation() const;
	void set_inertia_orientation(const Quaternion &p_inertia_orientation);

	static Ref<GLTFPhysicsBody> from_node(const CollisionObject3D *p_body_node);
	CollisionObject3D *to_node() const;

	static Ref<GLTFPhysicsBody> from_dictionary(const Dictionary &p_dictionary);
	Dictionary to_dictionary() const;
};
