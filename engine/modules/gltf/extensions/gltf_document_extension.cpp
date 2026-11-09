/**************************************************************************/
/*  gltf_document_extension.cpp                                           */

#include "gltf_document_extension.h"

#include "core/object/class_registry.h" // IWYU pragma: keep. `ORBLIT_VIRTUAL_BIND` macro.

void GLTFDocumentExtension::register_members() {
	// Import process.
	ORBLIT_VIRTUAL_BIND(_import_preflight, "state", "extensions");
	ORBLIT_VIRTUAL_BIND(_get_supported_extensions);
	ORBLIT_VIRTUAL_BIND(_parse_node_extensions, "state", "gltf_node", "extensions");
	ORBLIT_VIRTUAL_BIND(_parse_image_data, "state", "image_data", "mime_type", "ret_image");
	ORBLIT_VIRTUAL_BIND(_get_image_file_extension);
	ORBLIT_VIRTUAL_BIND(_parse_texture_json, "state", "texture_json", "ret_gltf_texture");
	ORBLIT_VIRTUAL_BIND(_import_object_model_property, "state", "split_json_pointer", "partial_paths");
	ORBLIT_VIRTUAL_BIND(_import_post_parse, "state");
	ORBLIT_VIRTUAL_BIND(_import_pre_generate, "state");
	ORBLIT_VIRTUAL_BIND(_generate_scene_node, "state", "gltf_node", "scene_parent");
	ORBLIT_VIRTUAL_BIND(_import_node, "state", "gltf_node", "json", "node");
	ORBLIT_VIRTUAL_BIND(_import_post, "state", "root");
	// Export process.
	ORBLIT_VIRTUAL_BIND(_export_get_property_list, "root_node");
	ORBLIT_VIRTUAL_BIND(_export_preflight, "state", "root");
	ORBLIT_VIRTUAL_BIND(_convert_scene_node, "state", "gltf_node", "scene_node");
	ORBLIT_VIRTUAL_BIND(_export_post_convert, "state", "root");
	ORBLIT_VIRTUAL_BIND(_export_preserialize, "state");
	ORBLIT_VIRTUAL_BIND(_export_object_model_property, "state", "node_path", "orblit_node", "gltf_node_index", "target_object", "target_depth");
	ORBLIT_VIRTUAL_BIND(_get_saveable_image_formats);
	ORBLIT_VIRTUAL_BIND(_serialize_image_to_bytes, "state", "image", "image_dict", "image_format", "lossy_quality");
	ORBLIT_VIRTUAL_BIND(_save_image_at_path, "state", "image", "file_path", "image_format", "lossy_quality");
	ORBLIT_VIRTUAL_BIND(_serialize_texture_json, "state", "texture_json", "gltf_texture", "image_format");
	ORBLIT_VIRTUAL_BIND(_export_node, "state", "gltf_node", "json", "node");
	ORBLIT_VIRTUAL_BIND(_export_post, "state");
}

// Import process.
Error GLTFDocumentExtension::import_preflight(Ref<GLTFState> p_state, const Vector<String> &p_extensions) {
	ERR_FAIL_COND_V(p_state.is_null(), ERR_INVALID_PARAMETER);
	Error err = OK;
	ORBLIT_VIRTUAL_CALL(_import_preflight, p_state, p_extensions, err);
	return err;
}

Vector<String> GLTFDocumentExtension::get_supported_extensions() {
	Vector<String> ret;
	ORBLIT_VIRTUAL_CALL(_get_supported_extensions, ret);
	return ret;
}

Error GLTFDocumentExtension::parse_node_extensions(Ref<GLTFState> p_state, Ref<GLTFNode> p_gltf_node, const Dictionary &p_extensions) {
	ERR_FAIL_COND_V(p_state.is_null(), ERR_INVALID_PARAMETER);
	ERR_FAIL_COND_V(p_gltf_node.is_null(), ERR_INVALID_PARAMETER);
	Error err = OK;
	ORBLIT_VIRTUAL_CALL(_parse_node_extensions, p_state, p_gltf_node, Dictionary(p_extensions), err);
	return err;
}

Error GLTFDocumentExtension::parse_image_data(Ref<GLTFState> p_state, const PackedByteArray &p_image_data, const String &p_mime_type, Ref<Image> r_image) {
	ERR_FAIL_COND_V(p_state.is_null(), ERR_INVALID_PARAMETER);
	ERR_FAIL_COND_V(r_image.is_null(), ERR_INVALID_PARAMETER);
	Error err = OK;
	ORBLIT_VIRTUAL_CALL(_parse_image_data, p_state, p_image_data, p_mime_type, r_image, err);
	return err;
}

String GLTFDocumentExtension::get_image_file_extension() {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_image_file_extension, ret);
	return ret;
}

Error GLTFDocumentExtension::parse_texture_json(Ref<GLTFState> p_state, const Dictionary &p_texture_json, Ref<GLTFTexture> r_gltf_texture) {
	ERR_FAIL_COND_V(p_state.is_null(), ERR_INVALID_PARAMETER);
	ERR_FAIL_COND_V(r_gltf_texture.is_null(), ERR_INVALID_PARAMETER);
	Error err = OK;
	ORBLIT_VIRTUAL_CALL(_parse_texture_json, p_state, Dictionary(p_texture_json), r_gltf_texture, err);
	return err;
}

Ref<GLTFObjectModelProperty> GLTFDocumentExtension::import_object_model_property(Ref<GLTFState> p_state, const PackedStringArray &p_split_json_pointer, const TypedArray<EntityPath> &p_partial_paths) {
	Ref<GLTFObjectModelProperty> ret;
	ERR_FAIL_COND_V(p_state.is_null(), ret);
	ORBLIT_VIRTUAL_CALL(_import_object_model_property, p_state, p_split_json_pointer, p_partial_paths, ret);
	return ret;
}

Error GLTFDocumentExtension::import_post_parse(Ref<GLTFState> p_state) {
	ERR_FAIL_COND_V(p_state.is_null(), ERR_INVALID_PARAMETER);
	Error err = OK;
	ORBLIT_VIRTUAL_CALL(_import_post_parse, p_state, err);
	return err;
}

Error GLTFDocumentExtension::import_pre_generate(Ref<GLTFState> p_state) {
	ERR_FAIL_COND_V(p_state.is_null(), ERR_INVALID_PARAMETER);
	Error err = OK;
	ORBLIT_VIRTUAL_CALL(_import_pre_generate, p_state, err);
	return err;
}

Entity3D *GLTFDocumentExtension::generate_scene_node(Ref<GLTFState> p_state, Ref<GLTFNode> p_gltf_node, Entity *p_scene_parent) {
	ERR_FAIL_COND_V(p_state.is_null(), nullptr);
	ERR_FAIL_COND_V(p_gltf_node.is_null(), nullptr);
	Entity3D *ret_node = nullptr;
	ORBLIT_VIRTUAL_CALL(_generate_scene_node, p_state, p_gltf_node, p_scene_parent, ret_node);
	return ret_node;
}

Error GLTFDocumentExtension::import_node(Ref<GLTFState> p_state, Ref<GLTFNode> p_gltf_node, Dictionary &r_dict, Entity *p_node) {
	ERR_FAIL_COND_V(p_state.is_null(), ERR_INVALID_PARAMETER);
	ERR_FAIL_COND_V(p_gltf_node.is_null(), ERR_INVALID_PARAMETER);
	ERR_FAIL_NULL_V(p_node, ERR_INVALID_PARAMETER);
	Error err = OK;
	ORBLIT_VIRTUAL_CALL(_import_node, p_state, p_gltf_node, Dictionary(r_dict), p_node, err);
	return err;
}

Error GLTFDocumentExtension::import_post(Ref<GLTFState> p_state, Entity *p_root) {
	ERR_FAIL_NULL_V(p_root, ERR_INVALID_PARAMETER);
	ERR_FAIL_COND_V(p_state.is_null(), ERR_INVALID_PARAMETER);
	Error err = OK;
	ORBLIT_VIRTUAL_CALL(_import_post, p_state, p_root, err);
	return err;
}

// Export process.
List<PropertyInfo> GLTFDocumentExtension::export_get_property_list(Entity *p_root_node) {
	TypedArray<Dictionary> ret_dicts;
	ORBLIT_VIRTUAL_CALL(_export_get_property_list, p_root_node, ret_dicts);
	List<PropertyInfo> ret;
	if (ret_dicts.is_empty()) {
		return ret;
	}
	for (int i = 0; i < ret_dicts.size(); i++) {
		ret.push_back(PropertyInfo::from_dict(ret_dicts[i]));
	}
	return ret;
}

Error GLTFDocumentExtension::export_preflight(Ref<GLTFState> p_state, Entity *p_root) {
	ERR_FAIL_NULL_V(p_root, ERR_INVALID_PARAMETER);
	Error err = OK;
	ORBLIT_VIRTUAL_CALL(_export_preflight, p_state, p_root, err);
	return err;
}

void GLTFDocumentExtension::convert_scene_node(Ref<GLTFState> p_state, Ref<GLTFNode> p_gltf_node, Entity *p_scene_node) {
	ERR_FAIL_COND(p_state.is_null());
	ERR_FAIL_COND(p_gltf_node.is_null());
	ERR_FAIL_NULL(p_scene_node);
	ORBLIT_VIRTUAL_CALL(_convert_scene_node, p_state, p_gltf_node, p_scene_node);
}

Error GLTFDocumentExtension::export_post_convert(Ref<GLTFState> p_state, Entity *p_root) {
	ERR_FAIL_COND_V(p_state.is_null(), ERR_INVALID_PARAMETER);
	ERR_FAIL_NULL_V(p_root, ERR_INVALID_PARAMETER);
	Error err = OK;
	ORBLIT_VIRTUAL_CALL(_export_post_convert, p_state, p_root, err);
	return err;
}

Error GLTFDocumentExtension::export_preserialize(Ref<GLTFState> p_state) {
	ERR_FAIL_COND_V(p_state.is_null(), ERR_INVALID_PARAMETER);
	Error err = OK;
	ORBLIT_VIRTUAL_CALL(_export_preserialize, p_state, err);
	return err;
}

Ref<GLTFObjectModelProperty> GLTFDocumentExtension::export_object_model_property(Ref<GLTFState> p_state, const EntityPath &p_node_path, const Entity *p_orblit_node, GLTFNodeIndex p_gltf_node_index, const Object *p_target_object, int p_target_depth) {
	Ref<GLTFObjectModelProperty> ret;
	ERR_FAIL_COND_V(p_state.is_null(), ret);
	ERR_FAIL_NULL_V(p_orblit_node, ret);
	ERR_FAIL_NULL_V(p_target_object, ret);
	ORBLIT_VIRTUAL_CALL(_export_object_model_property, p_state, p_node_path, p_orblit_node, p_gltf_node_index, p_target_object, p_target_depth, ret);
	return ret;
}

Vector<String> GLTFDocumentExtension::get_saveable_image_formats() {
	Vector<String> ret;
	ORBLIT_VIRTUAL_CALL(_get_saveable_image_formats, ret);
	return ret;
}

PackedByteArray GLTFDocumentExtension::serialize_image_to_bytes(Ref<GLTFState> p_state, Ref<Image> p_image, Dictionary &r_image_dict, const String &p_image_format, float p_lossy_quality) {
	PackedByteArray ret;
	ERR_FAIL_COND_V(p_state.is_null(), ret);
	ERR_FAIL_COND_V(p_image.is_null(), ret);
	ORBLIT_VIRTUAL_CALL(_serialize_image_to_bytes, p_state, p_image, Dictionary(r_image_dict), p_image_format, p_lossy_quality, ret);
	return ret;
}

Error GLTFDocumentExtension::save_image_at_path(Ref<GLTFState> p_state, Ref<Image> p_image, const String &p_file_path, const String &p_image_format, float p_lossy_quality) {
	ERR_FAIL_COND_V(p_state.is_null(), ERR_INVALID_PARAMETER);
	ERR_FAIL_COND_V(p_image.is_null(), ERR_INVALID_PARAMETER);
	Error ret = OK;
	ORBLIT_VIRTUAL_CALL(_save_image_at_path, p_state, p_image, p_file_path, p_image_format, p_lossy_quality, ret);
	return ret;
}

Error GLTFDocumentExtension::serialize_texture_json(Ref<GLTFState> p_state, Dictionary &r_texture_json, Ref<GLTFTexture> p_gltf_texture, const String &p_image_format) {
	ERR_FAIL_COND_V(p_state.is_null(), ERR_INVALID_PARAMETER);
	ERR_FAIL_COND_V(p_gltf_texture.is_null(), ERR_INVALID_PARAMETER);
	Error err = OK;
	ORBLIT_VIRTUAL_CALL(_serialize_texture_json, p_state, Dictionary(r_texture_json), p_gltf_texture, p_image_format, err);
	return err;
}

Error GLTFDocumentExtension::export_node(Ref<GLTFState> p_state, Ref<GLTFNode> p_gltf_node, Dictionary &r_dict, Entity *p_node) {
	ERR_FAIL_COND_V(p_state.is_null(), ERR_INVALID_PARAMETER);
	ERR_FAIL_COND_V(p_gltf_node.is_null(), ERR_INVALID_PARAMETER);
	Error err = OK;
	ORBLIT_VIRTUAL_CALL(_export_node, p_state, p_gltf_node, Dictionary(r_dict), p_node, err);
	return err;
}

Error GLTFDocumentExtension::export_post(Ref<GLTFState> p_state) {
	ERR_FAIL_COND_V(p_state.is_null(), ERR_INVALID_PARAMETER);
	Error err = OK;
	ORBLIT_VIRTUAL_CALL(_export_post, p_state, err);
	return err;
}
