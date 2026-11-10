/**************************************************************************/
/*  particles_editor_plugin.h                                             */

#pragma once

#include "editor/plugins/editor_plugin.h"

class CheckBox;
class ConfirmationDialog;
class HBoxContainer;
class MenuButton;
class OptionButton;
class StageDialog;
class SpinBox;

class ParticlesEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(ParticlesEditorPlugin, EditorPlugin);

private:
	enum {
		MENU_OPTION_CONVERT,
		MENU_RESTART
	};

	HBoxContainer *toolbar = nullptr;
	MenuButton *menu = nullptr;

protected:
	String handled_type;
	String conversion_option_name;

	Entity *edited_node = nullptr;

	void _notification(int p_what);

	bool need_show_lifetime_dialog(SpinBox *p_seconds);
	virtual void _menu_callback(int p_idx);

	virtual void _add_menu_options(PopupMenu *p_menu) {}
	virtual Entity *_convert_particles() = 0;

public:
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	ParticlesEditorPlugin();
};
