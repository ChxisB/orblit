/**************************************************************************/
/*  animation_library_editor.h                                            */

#pragma once

#include "core/containers/vector.h"
#include "editor/plugins/editor_plugin.h"
#include "scene/animation/animation_mixer.h"
#include "scene/ui/dialogs.h"
#include "scene/ui/tree.h"

class AnimationMixer;
class EditorFileDialog;

class AnimationLibraryEditor : public AcceptDialog {
	ORBLIT_CLASS(AnimationLibraryEditor, AcceptDialog)

	enum {
		LIB_BUTTON_ADD,
		LIB_BUTTON_LOAD,
		LIB_BUTTON_PASTE,
		LIB_BUTTON_FILE,
		LIB_BUTTON_DELETE,
	};
	enum {
		ANIM_BUTTON_COPY,
		ANIM_BUTTON_FILE,
		ANIM_BUTTON_DELETE,
	};

	enum FileMenuAction {
		FILE_MENU_SAVE_LIBRARY,
		FILE_MENU_SAVE_AS_LIBRARY,
		FILE_MENU_MAKE_LIBRARY_UNIQUE,
		FILE_MENU_EDIT_LIBRARY,

		FILE_MENU_SAVE_ANIMATION,
		FILE_MENU_SAVE_AS_ANIMATION,
		FILE_MENU_MAKE_ANIMATION_UNIQUE,
		FILE_MENU_EDIT_ANIMATION,
	};

	enum FileDialogAction {
		FILE_DIALOG_ACTION_OPEN_LIBRARY,
		FILE_DIALOG_ACTION_SAVE_LIBRARY,
		FILE_DIALOG_ACTION_OPEN_ANIMATION,
		FILE_DIALOG_ACTION_SAVE_ANIMATION,
	};

	FileDialogAction file_dialog_action = FILE_DIALOG_ACTION_OPEN_ANIMATION;

	StringName file_dialog_animation;
	StringName file_dialog_library;

	Button *new_library_button = nullptr;
	Button *load_library_button = nullptr;

	AcceptDialog *error_dialog = nullptr;
	bool adding_animation = false;
	StringName adding_animation_to_library;
	EditorFileDialog *file_dialog = nullptr;
	ConfirmationDialog *add_library_dialog = nullptr;
	LineEdit *add_library_name = nullptr;
	Label *add_library_validate = nullptr;
	PopupMenu *file_popup = nullptr;

	Tree *tree = nullptr;

	AnimationMixer *mixer = nullptr;

	void _add_library();
	void _add_library_validate(const String &p_name);
	void _add_library_confirm();
	void _load_library();
	void _load_file(const String &p_path);
	void _load_files(const PackedStringArray &p_paths);

	void _save_mixer_lib_folding(TreeItem *p_item);
	Vector<String> _load_mixer_libs_folding();
	String _get_mixer_signature() const;

	void _item_renamed();
	void _button_pressed(TreeItem *p_item, int p_column, int p_id, MouseButton p_button);

	void _file_popup_selected(int p_id);

	bool updating = false;

protected:
	void _notification(int p_what);
	void _update_editor(Object *p_mixer);
	virtual void shortcut_input(const Ref<InputEvent> &p_event) override;
	static void register_members();

public:
	void set_animation_mixer(Object *p_mixer);
	void show_dialog();
	void update_tree();
	AnimationLibraryEditor();
};
