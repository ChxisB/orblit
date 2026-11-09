/**************************************************************************/
/*  input_event_editor_plugin.h                                           */

#pragma once

#include "editor/inspector/editor_inspector.h"
#include "editor/plugins/editor_plugin.h"
#include "editor/settings/action_map_editor.h"

class InputEventConfigContainer : public VBoxContainer {
	ORBLIT_CLASS(InputEventConfigContainer, VBoxContainer);

	Label *input_event_text = nullptr;

	Ref<InputEvent> input_event;
	InputEventConfigurationDialog *config_dialog = nullptr;

	void _config_dialog_confirmed();
	void _configure_pressed();

	void _event_changed();

public:
	void set_event(const Ref<InputEvent> &p_event);

	InputEventConfigContainer();
};

class EditorInspectorPluginInputEvent : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorPluginInputEvent, EditorInspectorPlugin);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual void parse_begin(Object *p_object) override;
};

class InputEventEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(InputEventEditorPlugin, EditorPlugin);

public:
	virtual String get_plugin_name() const override { return "InputEvent"; }

	InputEventEditorPlugin();
};
