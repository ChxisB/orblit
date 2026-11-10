/**************************************************************************/
/*  gltf_document_extension_texture_ktx.cpp                               */

#include "gltf_document_extension_texture_ktx.h"

// Import process.
Error GLTFDocumentExtensionTextureKTX::import_preflight(Ref<GLTFState> p_state, const Vector<String> &p_extensions) {
	if (!p_extensions.has("KHR_texture_basisu")) {
		return ERR_SKIP;
	}
	return OK;
}

Vector<String> GLTFDocumentExtensionTextureKTX::get_supported_extensions() {
	Vector<String> ret;
	ret.push_back("KHR_texture_basisu");
	return ret;
}

Error GLTFDocumentExtensionTextureKTX::parse_image_data(Ref<GLTFState> p_state, const PackedByteArray &p_image_data, const String &p_mime_type, Ref<Image> r_image) {
	if (p_mime_type == "image/ktx2") {
		return r_image->load_ktx_from_buffer(p_image_data);
	}
	return OK;
}

Error GLTFDocumentExtensionTextureKTX::parse_texture_json(Ref<GLTFState> p_state, const Dictionary &p_texture_json, Ref<GLTFTexture> r_gltf_texture) {
	if (!p_texture_json.has("extensions")) {
		return OK;
	}
	const Dictionary &extensions = p_texture_json["extensions"];
	if (!extensions.has("KHR_texture_basisu")) {
		return OK;
	}
	const Dictionary &texture_ktx = extensions["KHR_texture_basisu"];
	ERR_FAIL_COND_V(!texture_ktx.has("source"), ERR_PARSE_ERROR);
	r_gltf_texture->set_src_image(texture_ktx["source"]);
	return OK;
}
