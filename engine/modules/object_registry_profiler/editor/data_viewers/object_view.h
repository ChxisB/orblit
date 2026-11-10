/**************************************************************************/
/*  object_view.h                                                         */

#pragma once

#include "../snapshot_data.h"
#include "shared_controls.h"
#include "snapshot_view.h"

class Tree;
class HSplitContainer;

class SnapshotObjectView : public SnapshotView {
	ORBLIT_CLASS(SnapshotObjectView, SnapshotView);

protected:
	Tree *object_list = nullptr;
	Tree *inbound_tree = nullptr;
	Tree *outbound_tree = nullptr;
	VBoxContainer *object_details = nullptr;
	TreeSortAndFilterBar *filter_bar = nullptr;
	HSplitContainer *objects_view = nullptr;

	HashMap<TreeItem *, SnapshotDataObject *> item_data_map;
	HashMap<SnapshotDataObject *, TreeItem *> data_item_map;
	HashMap<TreeItem *, TreeItem *> reference_item_map;

	void _object_selected();
	void _insert_data(GameStateSnapshot *p_snapshot, const String &p_name);
	Tree *_make_references_list(Control *p_container, const String &p_name, const String &p_col_1, const String &p_col_1_tooltip, const String &p_col_2, const String &p_col_2_tooltip);
	void _reference_selected(Tree *p_source_tree);

public:
	SnapshotObjectView();
	virtual void show_snapshot(GameStateSnapshot *p_data, GameStateSnapshot *p_diff_data) override;
};
