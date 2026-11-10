/**************************************************************************/
/*  audio_stream_ogg_vorbis_editor_plugin.cpp                             */

#include "audio_stream_ogg_vorbis_editor_plugin.h"

#include "modules/vorbis/audio_stream_ogg_vorbis.h"

bool EditorInspectorPluginAudioStreamOggVorbis::can_handle(Object *p_object) {
	return Object::cast_to<AudioStreamOggVorbis>(p_object) != nullptr;
}
