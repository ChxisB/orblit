/**************************************************************************/
/*  atlas_merging_dialog.h                                                */

#pragma once

#include "../tile_set.h"

#include "editor/inspector/editor_properties.h"
#include "scene/ui/dialogs.h"
#include "scene/ui/item_list.h"
#include "scene/ui/texture_rect.h"

class EditorFileDialog;
class EditorPropertyVector2i;

class AtlasMergingDialog : public ConfirmationDialog {
	ORBLIT_CLASS(AtlasMergingDialog, ConfirmationDialog);

private:
	int committed_actions_count = 0;
	bool delete_original_atlases = true;
	Ref<TileSetAtlasSource> merged;
	LocalVector<HashMap<Vector2i, Vector2i>> merged_mapping;
	Ref<TileSet> tile_set;

	// Settings.
	int next_line_after_column = 30;

	// GUI.
	ItemList *atlas_merging_atlases_list = nullptr;
	EditorPropertyInteger *columns_editor_property = nullptr;
	TextureRect *preview = nullptr;
	Label *select_2_atlases_label = nullptr;
	EditorFileDialog *editor_file_dialog = nullptr;
	Button *merge_button = nullptr;

	void _property_changed(const StringName &p_property, const Variant &p_value, const String &p_field, bool p_changing);

	void _generate_merged(const Vector<Ref<TileSetAtlasSource>> &p_atlas_sources, int p_max_columns);
	void _update_texture();
	void _merge_confirmed(const String &p_path);

protected:
	virtual void ok_pressed() override;
	virtual void cancel_pressed() override;
	virtual void custom_action(const String &) override;

	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;

	void _notification(int p_what);

public:
	void update_tile_set(Ref<TileSet> p_tile_set);

	AtlasMergingDialog();
};
