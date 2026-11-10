/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "audio_stream_ogg_vorbis.h"

#include "core/io/resource_importer.h"
#include "core/object/class_registry.h"

#ifdef TOOLS_ENABLED
#include "resource_importer_ogg_vorbis.h"

#include "editor/editor_node.h"
#include "editor/inspector/editor_inspector.h"

#include "modules/vorbis/editor/audio_stream_ogg_vorbis_editor_plugin.h"

static void _editor_init() {
	Ref<ResourceImporterOggVorbis> ogg_vorbis_importer;
	ogg_vorbis_importer.instantiate();
	ResourceFormatImporter::get_singleton()->add_importer(ogg_vorbis_importer);

	Ref<EditorInspectorPluginAudioStreamOggVorbis> plugin;
	plugin.instantiate();
	EditorInspector::add_inspector_plugin(plugin);
}
#endif

void initialize_vorbis_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_SCENE) {
		ORBLIT_REGISTER_CLASS(AudioStreamOggVorbis);
		ORBLIT_REGISTER_CLASS(AudioStreamPlaybackOggVorbis);
	}

#ifdef TOOLS_ENABLED
	if (p_level == MODULE_INITIALIZATION_LEVEL_EDITOR) {
		ORBLIT_REGISTER_CLASS(ResourceImporterOggVorbis);

		EditorNode::add_init_callback(_editor_init);
	}
#endif
}

void uninitialize_vorbis_module(ModuleInitializationLevel p_level) {
}
