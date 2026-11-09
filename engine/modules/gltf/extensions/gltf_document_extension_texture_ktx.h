/**************************************************************************/
/*  gltf_document_extension_texture_ktx.h                                 */

#pragma once

#include "gltf_document_extension.h"

class GLTFDocumentExtensionTextureKTX : public GLTFDocumentExtension {
	ORBLIT_CLASS(GLTFDocumentExtensionTextureKTX, GLTFDocumentExtension);

public:
	// Import process.
	Error import_preflight(Ref<GLTFState> p_state, const Vector<String> &p_extensions) override;
	Vector<String> get_supported_extensions() override;
	Error parse_image_data(Ref<GLTFState> p_state, const PackedByteArray &p_image_data, const String &p_mime_type, Ref<Image> r_image) override;
	Error parse_texture_json(Ref<GLTFState> p_state, const Dictionary &p_texture_json, Ref<GLTFTexture> r_gltf_texture) override;
};
