/**************************************************************************/
/*  gltf_skeleton.cpp                                                     */

#include "gltf_skeleton.h"

#include "../gltf_template_convert.h"

#include "core/object/class_registry.h"
#include "scene/spatial/bone_attachment_spatial.h"

void GLTFSkeleton::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_joints"), &GLTFSkeleton::get_joints);
	ClassRegistry::bind_method(D_METHOD("set_joints", "joints"), &GLTFSkeleton::set_joints);
	ClassRegistry::bind_method(D_METHOD("get_roots"), &GLTFSkeleton::get_roots);
	ClassRegistry::bind_method(D_METHOD("set_roots", "roots"), &GLTFSkeleton::set_roots);
	ClassRegistry::bind_method(D_METHOD("get_orblit_skeleton"), &GLTFSkeleton::get_orblit_skeleton);
	ClassRegistry::bind_method(D_METHOD("get_unique_names"), &GLTFSkeleton::get_unique_names);
	ClassRegistry::bind_method(D_METHOD("set_unique_names", "unique_names"), &GLTFSkeleton::set_unique_names);
	ClassRegistry::bind_method(D_METHOD("get_orblit_bone_node"), &GLTFSkeleton::get_orblit_bone_node);
	ClassRegistry::bind_method(D_METHOD("set_orblit_bone_node", "orblit_bone_node"), &GLTFSkeleton::set_orblit_bone_node);
	ClassRegistry::bind_method(D_METHOD("get_bone_attachment_count"), &GLTFSkeleton::get_bone_attachment_count);
	ClassRegistry::bind_method(D_METHOD("get_bone_attachment", "idx"), &GLTFSkeleton::get_bone_attachment);

	ADD_PROPERTY(PropertyInfo(Variant::PACKED_INT32_ARRAY, "joints"), "set_joints", "get_joints"); // Vector<GLTFNodeIndex>
	ADD_PROPERTY(PropertyInfo(Variant::PACKED_INT32_ARRAY, "roots"), "set_roots", "get_roots"); // Vector<GLTFNodeIndex>
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "unique_names", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_STORAGE | PROPERTY_USAGE_INTERNAL | PROPERTY_USAGE_EDITOR), "set_unique_names", "get_unique_names"); // Set<String>
	ADD_PROPERTY(PropertyInfo(Variant::DICTIONARY, "orblit_bone_node", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_STORAGE | PROPERTY_USAGE_INTERNAL | PROPERTY_USAGE_EDITOR), "set_orblit_bone_node", "get_orblit_bone_node"); // RBMap<int32_t,
}

Vector<GLTFNodeIndex> GLTFSkeleton::get_joints() {
	return Vector<GLTFNodeIndex>(joints);
}

void GLTFSkeleton::set_joints(const Vector<GLTFNodeIndex> &p_joints) {
	joints = Vector<GLTFNodeIndex>(p_joints);
}

Vector<GLTFNodeIndex> GLTFSkeleton::get_roots() {
	return Vector<GLTFNodeIndex>(roots);
}

void GLTFSkeleton::set_roots(const Vector<GLTFNodeIndex> &p_roots) {
	roots = Vector<GLTFNodeIndex>(p_roots);
}

Skeleton3D *GLTFSkeleton::get_orblit_skeleton() {
	return orblit_skeleton;
}

TypedArray<String> GLTFSkeleton::get_unique_names() {
	return GLTFTemplateConvert::to_array(unique_names);
}

void GLTFSkeleton::set_unique_names(const TypedArray<String> &p_unique_names) {
	GLTFTemplateConvert::set_from_array(unique_names, p_unique_names);
}

Dictionary GLTFSkeleton::get_orblit_bone_node() {
	return GLTFTemplateConvert::to_dictionary(orblit_bone_node);
}

void GLTFSkeleton::set_orblit_bone_node(const Dictionary &p_indict) {
	GLTFTemplateConvert::set_from_dictionary(orblit_bone_node, p_indict);
}

BoneAttachment3D *GLTFSkeleton::get_bone_attachment(int idx) {
	ERR_FAIL_INDEX_V(idx, bone_attachments.size(), nullptr);
	return bone_attachments[idx];
}

int32_t GLTFSkeleton::get_bone_attachment_count() {
	return bone_attachments.size();
}
