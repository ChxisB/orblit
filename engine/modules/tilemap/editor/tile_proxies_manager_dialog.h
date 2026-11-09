/**************************************************************************/
/*  tile_proxies_manager_dialog.h                                         */

#pragma once

#include "../tile_set.h"

#include "editor/inspector/editor_properties.h"
#include "scene/ui/dialogs.h"
#include "scene/ui/item_list.h"

class EditorPropertyVector2i;
class EditorUndoRedoManager;

class TileProxiesManagerDialog : public ConfirmationDialog {
	ORBLIT_CLASS(TileProxiesManagerDialog, ConfirmationDialog);

private:
	int committed_actions_count = 0;
	Ref<TileSet> tile_set;

	TileMapCell from;
	TileMapCell to;

	// GUI
	ItemList *source_level_list = nullptr;
	ItemList *coords_level_list = nullptr;
	ItemList *alternative_level_list = nullptr;

	EditorPropertyInteger *source_from_property_editor = nullptr;
	EditorPropertyVector2i *coords_from_property_editor = nullptr;
	EditorPropertyInteger *alternative_from_property_editor = nullptr;
	EditorPropertyInteger *source_to_property_editor = nullptr;
	EditorPropertyVector2i *coords_to_property_editor = nullptr;
	EditorPropertyInteger *alternative_to_property_editor = nullptr;

	PopupMenu *popup_menu = nullptr;
	void _right_clicked(int p_item, Vector2 p_local_mouse_pos, MouseButton p_mouse_button_index, Object *p_item_list);
	void _menu_id_pressed(int p_id);
	void _delete_selected_bindings();
	void _update_lists();
	void _update_enabled_property_editors();
	void _property_changed(const String &p_path, const Variant &p_value, const String &p_name, bool p_changing);
	void _add_button_pressed();

	void _clear_invalid_button_pressed();
	void _clear_all_button_pressed();

protected:
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _unhandled_key_input(Ref<InputEvent> p_event);
	virtual void cancel_pressed() override;
	static void register_members();

public:
	void update_tile_set(Ref<TileSet> p_tile_set);

	TileProxiesManagerDialog();
};
