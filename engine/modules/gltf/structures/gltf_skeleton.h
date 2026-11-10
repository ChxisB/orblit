/**************************************************************************/
/*  gltf_skeleton.h                                                       */

#pragma once

#include "../gltf_defines.h"

#include "core/io/resource.h"
#include "scene/spatial/bone_attachment_spatial.h"
#include "scene/spatial/skeleton_spatial.h"

class GLTFSkeleton : public Resource {
	ORBLIT_CLASS(GLTFSkeleton, Resource);
	friend class GLTFDocument;
	friend class SkinTool;
	friend class FBXDocument;

private:
	// The *synthesized* skeletons joints
	Vector<GLTFNodeIndex> joints;

	// The roots of the skeleton. If there are multiple, each root must have the
	// same parent (ie roots are siblings)
	Vector<GLTFNodeIndex> roots;

	// The created Skeleton3D for the scene
	Skeleton3D *orblit_skeleton = nullptr;

	// Set of unique bone names for the skeleton
	HashSet<String> unique_names;

	HashMap<int32_t, GLTFNodeIndex> orblit_bone_node;

	Vector<BoneAttachment3D *> bone_attachments;

protected:
	static void register_members();

public:
	Vector<GLTFNodeIndex> get_joints();
	void set_joints(const Vector<GLTFNodeIndex> &p_joints);

	Vector<GLTFNodeIndex> get_roots();
	void set_roots(const Vector<GLTFNodeIndex> &p_roots);

	Skeleton3D *get_orblit_skeleton();

	// Skeleton *get_orblit_skeleton() {
	// 	return orblit_skeleton;
	// }
	// void set_orblit_skeleton(Skeleton p_*orblit_skeleton) {
	// 	orblit_skeleton = p_orblit_skeleton;
	// }

	TypedArray<String> get_unique_names();
	void set_unique_names(const TypedArray<String> &p_unique_names);

	//RBMap<int32_t, GLTFNodeIndex> get_orblit_bone_node() {
	//	return orblit_bone_node;
	//}
	//void set_orblit_bone_node(const RBMap<int32_t, GLTFNodeIndex> &p_orblit_bone_node) {
	//	orblit_bone_node = p_orblit_bone_node;
	//}
	Dictionary get_orblit_bone_node();
	void set_orblit_bone_node(const Dictionary &p_indict);

	//Dictionary get_orblit_bone_node() {
	//	return VariantConversion::to_dict(orblit_bone_node);
	//}
	//void set_orblit_bone_node(Dictionary p_indict) {
	//	VariantConversion::set_from_dict(orblit_bone_node, p_indict);
	//}

	BoneAttachment3D *get_bone_attachment(int idx);

	int32_t get_bone_attachment_count();
};
