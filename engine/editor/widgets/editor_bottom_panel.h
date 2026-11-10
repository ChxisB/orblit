/**************************************************************************/
/*  editor_bottom_panel.h                                                 */

#pragma once

#include "editor/docks/dock_tab_container.h"
#include "scene/ui/texture_progress_bar.h"

class Button;
class ConfigFile;
class EditorDock;
class EditorToaster;
class HBoxContainer;

class ProgressIndicator : public TextureProgressBar {
	ORBLIT_CLASS(ProgressIndicator, TextureProgressBar);

protected:
	void _notification(int p_what);
	static void register_members();

	virtual void gui_input(const Ref<InputEvent> &p_event) override;

public:
	ProgressIndicator();
};

class EditorBottomPanel : public DockTabContainer {
	ORBLIT_CLASS(EditorBottomPanel, DockTabContainer);

	HBoxContainer *bottom_hbox = nullptr;
	EditorToaster *editor_toaster = nullptr;
	ProgressIndicator *progress_indicator = nullptr;
	Button *pin_button = nullptr;
	Button *expand_button = nullptr;

	int previous_tab = -1;
	bool lock_panel_switching = false;
	LocalVector<EditorDock *> bottom_docks;
	HashMap<String, int> dock_offsets;

	LocalVector<Button *> legacy_buttons;
	void _on_button_visibility_changed(Button *p_button, EditorDock *p_dock);

	void _repaint();
	void _on_tab_changed(int p_idx);
	void _pin_button_toggled(bool p_pressed);
	void _expand_button_toggled(bool p_pressed);
	void _update_center_split_offset();
	EditorDock *_get_dock_from_control(Control *p_control) const;

protected:
	void _notification(int p_what);

public:
	virtual void dock_closed(EditorDock *p_dock) override;
	virtual void dock_focused(EditorDock *p_dock, bool p_was_visible) override;
	virtual void update_visibility() override { show(); } // Never hide bottom panel.
	virtual TabStyle get_tab_style() const override;
	virtual bool can_switch_dock() const override;
	virtual Rect2 get_floating_dock_rect(EditorDock *p_dock) override;
	virtual void load_selected_tab(int p_idx) override;

	void save_layout_to_config(Ref<ConfigFile> p_config_file, const String &p_section) const;
	void load_layout_from_config(Ref<ConfigFile> p_config_file, const String &p_section);

	Button *add_item(String p_text, Control *p_item, const Ref<Shortcut> &p_shortcut = nullptr, bool p_at_front = false);
	void remove_item(Control *p_item);
	void make_item_visible(Control *p_item, bool p_visible = true, bool p_ignore_lock = false);
	void hide_bottom_panel();
	void toggle_last_opened_bottom_panel();
	void set_expanded(bool p_expanded);
	void _theme_changed();
	bool is_locked() const { return lock_panel_switching; }

	ProgressIndicator *get_progress_indicator() { return progress_indicator; }

	void set_bottom_panel_offset(int p_offset);
	int get_bottom_panel_offset();

	EditorBottomPanel();
	~EditorBottomPanel();
};
