/**************************************************************************/
/*  gltf_node.h                                                           */

#pragma once

#include "../gltf_defines.h"

#include "core/io/resource.h"

class GLTFNode : public Resource {
	ORBLIT_CLASS(GLTFNode, Resource);
	friend class GLTFDocument;
	friend class SkinTool;
	friend class FBXDocument;

private:
	String original_name;
	GLTFNodeIndex parent = -1;
	int height = -1;
	Transform3D transform;
	GLTFMeshIndex mesh = -1;
	GLTFCameraIndex camera = -1;
	GLTFSkinIndex skin = -1;
	GLTFSkeletonIndex skeleton = -1;
	bool joint = false;
	bool visible = true;
	Vector<int> children;
	GLTFLightIndex light = -1;
	Dictionary additional_data;

protected:
	static void register_members();

public:
	String get_original_name();
	void set_original_name(const String &p_name);

	GLTFNodeIndex get_parent();
	void set_parent(GLTFNodeIndex p_parent);

	int get_height();
	void set_height(int p_height);

	Transform3D get_xform();
	void set_xform(const Transform3D &p_xform);

	Transform3D get_rest_xform();
	void set_rest_xform(const Transform3D &p_rest_xform);

	GLTFMeshIndex get_mesh();
	void set_mesh(GLTFMeshIndex p_mesh);

	GLTFCameraIndex get_camera();
	void set_camera(GLTFCameraIndex p_camera);

	GLTFSkinIndex get_skin();
	void set_skin(GLTFSkinIndex p_skin);

	GLTFSkeletonIndex get_skeleton();
	void set_skeleton(GLTFSkeletonIndex p_skeleton);

	Vector3 get_position();
	void set_position(const Vector3 &p_position);

	Quaternion get_rotation();
	void set_rotation(const Quaternion &p_rotation);

	Vector3 get_scale();
	void set_scale(const Vector3 &p_scale);

	Vector<int> get_children();
	void set_children(const Vector<int> &p_children);
	void append_child_index(int p_child_index);

	GLTFLightIndex get_light();
	void set_light(GLTFLightIndex p_light);

	bool get_visible();
	void set_visible(bool p_visible);

	Variant get_additional_data(const StringName &p_extension_name);
	bool has_additional_data(const StringName &p_extension_name);
	void set_additional_data(const StringName &p_extension_name, Variant p_additional_data);

	EntityPath get_scene_node_path(Ref<GLTFState> p_state, bool p_handle_skeletons = true);
};
