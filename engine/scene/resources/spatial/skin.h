/**************************************************************************/
/*  skin.h                                                                */

#pragma once

#include "core/io/resource.h"

class Skin : public Resource {
	ORBLIT_CLASS(Skin, Resource)

	struct Bind {
		int bone = -1;
		StringName name;
		Transform3D pose;
	};

	Vector<Bind> binds;

	Bind *binds_ptr = nullptr;
	int bind_count = 0;

protected:
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;

	virtual void reset_state() override;
	static void register_members();

public:
	void set_bind_count(int p_size);
	inline int get_bind_count() const { return bind_count; }

	void add_bind(int p_bone, const Transform3D &p_pose);
	void add_named_bind(const String &p_name, const Transform3D &p_pose);

	void set_bind_bone(int p_index, int p_bone);
	void set_bind_pose(int p_index, const Transform3D &p_pose);
	void set_bind_name(int p_index, const StringName &p_name);

	inline int get_bind_bone(int p_index) const {
		ERR_FAIL_INDEX_V(p_index, bind_count, -1);
		return binds_ptr[p_index].bone;
	}

	inline StringName get_bind_name(int p_index) const {
		ERR_FAIL_INDEX_V(p_index, bind_count, StringName());
		return binds_ptr[p_index].name;
	}

	inline Transform3D get_bind_pose(int p_index) const {
		ERR_FAIL_INDEX_V(p_index, bind_count, Transform3D());
		return binds_ptr[p_index].pose;
	}

	void clear_binds();

	Skin();
};
