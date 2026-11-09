/**************************************************************************/
/*  physical_bone_planar.h                                                    */

#pragma once

#include "scene/planar/physics/rigid_body_planar.h"
#include "scene/planar/skeleton_planar.h"

class Joint2D;

class PhysicalBone2D : public RigidBody2D {
	ORBLIT_CLASS(PhysicalBone2D, RigidBody2D);

protected:
	void _notification(int p_what);
	static void register_members();

private:
	Skeleton2D *parent_skeleton = nullptr;
	int bone2d_index = -1;
	EntityPath bone2d_nodepath;
	bool follow_bone_when_simulating = false;

	Joint2D *child_joint = nullptr;
	bool auto_configure_joint = true;

	bool simulate_physics = false;
	bool _internal_simulate_physics = false;

	void _find_skeleton_parent();
	void _find_joint_child();
	void _auto_configure_joint();

	void _start_physics_simulation();
	void _stop_physics_simulation();
	void _position_at_bone2d();

public:
	Joint2D *get_joint() const;
	bool get_auto_configure_joint() const;
	void set_auto_configure_joint(bool p_auto_configure);

	void set_simulate_physics(bool p_simulate);
	bool get_simulate_physics() const;
	bool is_simulating_physics() const;

	void set_bone2d_nodepath(const EntityPath &p_nodepath);
	EntityPath get_bone2d_nodepath() const;
	void set_bone2d_index(int p_bone_idx);
	int get_bone2d_index() const;
	void set_follow_bone_when_simulating(bool p_follow);
	bool get_follow_bone_when_simulating() const;

	PackedStringArray get_configuration_warnings() const override;

	PhysicalBone2D();
	~PhysicalBone2D();
};
