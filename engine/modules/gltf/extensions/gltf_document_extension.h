/**************************************************************************/
/*  gltf_document_extension.h                                             */

#pragma once

#include "../gltf_state.h"

#include "scene/spatial/entity_spatial.h"

class GLTFDocumentExtension : public Resource {
	ORBLIT_CLASS(GLTFDocumentExtension, Resource);

protected:
	static void register_members();

public:
	// Import process.
	virtual Error import_preflight(Ref<GLTFState> p_state, const Vector<String> &p_extensions);
	virtual Vector<String> get_supported_extensions();
	virtual Error parse_node_extensions(Ref<GLTFState> p_state, Ref<GLTFNode> p_gltf_node, const Dictionary &p_extensions);
	virtual Error parse_image_data(Ref<GLTFState> p_state, const PackedByteArray &p_image_data, const String &p_mime_type, Ref<Image> r_image);
	virtual String get_image_file_extension();
	virtual Error parse_texture_json(Ref<GLTFState> p_state, const Dictionary &p_texture_json, Ref<GLTFTexture> r_gltf_texture);
	virtual Ref<GLTFObjectModelProperty> import_object_model_property(Ref<GLTFState> p_state, const PackedStringArray &p_split_json_pointer, const TypedArray<EntityPath> &p_partial_paths);
	virtual Error import_post_parse(Ref<GLTFState> p_state);
	virtual Error import_pre_generate(Ref<GLTFState> p_state);
	virtual Entity3D *generate_scene_node(Ref<GLTFState> p_state, Ref<GLTFNode> p_gltf_node, Entity *p_scene_parent);
	virtual Error import_node(Ref<GLTFState> p_state, Ref<GLTFNode> p_gltf_node, Dictionary &r_json, Entity *p_node);
	virtual Error import_post(Ref<GLTFState> p_state, Entity *p_node);
	// Export process.
	virtual List<PropertyInfo> export_get_property_list(Entity *p_root_node);
	virtual Error export_preflight(Ref<GLTFState> p_state, Entity *p_root);
	virtual void convert_scene_node(Ref<GLTFState> p_state, Ref<GLTFNode> p_gltf_node, Entity *p_scene_node);
	virtual Error export_post_convert(Ref<GLTFState> p_state, Entity *p_root);
	virtual Error export_preserialize(Ref<GLTFState> p_state);
	virtual Ref<GLTFObjectModelProperty> export_object_model_property(Ref<GLTFState> p_state, const EntityPath &p_node_path, const Entity *p_orblit_node, GLTFNodeIndex p_gltf_node_index, const Object *p_target_object, int p_target_depth);
	virtual Vector<String> get_saveable_image_formats();
	virtual PackedByteArray serialize_image_to_bytes(Ref<GLTFState> p_state, Ref<Image> p_image, Dictionary &r_image_dict, const String &p_image_format, float p_lossy_quality);
	virtual Error save_image_at_path(Ref<GLTFState> p_state, Ref<Image> p_image, const String &p_file_path, const String &p_image_format, float p_lossy_quality);
	virtual Error serialize_texture_json(Ref<GLTFState> p_state, Dictionary &r_texture_json, Ref<GLTFTexture> p_gltf_texture, const String &p_image_format);
	virtual Error export_node(Ref<GLTFState> p_state, Ref<GLTFNode> p_gltf_node, Dictionary &r_json, Entity *p_node);
	virtual Error export_post(Ref<GLTFState> p_state);

	// Import process.
	ORBLIT_VIRTUAL2R(Error, _import_preflight, Ref<GLTFState>, Vector<String>);
	ORBLIT_VIRTUAL0R(Vector<String>, _get_supported_extensions);
	ORBLIT_VIRTUAL3R(Error, _parse_node_extensions, Ref<GLTFState>, Ref<GLTFNode>, Dictionary);
	ORBLIT_VIRTUAL4R(Error, _parse_image_data, Ref<GLTFState>, PackedByteArray, String, Ref<Image>);
	ORBLIT_VIRTUAL0R(String, _get_image_file_extension);
	ORBLIT_VIRTUAL3R(Error, _parse_texture_json, Ref<GLTFState>, Dictionary, Ref<GLTFTexture>);
	ORBLIT_VIRTUAL3R(Ref<GLTFObjectModelProperty>, _import_object_model_property, Ref<GLTFState>, PackedStringArray, TypedArray<EntityPath>);
	ORBLIT_VIRTUAL1R(Error, _import_post_parse, Ref<GLTFState>);
	ORBLIT_VIRTUAL1R(Error, _import_pre_generate, Ref<GLTFState>);
	ORBLIT_VIRTUAL3R(Entity3D *, _generate_scene_node, Ref<GLTFState>, Ref<GLTFNode>, Entity *);
	ORBLIT_VIRTUAL4R(Error, _import_node, Ref<GLTFState>, Ref<GLTFNode>, Dictionary, Entity *);
	ORBLIT_VIRTUAL2R(Error, _import_post, Ref<GLTFState>, Entity *);
	// Export process.
	ORBLIT_VIRTUAL1R(TypedArray<Dictionary>, _export_get_property_list, Entity *);
	ORBLIT_VIRTUAL2R(Error, _export_preflight, Ref<GLTFState>, Entity *);
	ORBLIT_VIRTUAL3(_convert_scene_node, Ref<GLTFState>, Ref<GLTFNode>, Entity *);
	ORBLIT_VIRTUAL2R(Error, _export_post_convert, Ref<GLTFState>, Entity *);
	ORBLIT_VIRTUAL1R(Error, _export_preserialize, Ref<GLTFState>);
	ORBLIT_VIRTUAL6R(Ref<GLTFObjectModelProperty>, _export_object_model_property, Ref<GLTFState>, EntityPath, const Entity *, GLTFNodeIndex, const Object *, int);
	ORBLIT_VIRTUAL0R(Vector<String>, _get_saveable_image_formats);
	ORBLIT_VIRTUAL5R(PackedByteArray, _serialize_image_to_bytes, Ref<GLTFState>, Ref<Image>, Dictionary, String, float);
	ORBLIT_VIRTUAL5R(Error, _save_image_at_path, Ref<GLTFState>, Ref<Image>, String, String, float);
	ORBLIT_VIRTUAL4R(Error, _serialize_texture_json, Ref<GLTFState>, Dictionary, Ref<GLTFTexture>, String);
	ORBLIT_VIRTUAL4R(Error, _export_node, Ref<GLTFState>, Ref<GLTFNode>, Dictionary, Entity *);
	ORBLIT_VIRTUAL1R(Error, _export_post, Ref<GLTFState>);
};
