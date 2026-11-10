/**************************************************************************/
/*  rename_dialog.h                                                       */

#pragma once

#include "editor/scene/stage_editor.h"
#include "scene/ui/dialogs.h"
#include "scene/ui/line_edit.h"

class Button;
class CheckBox;
class CheckButton;
class Label;
class OptionButton;
class SpinBox;
class TabContainer;

class RenameDialog : public ConfirmationDialog {
	ORBLIT_CLASS(RenameDialog, ConfirmationDialog);

	virtual void ok_pressed() override { rename(); }
	void _cancel_pressed() {}
	void _features_toggled(bool pressed);
	void _insert_text(const String &text);
	void _update_substitute();
	bool _is_main_field(LineEdit *line_edit);

	void _iterate_scene(const Entity *node, const Array &selection, int *count);
	String _apply_rename(const Entity *node, int count);
	String _substitute(const String &subject, const Entity *node, int count);
	String _regex(const String &pattern, const String &subject, const String &replacement);
	String _postprocess(const String &subject);
	void _update_preview(const String &new_text = "");
	void _update_preview_int(int new_value = 0);
	static void _error_handler(void *p_self, const char *p_func, const char *p_file, int p_line, const char *p_error, const char *p_errorexp, bool p_editor_notify, ErrorHandlerType p_type);

	StageEditor *scene_tree_editor = nullptr;
	int global_count = 0;

	LineEdit *lne_search = nullptr;
	LineEdit *lne_replace = nullptr;
	LineEdit *lne_prefix = nullptr;
	LineEdit *lne_suffix = nullptr;

	TabContainer *tabc_features = nullptr;

	CheckBox *cbut_substitute = nullptr;
	CheckButton *cbut_regex = nullptr;
	CheckBox *cbut_process = nullptr;
	CheckBox *chk_per_level_counter = nullptr;

	Button *but_insert_name = nullptr;
	Button *but_insert_parent = nullptr;
	Button *but_insert_type = nullptr;
	Button *but_insert_scene = nullptr;
	Button *but_insert_root = nullptr;
	Button *but_insert_count = nullptr;

	SpinBox *spn_count_start = nullptr;
	SpinBox *spn_count_step = nullptr;
	SpinBox *spn_count_padding = nullptr;

	OptionButton *opt_style = nullptr;
	OptionButton *opt_case = nullptr;

	Label *lbl_preview_title = nullptr;
	Label *lbl_preview = nullptr;

	List<Pair<EntityPath, String>> to_rename;
	Entity *preview_node = nullptr;
	bool lock_preview_update = false;
	ErrorHandlerList eh;
	bool has_errors = false;

protected:
	static void register_members();
	virtual void _post_popup() override;

public:
	void reset();
	void rename();

	RenameDialog(StageEditor *p_scene_tree_editor);
};
