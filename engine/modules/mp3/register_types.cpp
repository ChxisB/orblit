/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "audio_stream_mp3.h"

#include "core/io/resource_importer.h"
#include "core/object/class_registry.h"

#ifdef TOOLS_ENABLED
#include "resource_importer_mp3.h"

#include "editor/editor_node.h"
#include "editor/inspector/editor_inspector.h"

#include "modules/mp3/editor/audio_stream_mp3_editor_plugin.h"

static void _editor_init() {
	Ref<ResourceImporterMP3> mp3_import;
	mp3_import.instantiate();
	ResourceFormatImporter::get_singleton()->add_importer(mp3_import);

	Ref<EditorInspectorPluginAudioStreamMP3> plugin;
	plugin.instantiate();
	EditorInspector::add_inspector_plugin(plugin);
}
#endif

void initialize_mp3_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_SCENE) {
		ORBLIT_REGISTER_CLASS(AudioStreamMP3);
	}

#ifdef TOOLS_ENABLED
	if (p_level == MODULE_INITIALIZATION_LEVEL_EDITOR) {
		ORBLIT_REGISTER_CLASS(ResourceImporterMP3);

		EditorNode::add_init_callback(_editor_init);
	}
#endif
}

void uninitialize_mp3_module(ModuleInitializationLevel p_level) {
}
