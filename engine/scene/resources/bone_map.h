/**************************************************************************/
/*  bone_map.h                                                            */

#pragma once

#include "scene/resources/skeleton_profile.h"

class BoneMap : public Resource {
	ORBLIT_CLASS(BoneMap, Resource);

	Ref<SkeletonProfile> profile;
	HashMap<StringName, StringName> bone_map;

	void _update_profile();
	void _validate_bone_map();

protected:
	bool _get(const StringName &p_path, Variant &r_ret) const;
	bool _set(const StringName &p_path, const Variant &p_value);
	void _validate_property(PropertyInfo &p_property) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;
	static void register_members();

public:
	Ref<SkeletonProfile> get_profile() const;
	void set_profile(const Ref<SkeletonProfile> &p_profile);

	int get_skeleton_bone_name_count(const StringName &p_skeleton_bone_name) const;

	StringName get_skeleton_bone_name(const StringName &p_profile_bone_name) const;
	void set_skeleton_bone_name(const StringName &p_profile_bone_name, const StringName &p_skeleton_bone_name);
	void _set_skeleton_bone_name(const StringName &p_profile_bone_name, const StringName &p_skeleton_bone_name); // Avoid to emit signal for editor.

	StringName find_profile_bone_name(const StringName &p_skeleton_bone_name) const;

	BoneMap();
	~BoneMap();
};
