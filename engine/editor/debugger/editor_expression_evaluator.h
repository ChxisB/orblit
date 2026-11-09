/**************************************************************************/
/*  editor_expression_evaluator.h                                         */

#pragma once

#include "scene/ui/box_container.h"

class Button;
class CheckBox;
class EditorDebuggerInspector;
class LineEdit;
class ScriptEditorDebugger;

class EditorExpressionEvaluator : public VBoxContainer {
	ORBLIT_CLASS(EditorExpressionEvaluator, VBoxContainer)

private:
	LineEdit *expression_input = nullptr;
	CheckBox *clear_on_run_checkbox = nullptr;
	Button *evaluate_btn = nullptr;
	Button *clear_btn = nullptr;

	EditorDebuggerInspector *inspector = nullptr;

	LocalVector<String> expression_history;
	int expression_index = -1;

	void _line_edit_gui_input(const Ref<InputEvent> &p_event);
	void _evaluate();
	void _clear();

	void _remote_object_selected(ObjectID p_id);
	void _on_expression_input_changed(const String &p_expression);
	void _on_debugger_breaked(bool p_breaked, bool p_can_debug);
	void _on_debugger_clear_execution(Ref<Script> p_stack_script);

protected:
	ScriptEditorDebugger *editor_debugger = nullptr;

	void _notification(int p_what);

public:
	void on_start();
	void set_editor_debugger(ScriptEditorDebugger *p_editor_debugger);
	void add_value(const Array &p_array);

	EditorExpressionEvaluator();
};
