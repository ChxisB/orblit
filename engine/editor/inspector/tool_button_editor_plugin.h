/**************************************************************************/
/*  tool_button_editor_plugin.h                                           */

#pragma once

#include "editor/inspector/editor_inspector.h"
#include "editor/plugins/editor_plugin.h"

class EditorInspectorToolButtonPlugin : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorToolButtonPlugin, EditorInspectorPlugin);

	void _call_action(const Variant &p_object, const StringName &p_property);
	void _invoke_callable(const Callable &p_callable);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual bool parse_property(Object *p_object, const Variant::Type p_type, const String &p_path, const PropertyHint p_hint, const String &p_hint_text, const BitField<PropertyUsageFlags> p_usage, const bool p_wide = false) override;
};

class ToolButtonEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(ToolButtonEditorPlugin, EditorPlugin);

public:
	virtual String get_plugin_name() const override { return "ToolButtonEditorPlugin"; }

	ToolButtonEditorPlugin();
};
