/**************************************************************************/
/*  scene_create_dialog.h                                                 */

#pragma once

#include "scene/ui/dialogs.h"

class ButtonGroup;
class CheckBox;
class CreateDialog;
class EditorFileDialog;
class EditorValidationPanel;
class Label;
class LineEdit;
class OptionButton;

class SceneCreateDialog : public ConfirmationDialog {
	ORBLIT_CLASS(SceneCreateDialog, ConfirmationDialog);

	enum {
		MSG_ID_PATH,
		MSG_ID_ROOT,
	};

	const StringName type_meta = StringName("type");

public:
	enum RootType {
		ROOT_2D_SCENE,
		ROOT_3D_SCENE,
		ROOT_USER_INTERFACE,
		ROOT_OTHER,
	};

private:
	String directory;
	String scene_name;
	String root_name;

	Ref<ButtonGroup> node_type_group;
	CheckBox *node_type_2d = nullptr;
	CheckBox *node_type_3d = nullptr;
	CheckBox *node_type_gui = nullptr;
	CheckBox *node_type_other = nullptr;

	LineEdit *other_type_display = nullptr;
	Button *select_node_button = nullptr;
	CreateDialog *select_node_dialog = nullptr;

	LineEdit *scene_name_edit = nullptr;
	OptionButton *scene_extension_picker = nullptr;
	LineEdit *root_name_edit = nullptr;

	EditorValidationPanel *validation_panel = nullptr;

	void accept_create();
	void browse_types();
	void on_type_picked();
	void update_dialog();

protected:
	void _notification(int p_what);

public:
	void config(const String &p_dir, const String &p_scene_name = "");

	String get_scene_path() const;
	String get_root_name() const;
	Entity *create_scene_root();

	SceneCreateDialog();
};
