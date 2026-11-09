/**************************************************************************/
/*  audio_stream_editor_plugin.h                                          */

#pragma once

#include "editor/inspector/editor_inspector.h"
#include "editor/plugins/editor_plugin.h"
#include "scene/audio/audio_stream_player.h"
#include "scene/ui/button.h"
#include "scene/ui/color_rect.h"
#include "scene/ui/label.h"

class AudioStreamEditor : public ColorRect {
	ORBLIT_CLASS(AudioStreamEditor, ColorRect);

	Ref<AudioStream> stream;

	AudioStreamPlayer *_player = nullptr;
	ColorRect *_preview = nullptr;
	Control *_indicator = nullptr;
	Label *_current_label = nullptr;
	Label *_duration_label = nullptr;

	Button *_play_button = nullptr;
	Button *_stop_button = nullptr;

	float _current = 0;
	bool _dragging = false;
	bool _pausing = false;

protected:
	void _notification(int p_what);
	void _preview_changed(ObjectID p_which);
	void _play();
	void _stop();
	void _on_finished();
	void _draw_preview();
	void _draw_indicator();
	void _on_input_indicator(Ref<InputEvent> p_event);
	void _seek_to(real_t p_x);
	void _stream_changed();

public:
	void set_stream(const Ref<AudioStream> &p_stream);

	AudioStreamEditor();
};

class EditorInspectorPluginAudioStream : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorPluginAudioStream, EditorInspectorPlugin);

public:
	virtual void parse_begin(Object *p_object) override;
};

class EditorInspectorPluginAudioStreamWAV : public EditorInspectorPluginAudioStream {
	ORBLIT_CLASS(EditorInspectorPluginAudioStreamWAV, EditorInspectorPluginAudioStream);

public:
	virtual bool can_handle(Object *p_object) override;
};
