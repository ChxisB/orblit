/**************************************************************************/
/*  gltf_document_extension_physics.h                                     */

#pragma once

#include "../gltf_document_extension.h"

class GLTFDocumentExtensionPhysics : public GLTFDocumentExtension {
	ORBLIT_CLASS(GLTFDocumentExtensionPhysics, GLTFDocumentExtension);

public:
	// Import process.
	Error import_preflight(Ref<GLTFState> p_state, const Vector<String> &p_extensions) override;
	Vector<String> get_supported_extensions() override;
	Error parse_node_extensions(Ref<GLTFState> p_state, Ref<GLTFNode> p_gltf_node, const Dictionary &p_extensions) override;
	Ref<GLTFObjectModelProperty> import_object_model_property(Ref<GLTFState> p_state, const PackedStringArray &p_split_json_pointer, const TypedArray<EntityPath> &p_partial_paths) override;
	Entity3D *generate_scene_node(Ref<GLTFState> p_state, Ref<GLTFNode> p_gltf_node, Entity *p_scene_parent) override;
	// Export process.
	void convert_scene_node(Ref<GLTFState> p_state, Ref<GLTFNode> p_gltf_node, Entity *p_scene_node) override;
	Error export_preserialize(Ref<GLTFState> p_state) override;
	Ref<GLTFObjectModelProperty> export_object_model_property(Ref<GLTFState> p_state, const EntityPath &p_node_path, const Entity *p_orblit_node, GLTFNodeIndex p_gltf_node_index, const Object *p_target_object, int p_target_depth) override;
	Error export_node(Ref<GLTFState> p_state, Ref<GLTFNode> p_gltf_node, Dictionary &r_node_json, Entity *p_scene_node) override;
};
