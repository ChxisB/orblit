/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "fbx_document.h"

#include "core/config/engine.h"
#include "core/object/class_registry.h"

#include "modules/gltf/extensions/gltf_document_extension_convert_importer_mesh.h"

#ifdef TOOLS_ENABLED
#include "editor/editor_scene_importer_fbx2gltf.h"
#include "editor/editor_scene_importer_ufbx.h"
//
#include "core/config/project_settings.h"
#include "editor/editor_node.h"

static void _editor_init() {
	Ref<EditorSceneFormatImporterUFBX> import_fbx;
	import_fbx.instantiate();
	ResourceImporterScene::add_scene_importer(import_fbx);

	bool fbx2gltf_enabled = GLOBAL_GET("filesystem/import/fbx2gltf/enabled");
	if (fbx2gltf_enabled) {
		Ref<EditorSceneFormatImporterFBX2GLTF> importer;
		importer.instantiate();
		ResourceImporterScene::add_scene_importer(importer);
	}
}
#endif // TOOLS_ENABLED

#define FBX_REGISTER_DOCUMENT_EXTENSION(m_doc_ext_class) \
	Ref<m_doc_ext_class> extension_##m_doc_ext_class; \
	extension_##m_doc_ext_class.instantiate(); \
	FBXDocument::register_gltf_document_extension(extension_##m_doc_ext_class);

void initialize_fbx_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_SCENE) {
		ORBLIT_REGISTER_CLASS(FBXDocument);
		ORBLIT_REGISTER_CLASS(FBXState);
		bool is_editor = Engine::get_singleton()->is_editor_hint();
		if (!is_editor) {
			FBX_REGISTER_DOCUMENT_EXTENSION(GLTFDocumentExtensionConvertImporterMesh);
		}
	}

#ifdef TOOLS_ENABLED
	if (p_level == MODULE_INITIALIZATION_LEVEL_EDITOR) {
		ORBLIT_REGISTER_CLASS(EditorSceneFormatImporterUFBX);

		GLOBAL_DEF_RST_BASIC("filesystem/import/fbx2gltf/enabled", true);
		ORBLIT_REGISTER_CLASS(EditorSceneFormatImporterFBX2GLTF);
		GLOBAL_DEF_RST("filesystem/import/fbx2gltf/enabled.android", false);
		GLOBAL_DEF_RST("filesystem/import/fbx2gltf/enabled.web", false);

		EditorNode::add_init_callback(_editor_init);
	}
#endif // TOOLS_ENABLED
}

void uninitialize_fbx_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
	FBXDocument::unregister_all_gltf_document_extensions();
}
