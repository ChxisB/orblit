/**************************************************************************/
/*  gltf_skin.cpp                                                         */

#include "gltf_skin.h"

#include "../gltf_template_convert.h"

#include "core/object/class_registry.h"
#include "core/variant/typed_array.h"
#include "scene/resources/spatial/skin.h"

void GLTFSkin::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_skin_root"), &GLTFSkin::get_skin_root);
	ClassRegistry::bind_method(D_METHOD("set_skin_root", "skin_root"), &GLTFSkin::set_skin_root);
	ClassRegistry::bind_method(D_METHOD("get_joints_original"), &GLTFSkin::get_joints_original);
	ClassRegistry::bind_method(D_METHOD("set_joints_original", "joints_original"), &GLTFSkin::set_joints_original);
	ClassRegistry::bind_method(D_METHOD("get_inverse_binds"), &GLTFSkin::get_inverse_binds);
	ClassRegistry::bind_method(D_METHOD("set_inverse_binds", "inverse_binds"), &GLTFSkin::set_inverse_binds);
	ClassRegistry::bind_method(D_METHOD("get_joints"), &GLTFSkin::get_joints);
	ClassRegistry::bind_method(D_METHOD("set_joints", "joints"), &GLTFSkin::set_joints);
	ClassRegistry::bind_method(D_METHOD("get_non_joints"), &GLTFSkin::get_non_joints);
	ClassRegistry::bind_method(D_METHOD("set_non_joints", "non_joints"), &GLTFSkin::set_non_joints);
	ClassRegistry::bind_method(D_METHOD("get_roots"), &GLTFSkin::get_roots);
	ClassRegistry::bind_method(D_METHOD("set_roots", "roots"), &GLTFSkin::set_roots);
	ClassRegistry::bind_method(D_METHOD("get_skeleton"), &GLTFSkin::get_skeleton);
	ClassRegistry::bind_method(D_METHOD("set_skeleton", "skeleton"), &GLTFSkin::set_skeleton);
	ClassRegistry::bind_method(D_METHOD("get_joint_i_to_bone_i"), &GLTFSkin::get_joint_i_to_bone_i);
	ClassRegistry::bind_method(D_METHOD("set_joint_i_to_bone_i", "joint_i_to_bone_i"), &GLTFSkin::set_joint_i_to_bone_i);
	ClassRegistry::bind_method(D_METHOD("get_joint_i_to_name"), &GLTFSkin::get_joint_i_to_name);
	ClassRegistry::bind_method(D_METHOD("set_joint_i_to_name", "joint_i_to_name"), &GLTFSkin::set_joint_i_to_name);
	ClassRegistry::bind_method(D_METHOD("get_orblit_skin"), &GLTFSkin::get_orblit_skin);
	ClassRegistry::bind_method(D_METHOD("set_orblit_skin", "orblit_skin"), &GLTFSkin::set_orblit_skin);

	ADD_PROPERTY(PropertyInfo(Variant::INT, "skin_root"), "set_skin_root", "get_skin_root"); // GLTFNodeIndex
	ADD_PROPERTY(PropertyInfo(Variant::PACKED_INT32_ARRAY, "joints_original"), "set_joints_original", "get_joints_original"); // Vector<GLTFNodeIndex>
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "inverse_binds", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_STORAGE | PROPERTY_USAGE_INTERNAL), "set_inverse_binds", "get_inverse_binds"); // Vector<Transform3D>
	ADD_PROPERTY(PropertyInfo(Variant::PACKED_INT32_ARRAY, "joints"), "set_joints", "get_joints"); // Vector<GLTFNodeIndex>
	ADD_PROPERTY(PropertyInfo(Variant::PACKED_INT32_ARRAY, "non_joints"), "set_non_joints", "get_non_joints"); // Vector<GLTFNodeIndex>
	ADD_PROPERTY(PropertyInfo(Variant::PACKED_INT32_ARRAY, "roots"), "set_roots", "get_roots"); // Vector<GLTFNodeIndex>
	ADD_PROPERTY(PropertyInfo(Variant::INT, "skeleton"), "set_skeleton", "get_skeleton"); // int
	ADD_PROPERTY(PropertyInfo(Variant::DICTIONARY, "joint_i_to_bone_i", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_STORAGE | PROPERTY_USAGE_INTERNAL), "set_joint_i_to_bone_i", "get_joint_i_to_bone_i"); // RBMap<int,
	ADD_PROPERTY(PropertyInfo(Variant::DICTIONARY, "joint_i_to_name", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_STORAGE | PROPERTY_USAGE_INTERNAL), "set_joint_i_to_name", "get_joint_i_to_name"); // RBMap<int,
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "orblit_skin", PROPERTY_HINT_RESOURCE_TYPE, Skin::get_static_class_name()), "set_orblit_skin", "get_orblit_skin"); // Ref<Skin>
}

GLTFNodeIndex GLTFSkin::get_skin_root() {
	return skin_root;
}

void GLTFSkin::set_skin_root(GLTFNodeIndex p_skin_root) {
	skin_root = p_skin_root;
}

Vector<GLTFNodeIndex> GLTFSkin::get_joints_original() {
	return joints_original;
}

void GLTFSkin::set_joints_original(const Vector<GLTFNodeIndex> &p_joints_original) {
	joints_original = Vector<GLTFNodeIndex>(p_joints_original);
}

TypedArray<Transform3D> GLTFSkin::get_inverse_binds() {
	return GLTFTemplateConvert::to_array(inverse_binds);
}

void GLTFSkin::set_inverse_binds(const TypedArray<Transform3D> &p_inverse_binds) {
	GLTFTemplateConvert::set_from_array(inverse_binds, p_inverse_binds);
}

Vector<GLTFNodeIndex> GLTFSkin::get_joints() {
	return joints;
}

void GLTFSkin::set_joints(const Vector<GLTFNodeIndex> &p_joints) {
	joints = Vector<GLTFNodeIndex>(p_joints);
}

Vector<GLTFNodeIndex> GLTFSkin::get_non_joints() {
	return non_joints;
}

void GLTFSkin::set_non_joints(const Vector<GLTFNodeIndex> &p_non_joints) {
	non_joints = Vector<GLTFNodeIndex>(p_non_joints);
}

Vector<GLTFNodeIndex> GLTFSkin::get_roots() {
	return roots;
}

void GLTFSkin::set_roots(const Vector<GLTFNodeIndex> &p_roots) {
	roots = Vector<GLTFNodeIndex>(p_roots);
}

int GLTFSkin::get_skeleton() {
	return skeleton;
}

void GLTFSkin::set_skeleton(int p_skeleton) {
	skeleton = p_skeleton;
}

Dictionary GLTFSkin::get_joint_i_to_bone_i() {
	return GLTFTemplateConvert::to_dictionary(joint_i_to_bone_i);
}

void GLTFSkin::set_joint_i_to_bone_i(const Dictionary &p_joint_i_to_bone_i) {
	GLTFTemplateConvert::set_from_dictionary(joint_i_to_bone_i, p_joint_i_to_bone_i);
}

Dictionary GLTFSkin::get_joint_i_to_name() {
	Dictionary ret;
	HashMap<int, StringName>::Iterator elem = joint_i_to_name.begin();
	while (elem) {
		ret[elem->key] = String(elem->value);
		++elem;
	}
	return ret;
}

void GLTFSkin::set_joint_i_to_name(const Dictionary &p_joint_i_to_name) {
	joint_i_to_name = HashMap<int, StringName>();
	for (const KeyValue<Variant, Variant> &kv : p_joint_i_to_name) {
		joint_i_to_name[kv.key] = kv.value;
	}
}

Ref<Skin> GLTFSkin::get_orblit_skin() {
	return orblit_skin;
}

void GLTFSkin::set_orblit_skin(const Ref<Skin> &p_orblit_skin) {
	orblit_skin = p_orblit_skin;
}

Error GLTFSkin::from_dictionary(const Dictionary &dict) {
	ERR_FAIL_COND_V(!dict.has("skin_root"), ERR_INVALID_DATA);
	skin_root = dict["skin_root"];

	ERR_FAIL_COND_V(!dict.has("joints_original"), ERR_INVALID_DATA);
	Array joints_original_array = dict["joints_original"];
	joints_original.clear();
	for (int i = 0; i < joints_original_array.size(); ++i) {
		joints_original.push_back(joints_original_array[i]);
	}

	ERR_FAIL_COND_V(!dict.has("inverse_binds"), ERR_INVALID_DATA);
	Array inverse_binds_array = dict["inverse_binds"];
	inverse_binds.clear();
	for (int i = 0; i < inverse_binds_array.size(); ++i) {
		ERR_FAIL_COND_V(inverse_binds_array[i].get_type() != Variant::TRANSFORM3D, ERR_INVALID_DATA);
		inverse_binds.push_back(inverse_binds_array[i]);
	}

	ERR_FAIL_COND_V(!dict.has("joints"), ERR_INVALID_DATA);
	Array joints_array = dict["joints"];
	joints.clear();
	for (int i = 0; i < joints_array.size(); ++i) {
		joints.push_back(joints_array[i]);
	}

	ERR_FAIL_COND_V(!dict.has("non_joints"), ERR_INVALID_DATA);
	Array non_joints_array = dict["non_joints"];
	non_joints.clear();
	for (int i = 0; i < non_joints_array.size(); ++i) {
		non_joints.push_back(non_joints_array[i]);
	}

	ERR_FAIL_COND_V(!dict.has("roots"), ERR_INVALID_DATA);
	Array roots_array = dict["roots"];
	roots.clear();
	for (int i = 0; i < roots_array.size(); ++i) {
		roots.push_back(roots_array[i]);
	}

	ERR_FAIL_COND_V(!dict.has("skeleton"), ERR_INVALID_DATA);
	skeleton = dict["skeleton"];

	ERR_FAIL_COND_V(!dict.has("joint_i_to_bone_i"), ERR_INVALID_DATA);
	Dictionary joint_i_to_bone_i_dict = dict["joint_i_to_bone_i"];
	joint_i_to_bone_i.clear();
	for (const KeyValue<Variant, Variant> &kv : joint_i_to_bone_i_dict) {
		int key = kv.key;
		int value = kv.value;
		joint_i_to_bone_i[key] = value;
	}

	ERR_FAIL_COND_V(!dict.has("joint_i_to_name"), ERR_INVALID_DATA);
	Dictionary joint_i_to_name_dict = dict["joint_i_to_name"];
	joint_i_to_name.clear();
	for (const KeyValue<Variant, Variant> &kv : joint_i_to_name_dict) {
		int key = kv.key;
		StringName value = kv.value;
		joint_i_to_name[key] = value;
	}
	if (dict.has("orblit_skin")) {
		orblit_skin = dict["orblit_skin"];
	}
	return OK;
}

Dictionary GLTFSkin::to_dictionary() {
	Dictionary dict;
	dict["skin_root"] = skin_root;

	Array joints_original_array;
	for (int i = 0; i < joints_original.size(); ++i) {
		joints_original_array.push_back(joints_original[i]);
	}
	dict["joints_original"] = joints_original_array;

	Array inverse_binds_array;
	for (int i = 0; i < inverse_binds.size(); ++i) {
		inverse_binds_array.push_back(inverse_binds[i]);
	}
	dict["inverse_binds"] = inverse_binds_array;

	Array joints_array;
	for (int i = 0; i < joints.size(); ++i) {
		joints_array.push_back(joints[i]);
	}
	dict["joints"] = joints_array;

	Array non_joints_array;
	for (int i = 0; i < non_joints.size(); ++i) {
		non_joints_array.push_back(non_joints[i]);
	}
	dict["non_joints"] = non_joints_array;

	Array roots_array;
	for (int i = 0; i < roots.size(); ++i) {
		roots_array.push_back(roots[i]);
	}
	dict["roots"] = roots_array;

	dict["skeleton"] = skeleton;

	Dictionary joint_i_to_bone_i_dict;
	for (HashMap<int, int>::Iterator E = joint_i_to_bone_i.begin(); E; ++E) {
		joint_i_to_bone_i_dict[E->key] = E->value;
	}
	dict["joint_i_to_bone_i"] = joint_i_to_bone_i_dict;

	Dictionary joint_i_to_name_dict;
	for (HashMap<int, StringName>::Iterator E = joint_i_to_name.begin(); E; ++E) {
		joint_i_to_name_dict[E->key] = E->value;
	}
	dict["joint_i_to_name"] = joint_i_to_name_dict;

	dict["orblit_skin"] = orblit_skin;
	return dict;
}
