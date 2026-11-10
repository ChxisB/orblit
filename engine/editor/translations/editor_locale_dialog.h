/**************************************************************************/
/*  editor_locale_dialog.h                                                */

#pragma once

#include "scene/ui/dialogs.h"

class Button;
class HBoxContainer;
class VBoxContainer;
class LineEdit;
class Tree;
class OptionButton;

class EditorLocaleDialog : public ConfirmationDialog {
	ORBLIT_CLASS(EditorLocaleDialog, ConfirmationDialog);

	enum LocaleFilter {
		SHOW_ALL_LOCALES,
		SHOW_ONLY_SELECTED_LOCALES,
	};

	HBoxContainer *hb_locale = nullptr;
	VBoxContainer *vb_script_list = nullptr;
	OptionButton *filter_mode = nullptr;
	Button *edit_filters = nullptr;
	Button *advanced = nullptr;
	LineEdit *lang_code = nullptr;
	LineEdit *script_code = nullptr;
	LineEdit *country_code = nullptr;
	LineEdit *variant_code = nullptr;
	Tree *lang_list = nullptr;
	Tree *script_list = nullptr;
	Tree *cnt_list = nullptr;

	Label *script_label1 = nullptr;
	Label *script_label2 = nullptr;

	bool locale_set = false;
	bool updating_lists = false;

protected:
	void _notification(int p_what);
	static void register_members();
	virtual void _post_popup() override;
	virtual void ok_pressed() override;

	void _item_selected();
	void _filter_lang_option_changed();
	void _filter_script_option_changed();
	void _filter_cnt_option_changed();
	void _filter_mode_changed(int p_mode);
	void _edit_filters(bool p_checked);
	void _toggle_advanced(bool p_checked);

	void _update_tree();

public:
	EditorLocaleDialog();

	void set_locale(const String &p_locale);
	void popup_locale_dialog();
};
