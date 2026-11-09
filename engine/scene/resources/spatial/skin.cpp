/**************************************************************************/
/*  skin.cpp                                                              */

#include "skin.h"

#include "core/object/class_registry.h"

void Skin::set_bind_count(int p_size) {
	ERR_FAIL_COND(p_size < 0);
	binds.resize(p_size);
	binds_ptr = binds.ptrw();
	bind_count = p_size;
	emit_changed();
}

void Skin::add_bind(int p_bone, const Transform3D &p_pose) {
	uint32_t index = bind_count;
	set_bind_count(bind_count + 1);
	set_bind_bone(index, p_bone);
	set_bind_pose(index, p_pose);
}

void Skin::add_named_bind(const String &p_name, const Transform3D &p_pose) {
	uint32_t index = bind_count;
	set_bind_count(bind_count + 1);
	set_bind_name(index, p_name);
	set_bind_pose(index, p_pose);
}

void Skin::set_bind_name(int p_index, const StringName &p_name) {
	ERR_FAIL_INDEX(p_index, bind_count);
	bool notify_change = (binds_ptr[p_index].name != StringName()) != (p_name != StringName());
	binds_ptr[p_index].name = p_name;
	emit_changed();
	if (notify_change) {
		notify_property_list_changed();
	}
}

void Skin::set_bind_bone(int p_index, int p_bone) {
	ERR_FAIL_INDEX(p_index, bind_count);
	binds_ptr[p_index].bone = p_bone;
	emit_changed();
}

void Skin::set_bind_pose(int p_index, const Transform3D &p_pose) {
	ERR_FAIL_INDEX(p_index, bind_count);
	binds_ptr[p_index].pose = p_pose;
	emit_changed();
}

void Skin::clear_binds() {
	binds.clear();
	binds_ptr = nullptr;
	bind_count = 0;
	emit_changed();
}

void Skin::reset_state() {
	clear_binds();
}

bool Skin::_set(const StringName &p_name, const Variant &p_value) {
	String prop_name = p_name;
	if (prop_name == "bind_count") {
		set_bind_count(p_value);
		return true;
	} else if (prop_name.begins_with("bind/")) {
		int index = prop_name.get_slicec('/', 1).to_int();
		String what = prop_name.get_slicec('/', 2);
		if (what == "bone") {
			set_bind_bone(index, p_value);
			return true;
		} else if (what == "name") {
			set_bind_name(index, p_value);
			return true;
		} else if (what == "pose") {
			set_bind_pose(index, p_value);
			return true;
		}
	}
	return false;
}

bool Skin::_get(const StringName &p_name, Variant &r_ret) const {
	String prop_name = p_name;
	if (prop_name == "bind_count") {
		r_ret = get_bind_count();
		return true;
	} else if (prop_name.begins_with("bind/")) {
		int index = prop_name.get_slicec('/', 1).to_int();
		String what = prop_name.get_slicec('/', 2);
		if (what == "bone") {
			r_ret = get_bind_bone(index);
			return true;
		} else if (what == "name") {
			r_ret = get_bind_name(index);
			return true;
		} else if (what == "pose") {
			r_ret = get_bind_pose(index);
			return true;
		}
	}
	return false;
}

void Skin::_get_property_list(List<PropertyInfo> *p_list) const {
	p_list->push_back(PropertyInfo(Variant::INT, PNAME("bind_count"), PROPERTY_HINT_RANGE, "0,16384,1,or_greater"));
	for (int i = 0; i < get_bind_count(); i++) {
		const String prefix = vformat("%s/%d/", PNAME("bind"), i);
		p_list->push_back(PropertyInfo(Variant::STRING_NAME, prefix + PNAME("name")));
		p_list->push_back(PropertyInfo(Variant::INT, prefix + PNAME("bone"), PROPERTY_HINT_RANGE, "0,16384,1,or_greater", get_bind_name(i) != StringName() ? PROPERTY_USAGE_NO_EDITOR : PROPERTY_USAGE_DEFAULT));
		p_list->push_back(PropertyInfo(Variant::TRANSFORM3D, prefix + PNAME("pose")));
	}
}

void Skin::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_bind_count", "bind_count"), &Skin::set_bind_count);
	ClassRegistry::bind_method(D_METHOD("get_bind_count"), &Skin::get_bind_count);

	ClassRegistry::bind_method(D_METHOD("add_bind", "bone", "pose"), &Skin::add_bind);
	ClassRegistry::bind_method(D_METHOD("add_named_bind", "name", "pose"), &Skin::add_named_bind);

	ClassRegistry::bind_method(D_METHOD("set_bind_pose", "bind_index", "pose"), &Skin::set_bind_pose);
	ClassRegistry::bind_method(D_METHOD("get_bind_pose", "bind_index"), &Skin::get_bind_pose);

	ClassRegistry::bind_method(D_METHOD("set_bind_name", "bind_index", "name"), &Skin::set_bind_name);
	ClassRegistry::bind_method(D_METHOD("get_bind_name", "bind_index"), &Skin::get_bind_name);

	ClassRegistry::bind_method(D_METHOD("set_bind_bone", "bind_index", "bone"), &Skin::set_bind_bone);
	ClassRegistry::bind_method(D_METHOD("get_bind_bone", "bind_index"), &Skin::get_bind_bone);

	ClassRegistry::bind_method(D_METHOD("clear_binds"), &Skin::clear_binds);
}

Skin::Skin() {
}
