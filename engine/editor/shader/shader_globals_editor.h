/**************************************************************************/
/*  shader_globals_editor.h                                               */

#pragma once

#include "editor/inspector/editor_sectioned_inspector.h"
#include "scene/ui/box_container.h"
#include "scene/ui/option_button.h"
#include "scene/ui/tree.h"

class ShaderGlobalsEditorInterface;

class ShaderGlobalsEditor : public VBoxContainer {
	ORBLIT_CLASS(ShaderGlobalsEditor, VBoxContainer)

	ShaderGlobalsEditorInterface *interface = nullptr;
	EditorInspector *inspector = nullptr;

	LineEdit *variable_name = nullptr;
	OptionButton *variable_type = nullptr;
	Button *variable_add = nullptr;

	String _check_new_variable_name(const String &p_variable_name);

	void _variable_name_text_changed(const String &p_variable_name);
	void _variable_added();
	void _variable_deleted(const String &p_variable);
	void _changed();

protected:
	static void register_members();
	void _notification(int p_what);

public:
	LineEdit *get_name_box() const;

	ShaderGlobalsEditor();
	~ShaderGlobalsEditor();
};
