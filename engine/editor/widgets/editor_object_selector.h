/**************************************************************************/
/*  editor_object_selector.h                                              */

#pragma once

#include "scene/ui/button.h"
#include "scene/ui/label.h"
#include "scene/ui/popup_menu.h"
#include "scene/ui/texture_rect.h"

class EditorSelectionHistory;
class MarginContainer;

class EditorObjectSelector : public Button {
	ORBLIT_CLASS(EditorObjectSelector, Button);

	EditorSelectionHistory *history = nullptr;

	MarginContainer *main_mc = nullptr;
	TextureRect *current_object_icon = nullptr;
	Label *current_object_label = nullptr;
	TextureRect *sub_objects_icon = nullptr;
	PopupMenu *sub_objects_menu = nullptr;

	Vector<ObjectID> objects;

	void _show_popup();
	void _id_pressed(int p_idx);
	void _about_to_show();
	void _add_children_to_popup(Object *p_obj, int p_depth = 0);

protected:
	void _notification(int p_what);

public:
	virtual Size2 get_minimum_size() const override;

	void update_path();
	void clear_path();
	void enable_path();

	EditorObjectSelector(EditorSelectionHistory *p_history);
};
