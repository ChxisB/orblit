/**************************************************************************/
/*  audio_stream_interactive_editor_plugin.h                              */

#pragma once

#include "editor/inspector/editor_inspector.h"
#include "editor/plugins/editor_plugin.h"
#include "scene/ui/dialogs.h"

class CheckBox;
class HSplitContainer;
class VSplitContainer;
class Tree;
class TreeItem;
class AudioStreamInteractive;

class AudioStreamInteractiveTransitionEditor : public AcceptDialog {
	ORBLIT_CLASS(AudioStreamInteractiveTransitionEditor, AcceptDialog);

	AudioStreamInteractive *audio_stream_interactive = nullptr;

	HSplitContainer *split = nullptr;
	Tree *tree = nullptr;

	Vector<TreeItem *> rows;

	CheckBox *transition_enabled = nullptr;
	OptionButton *transition_from = nullptr;
	OptionButton *transition_to = nullptr;
	OptionButton *fade_mode = nullptr;
	SpinBox *fade_beats = nullptr;
	OptionButton *filler_clip = nullptr;
	CheckBox *hold_previous = nullptr;

	bool updating_selection = false;
	int order_counter = 0;
	HashMap<Vector2i, int> selection_order;

	Vector<Vector2i> selected;
	bool updating = false;
	void _cell_selected(TreeItem *p_item, int p_column, bool p_selected);
	void _update_transitions();

	void _update_selection();
	void _edited();

protected:
	void _notification(int p_what);
	static void register_members();

public:
	void edit(Object *p_obj);

	AudioStreamInteractiveTransitionEditor();
};

//

class EditorInspectorPluginAudioStreamInteractive : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorPluginAudioStreamInteractive, EditorInspectorPlugin);

	AudioStreamInteractiveTransitionEditor *audio_stream_interactive_transition_editor = nullptr;

	void _edit(Object *p_object);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual void parse_end(Object *p_object) override;

	EditorInspectorPluginAudioStreamInteractive();
};

class AudioStreamInteractiveEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(AudioStreamInteractiveEditorPlugin, EditorPlugin);

public:
	virtual String get_plugin_name() const override { return "AudioStreamInteractive"; }

	AudioStreamInteractiveEditorPlugin();
};
