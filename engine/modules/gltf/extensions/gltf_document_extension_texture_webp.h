/**************************************************************************/
/*  gltf_document_extension_texture_webp.h                                */

#pragma once

#include "gltf_document_extension.h"

class GLTFDocumentExtensionTextureWebP : public GLTFDocumentExtension {
	ORBLIT_CLASS(GLTFDocumentExtensionTextureWebP, GLTFDocumentExtension);

public:
	// Import process.
	Error import_preflight(Ref<GLTFState> p_state, const Vector<String> &p_extensions) override;
	Vector<String> get_supported_extensions() override;
	Error parse_image_data(Ref<GLTFState> p_state, const PackedByteArray &p_image_data, const String &p_mime_type, Ref<Image> r_image) override;
	String get_image_file_extension() override;
	Error parse_texture_json(Ref<GLTFState> p_state, const Dictionary &p_texture_json, Ref<GLTFTexture> r_gltf_texture) override;
	// Export process.
	Vector<String> get_saveable_image_formats() override;
	PackedByteArray serialize_image_to_bytes(Ref<GLTFState> p_state, Ref<Image> p_image, Dictionary &r_image_dict, const String &p_image_format, float p_lossy_quality) override;
	Error save_image_at_path(Ref<GLTFState> p_state, Ref<Image> p_image, const String &p_full_path, const String &p_image_format, float p_lossy_quality) override;
	Error serialize_texture_json(Ref<GLTFState> p_state, Dictionary &r_texture_json, Ref<GLTFTexture> p_gltf_texture, const String &p_image_format) override;
};
