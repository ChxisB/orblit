/**************************************************************************/
/*  audio_stream_mp3_editor_plugin.h                                      */

#pragma once

#include "editor/audio/audio_stream_editor_plugin.h"

class EditorInspectorPluginAudioStreamMP3 : public EditorInspectorPluginAudioStream {
	ORBLIT_CLASS(EditorInspectorPluginAudioStreamMP3, EditorInspectorPluginAudioStream);

public:
	virtual bool can_handle(Object *p_object) override;
};
