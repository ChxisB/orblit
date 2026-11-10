/**************************************************************************/
/*  refcounted_view.h                                                     */

#pragma once

#include "../snapshot_data.h"
#include "shared_controls.h"
#include "snapshot_view.h"

class Tree;
class HSplitContainer;

class SnapshotRefCountedView : public SnapshotView {
	ORBLIT_CLASS(SnapshotRefCountedView, SnapshotView);

protected:
	Tree *refs_list = nullptr;
	VBoxContainer *ref_details = nullptr;
	TreeSortAndFilterBar *filter_bar = nullptr;
	HSplitContainer *refs_view = nullptr;

	HashMap<TreeItem *, SnapshotDataObject *> item_data_map;
	HashMap<SnapshotDataObject *, TreeItem *> data_item_map;
	HashMap<TreeItem *, TreeItem *> reference_item_map;

	void _refcounted_selected();
	void _insert_data(GameStateSnapshot *p_snapshot, const String &p_name);
	void _ref_selected(Tree *p_source_tree);
	void _set_split_to_center();

public:
	SnapshotRefCountedView();
	virtual void show_snapshot(GameStateSnapshot *p_data, GameStateSnapshot *p_diff_data) override;
};
