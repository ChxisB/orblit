/**************************************************************************/
/*  skeleton_modification_planar_jiggle.h                                     */

#pragma once

#include "scene/planar/skeleton_planar.h"
#include "scene/resources/planar/skeleton/skeleton_modification_planar.h"

///////////////////////////////////////
// SkeletonModification2DJIGGLE
///////////////////////////////////////

class SkeletonModification2DJiggle : public SkeletonModification2D {
	ORBLIT_CLASS(SkeletonModification2DJiggle, SkeletonModification2D);

private:
	struct Jiggle_Joint_Data2D {
		int bone_idx = -1;
		EntityPath bone2d_node;
		ObjectID bone2d_node_cache;

		bool override_defaults = false;
		float stiffness = 3;
		float mass = 0.75;
		float damping = 0.75;
		bool use_gravity = false;
		Vector2 gravity = Vector2(0, 6.0);

		Vector2 force = Vector2(0, 0);
		Vector2 acceleration = Vector2(0, 0);
		Vector2 velocity = Vector2(0, 0);
		Vector2 last_position = Vector2(0, 0);
		Vector2 dynamic_position = Vector2(0, 0);

		Vector2 last_noncollision_position = Vector2(0, 0);
	};

	Vector<Jiggle_Joint_Data2D> jiggle_data_chain;

	EntityPath target_node;
	ObjectID target_node_cache;
	void update_target_cache();

	float stiffness = 3;
	float mass = 0.75;
	float damping = 0.75;
	bool use_gravity = false;
	Vector2 gravity = Vector2(0, 6);

	bool use_colliders = false;
	uint32_t collision_mask = 1;

	void jiggle_joint_update_bone2d_cache(int p_joint_idx);
	void _execute_jiggle_joint(int p_joint_idx, Entity2D *p_target, float p_delta);
	void _update_jiggle_joint_data();

protected:
	static void register_members();
	bool _set(const StringName &p_path, const Variant &p_value);
	bool _get(const StringName &p_path, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;

public:
	void reset();
	void _execute(float p_delta) override;
	void _setup_modification(SkeletonModificationStack2D *p_stack) override;

	void set_target_node(const EntityPath &p_target_node);
	EntityPath get_target_node() const;

	void set_stiffness(float p_stiffness);
	float get_stiffness() const;
	void set_mass(float p_mass);
	float get_mass() const;
	void set_damping(float p_damping);
	float get_damping() const;
	void set_use_gravity(bool p_use_gravity);
	bool get_use_gravity() const;
	void set_gravity(Vector2 p_gravity);
	Vector2 get_gravity() const;

	void set_use_colliders(bool p_use_colliders);
	bool get_use_colliders() const;
	void set_collision_mask(int p_mask);
	int get_collision_mask() const;

	int get_jiggle_data_chain_length();
	void set_jiggle_data_chain_length(int p_new_length);

	void set_jiggle_joint_bone2d_node(int p_joint_idx, const EntityPath &p_target_node);
	EntityPath get_jiggle_joint_bone2d_node(int p_joint_idx) const;
	void set_jiggle_joint_bone_index(int p_joint_idx, int p_bone_idx);
	int get_jiggle_joint_bone_index(int p_joint_idx) const;

	void set_jiggle_joint_override(int p_joint_idx, bool p_override);
	bool get_jiggle_joint_override(int p_joint_idx) const;
	void set_jiggle_joint_stiffness(int p_joint_idx, float p_stiffness);
	float get_jiggle_joint_stiffness(int p_joint_idx) const;
	void set_jiggle_joint_mass(int p_joint_idx, float p_mass);
	float get_jiggle_joint_mass(int p_joint_idx) const;
	void set_jiggle_joint_damping(int p_joint_idx, float p_damping);
	float get_jiggle_joint_damping(int p_joint_idx) const;
	void set_jiggle_joint_use_gravity(int p_joint_idx, bool p_use_gravity);
	bool get_jiggle_joint_use_gravity(int p_joint_idx) const;
	void set_jiggle_joint_gravity(int p_joint_idx, Vector2 p_gravity);
	Vector2 get_jiggle_joint_gravity(int p_joint_idx) const;

	SkeletonModification2DJiggle();
	~SkeletonModification2DJiggle();
};
