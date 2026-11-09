/**************************************************************************/
/*  spring_bone_collision_spatial.h                                            */

#pragma once

#include "scene/spatial/skeleton_spatial.h"

class SpringBoneCollision3D : public Entity3D {
	ORBLIT_CLASS(SpringBoneCollision3D, Entity3D);

	String bone_name;
	int bone = -1;

	Vector3 position_offset;
	Quaternion rotation_offset;

	void _validate_bone_name();

protected:
	PackedStringArray get_configuration_warnings() const override;

	void _validate_property(PropertyInfo &p_property) const;
	void _notification(int p_what);
	static void register_members();

	virtual Vector3 _collide(const Transform3D &p_center, float p_bone_radius, float p_bone_length, const Vector3 &p_current) const;

public:
	Skeleton3D *get_skeleton() const;

	void set_bone_name(const String &p_name);
	String get_bone_name() const;
	void set_bone(int p_bone);
	int get_bone() const;

	void set_position_offset(const Vector3 &p_offset);
	Vector3 get_position_offset() const;
	void set_rotation_offset(const Quaternion &p_offset);
	Quaternion get_rotation_offset() const;

	void sync_pose();
	Transform3D get_transform_from_skeleton(const Transform3D &p_center) const;

	Vector3 collide(const Transform3D &p_center, float p_bone_radius, float p_bone_length, const Vector3 &p_current) const;

	SpringBoneCollision3D();
};
