/**************************************************************************/
/*  quick_settings_dialog.h                                               */

#pragma once

#include "scene/ui/dialogs.h"
#include "scene/ui/label.h"

class Button;
class EditorSettingsDialog;
class MarginContainer;
class OptionButton;
class PanelContainer;
class VBoxContainer;

class SettingLabel : public Label {
	ORBLIT_CLASS(SettingLabel, Label);

	String setting_name;

public:
	virtual Control *make_custom_tooltip(const String &p_text) const override;

	SettingLabel(const String &p_text, const String &p_setting);
};

class QuickSettingsDialog : public AcceptDialog {
	ORBLIT_CLASS(QuickSettingsDialog, AcceptDialog);

	Vector<String> editor_styles;
	Vector<String> editor_themes;
	Vector<String> editor_scales;
	Vector<String> editor_network_modes;
	Vector<String> editor_check_for_updates;
	Vector<String> editor_directory_naming_conventions;

	void _fetch_setting_values();
	void _update_current_values();

	PanelContainer *settings_list_panel = nullptr;
	VBoxContainer *settings_list = nullptr;

	void _add_setting_control(const String &p_text, const String &p_setting, Control *p_control);

	OptionButton *style_option_button = nullptr;
	OptionButton *theme_option_button = nullptr;
	OptionButton *scale_option_button = nullptr;
	OptionButton *network_mode_option_button = nullptr;
	OptionButton *check_for_update_button = nullptr;
	OptionButton *directory_naming_convention_button = nullptr;

	Label *custom_theme_label = nullptr;
	EditorSettingsDialog *editor_settings_dialog = nullptr;

	void _style_selected(int p_id);
	void _theme_selected(int p_id);
	void _scale_selected(int p_id);
	void _network_mode_selected(int p_id);
	void _check_for_update_selected(int p_id);
	void _directory_naming_convention_selected(int p_id);
	void _set_setting_value(const String &p_setting, const Variant &p_value, bool p_restart_required = false);
	void _show_full_settings();

	Label *restart_required_label = nullptr;
	Button *restart_required_button = nullptr;

	void _request_restart();

protected:
	void _notification(int p_what);
	static void register_members();

public:
	QuickSettingsDialog();
};
