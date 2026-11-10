/**************************************************************************/
/*  bone_attachment_spatial.h                                                  */

#pragma once

#include "scene/spatial/skeleton_spatial.h"

class BoneAttachment3D : public Entity3D {
	ORBLIT_CLASS(BoneAttachment3D, Entity3D);

	bool bound = false;
	String bone_name;
	int bone_idx = -1;

	bool override_pose = false;
	bool _override_dirty = false;
	bool overriding = false;

	bool use_external_skeleton = false;
	EntityPath external_skeleton_node;
	ObjectID external_skeleton_node_cache;

	void _check_bind();
	void _check_unbind();

	bool updating = false;
	void _transform_changed();
	void _update_external_skeleton_cache();

protected:
	void _validate_property(PropertyInfo &p_property) const;
	void _notification(int p_what);

	static void register_members();

public:
#ifdef TOOLS_ENABLED
	virtual void notify_skeleton_bones_renamed(Entity *p_base_scene, Skeleton3D *p_skeleton, Dictionary p_rename_map);
#endif // TOOLS_ENABLED

	virtual PackedStringArray get_configuration_warnings() const override;

	Skeleton3D *get_skeleton();

	void set_bone_name(const String &p_name);
	String get_bone_name() const;

	void set_bone_idx(const int &p_idx);
	int get_bone_idx() const;

	void set_override_pose(bool p_override_pose);
	bool get_override_pose() const;

	void set_use_external_skeleton(bool p_use_external_skeleton);
	bool get_use_external_skeleton() const;
	void set_external_skeleton(EntityPath p_external_skeleton);
	EntityPath get_external_skeleton() const;

	virtual void on_skeleton_update();

#ifdef TOOLS_ENABLED
	virtual void notify_rebind_required();
#endif

	BoneAttachment3D();
};
