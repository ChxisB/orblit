/**************************************************************************/
/*  snapshot_view.h                                                       */

#pragma once

#include "../snapshot_data.h"

#include "scene/ui/control.h"

class Tree;
class TreeItem;

class SnapshotView : public Control {
	ORBLIT_CLASS(SnapshotView, Control);

protected:
	GameStateSnapshot *snapshot_data = nullptr;
	GameStateSnapshot *diff_data = nullptr;

	Vector<TreeItem *> _get_children_recursive(Tree *p_tree);

public:
	String view_name;

	virtual void show_snapshot(GameStateSnapshot *p_data, GameStateSnapshot *p_diff_data = nullptr);
	virtual void clear_snapshot();
	bool is_showing_snapshot(GameStateSnapshot *p_data, GameStateSnapshot *p_diff_data);
};
