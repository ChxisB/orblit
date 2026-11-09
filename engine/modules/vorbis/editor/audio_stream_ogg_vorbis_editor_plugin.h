/**************************************************************************/
/*  audio_stream_ogg_vorbis_editor_plugin.h                               */

#pragma once

#include "editor/audio/audio_stream_editor_plugin.h"

class EditorInspectorPluginAudioStreamOggVorbis : public EditorInspectorPluginAudioStream {
	ORBLIT_CLASS(EditorInspectorPluginAudioStreamOggVorbis, EditorInspectorPluginAudioStream);

public:
	virtual bool can_handle(Object *p_object) override;
};
