/**************************************************************************/
/*  modifier_bone_target_spatial.cpp                                           */

#include "modifier_bone_target_spatial.h"

#include "core/config/engine.h"
#include "core/object/class_registry.h"
#include "scene/stage/stage.h"

void ModifierBoneTarget3D::_validate_bone_names() {
	// Prior bone name.
	if (!bone_name.is_empty()) {
		set_bone_name(bone_name);
	} else if (bone != -1) {
		set_bone(bone);
	}
}

PackedStringArray ModifierBoneTarget3D::get_configuration_warnings() const {
	PackedStringArray warnings = SkeletonModifier3D::get_configuration_warnings();

	if (Stage::is_fti_enabled_in_project() && is_physics_interpolated()) {
		warnings.push_back(RTR("ModifierBoneTarget3D should have physics_interpolation_mode set to OFF in order to avoid jitter."));
	}

	return warnings;
}

void ModifierBoneTarget3D::set_bone_name(const String &p_bone_name) {
	bone_name = p_bone_name;
	Skeleton3D *sk = get_skeleton();
	if (sk) {
		set_bone(sk->find_bone(bone_name));
	}
}

String ModifierBoneTarget3D::get_bone_name() const {
	return bone_name;
}

void ModifierBoneTarget3D::set_bone(int p_bone) {
	bone = p_bone;
	Skeleton3D *sk = get_skeleton();
	if (sk) {
		if (bone <= -1 || bone >= sk->get_bone_count()) {
			WARN_PRINT_ED("Bone index '" + itos(p_bone) + "' is out of range!");
			bone = -1;
		} else {
			bone_name = sk->get_bone_name(bone);
		}
	}
}

int ModifierBoneTarget3D::get_bone() const {
	return bone;
}

void ModifierBoneTarget3D::_validate_property(PropertyInfo &p_property) const {
	if (p_property.name == "influence") {
		p_property.usage = PROPERTY_USAGE_READ_ONLY;
	}
	if (!Engine::get_singleton()->is_editor_hint()) {
		return;
	}
	if (p_property.name == "bone_name") {
		Skeleton3D *skeleton = get_skeleton();
		if (skeleton) {
			p_property.hint = PROPERTY_HINT_ENUM_SUGGESTION;
			p_property.hint_string = skeleton->get_concatenated_bone_names();
		} else {
			p_property.hint = PROPERTY_HINT_NONE;
			p_property.hint_string = "";
		}
	}
}

void ModifierBoneTarget3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_bone_name", "bone_name"), &ModifierBoneTarget3D::set_bone_name);
	ClassRegistry::bind_method(D_METHOD("get_bone_name"), &ModifierBoneTarget3D::get_bone_name);
	ClassRegistry::bind_method(D_METHOD("set_bone", "bone"), &ModifierBoneTarget3D::set_bone);
	ClassRegistry::bind_method(D_METHOD("get_bone"), &ModifierBoneTarget3D::get_bone);

	ADD_PROPERTY(PropertyInfo(Variant::STRING, "bone_name", PROPERTY_HINT_ENUM_SUGGESTION, ""), "set_bone_name", "get_bone_name");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "bone", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NO_EDITOR), "set_bone", "get_bone");
}

void ModifierBoneTarget3D::_process_modification(double p_delta) {
	Skeleton3D *skeleton = get_skeleton();
	if (!skeleton || bone < 0 || bone >= skeleton->get_bone_count()) {
		return;
	}

	set_transform(skeleton->get_bone_global_pose(bone));
}

ModifierBoneTarget3D::ModifierBoneTarget3D() {
	set_physics_interpolation_mode(PHYSICS_INTERPOLATION_MODE_OFF);
}
