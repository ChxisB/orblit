/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "extensions/gltf_document_extension_convert_importer_mesh.h"
#include "extensions/gltf_document_extension_texture_ktx.h"
#include "extensions/gltf_document_extension_texture_webp.h"
#include "extensions/gltf_spec_gloss.h"
#include "gltf_document.h"
#include "gltf_state.h"
#include "structures/gltf_object_model_property.h"

#include "core/config/engine.h"
#include "core/object/class_registry.h"

#ifndef PHYSICS_SPATIAL_DISABLED
#include "extensions/physics/gltf_document_extension_physics.h"
#include "extensions/physics/gltf_physics_body.h"
#include "extensions/physics/gltf_physics_shape.h"
#endif // PHYSICS_SPATIAL_DISABLED

#ifdef TOOLS_ENABLED
#include "editor/editor_import_blend_runner.h"
#include "editor/editor_scene_exporter_gltf_plugin.h"
#include "editor/editor_scene_importer_blend.h"
#include "editor/editor_scene_importer_gltf.h"
//
#include "core/config/project_settings.h"
#include "editor/editor_node.h"
#include "editor/settings/editor_settings.h"

static void _editor_init() {
	Ref<EditorSceneFormatImporterGLTF> import_gltf;
	import_gltf.instantiate();
	ResourceImporterScene::add_scene_importer(import_gltf);

	// Blend to glTF importer.

	String blender_path = EDITOR_GET("filesystem/import/blender/blender_path");
	if (blender_path.is_empty() && EditorSettings::get_singleton()->has_setting("filesystem/import/blender/blender3_path")) {
		blender_path = EDITOR_GET("filesystem/import/blender/blender3_path");

		if (!blender_path.is_empty()) {
#if defined(MACOS_ENABLED)
			if (blender_path.contains(".app")) {
				blender_path += "/Contents/MacOS/Blender";
			} else {
				blender_path += "/blender";
			}
#elif defined(WINDOWS_ENABLED)
			blender_path += "\\blender.exe";
#elif defined(UNIX_ENABLED)
			blender_path += "/blender";
#endif

			EditorSettings::get_singleton()->set("filesystem/import/blender/blender_path", blender_path);
		}

		EditorSettings::get_singleton()->erase("filesystem/import/blender/blender3_path");
		EditorSettings::get_singleton()->save();
	}

	bool blend_enabled = GLOBAL_GET("filesystem/import/blender/enabled");
	if (blend_enabled) {
		Ref<EditorSceneFormatImporterBlend> importer;
		importer.instantiate();
		ResourceImporterScene::add_scene_importer(importer);

		Ref<EditorFileSystemImportFormatSupportQueryBlend> blend_import_query;
		blend_import_query.instantiate();
		EditorFileSystem::get_singleton()->add_import_format_support_query(blend_import_query);
	}
	memnew(EditorImportBlendRunner);
	EditorNode::get_singleton()->add_child(EditorImportBlendRunner::get_singleton());
}
#endif // TOOLS_ENABLED

#define GLTF_REGISTER_DOCUMENT_EXTENSION(m_doc_ext_class) \
	Ref<m_doc_ext_class> extension_##m_doc_ext_class; \
	extension_##m_doc_ext_class.instantiate(); \
	GLTFDocument::register_gltf_document_extension(extension_##m_doc_ext_class);

void initialize_gltf_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_SCENE) {
		// glTF API available at runtime.
		ORBLIT_REGISTER_CLASS(GLTFAccessor);
		ORBLIT_REGISTER_CLASS(GLTFAnimation);
		ORBLIT_REGISTER_CLASS(GLTFBufferView);
		ORBLIT_REGISTER_CLASS(GLTFCamera);
		ORBLIT_REGISTER_CLASS(GLTFDocument);
		ORBLIT_REGISTER_CLASS(GLTFDocumentExtension);
		ORBLIT_REGISTER_CLASS(GLTFDocumentExtensionConvertImporterMesh);
		ORBLIT_REGISTER_CLASS(GLTFLight);
		ORBLIT_REGISTER_CLASS(GLTFMesh);
		ORBLIT_REGISTER_CLASS(GLTFNode);
		ORBLIT_REGISTER_CLASS(GLTFObjectModelProperty);
#ifndef PHYSICS_SPATIAL_DISABLED
		ORBLIT_REGISTER_CLASS(GLTFPhysicsBody);
		ORBLIT_REGISTER_CLASS(GLTFPhysicsShape);
#endif // PHYSICS_SPATIAL_DISABLED
		ORBLIT_REGISTER_CLASS(GLTFSkeleton);
		ORBLIT_REGISTER_CLASS(GLTFSkin);
		ORBLIT_REGISTER_CLASS(GLTFSpecGloss);
		ORBLIT_REGISTER_CLASS(GLTFState);
		ORBLIT_REGISTER_CLASS(GLTFTexture);
		ORBLIT_REGISTER_CLASS(GLTFTextureSampler);
// Register GLTFDocumentExtension classes with GLTFDocument.
#ifndef PHYSICS_SPATIAL_DISABLED
		// Ensure physics is first in this list so that physics nodes are created before other nodes.
		GLTF_REGISTER_DOCUMENT_EXTENSION(GLTFDocumentExtensionPhysics);
#endif // PHYSICS_SPATIAL_DISABLED
		GLTF_REGISTER_DOCUMENT_EXTENSION(GLTFDocumentExtensionTextureKTX);
		GLTF_REGISTER_DOCUMENT_EXTENSION(GLTFDocumentExtensionTextureWebP);
		bool is_editor = Engine::get_singleton()->is_editor_hint();
		if (!is_editor) {
			GLTF_REGISTER_DOCUMENT_EXTENSION(GLTFDocumentExtensionConvertImporterMesh);
		}
	}

#ifdef TOOLS_ENABLED
	if (p_level == MODULE_INITIALIZATION_LEVEL_EDITOR) {
		ORBLIT_REGISTER_CLASS(EditorSceneFormatImporterGLTF);
		EditorPlugins::add_by_type<SceneExporterGLTFPlugin>();

		// Project settings defined here so doctool finds them.
		GLOBAL_DEF_RST_BASIC("filesystem/import/blender/enabled", true);
		ORBLIT_REGISTER_CLASS(EditorSceneFormatImporterBlend);
		// Can't (a priori) run external app on these platforms.
		GLOBAL_DEF_RST("filesystem/import/blender/enabled.android", false);
		GLOBAL_DEF_RST("filesystem/import/blender/enabled.web", false);

		EditorNode::add_init_callback(_editor_init);
	}
#endif // TOOLS_ENABLED
}

void uninitialize_gltf_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
	GLTFDocument::unregister_all_gltf_document_extensions();
}
