/**************************************************************************/
/*  audio_stream_mp3_editor_plugin.cpp                                    */

#include "audio_stream_mp3_editor_plugin.h"

#include "modules/mp3/audio_stream_mp3.h"

bool EditorInspectorPluginAudioStreamMP3::can_handle(Object *p_object) {
	return Object::cast_to<AudioStreamMP3>(p_object) != nullptr;
}
