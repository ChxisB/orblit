/**************************************************************************/
/*  snapshot_view.cpp                                                     */

#include "snapshot_view.h"

#include "scene/ui/tree.h"

void SnapshotView::clear_snapshot() {
	snapshot_data = nullptr;
	diff_data = nullptr;
	for (int i = 0; i < get_child_count(); i++) {
		get_child(i)->queue_destroy();
	}
}

void SnapshotView::show_snapshot(GameStateSnapshot *p_data, GameStateSnapshot *p_diff_data) {
	clear_snapshot();
	snapshot_data = p_data;
	diff_data = p_diff_data;
}

bool SnapshotView::is_showing_snapshot(GameStateSnapshot *p_data, GameStateSnapshot *p_diff_data) {
	return p_data == snapshot_data && p_diff_data == diff_data;
}

Vector<TreeItem *> SnapshotView::_get_children_recursive(Tree *p_tree) {
	Vector<TreeItem *> found_items;
	List<TreeItem *> items_to_check;
	if (p_tree && p_tree->get_root()) {
		items_to_check.push_back(p_tree->get_root());
	}
	while (items_to_check.size() > 0) {
		TreeItem *to_check = items_to_check.front()->get();
		items_to_check.pop_front();
		found_items.push_back(to_check);
		for (int i = 0; i < to_check->get_child_count(); i++) {
			items_to_check.push_back(to_check->get_child(i));
		}
	}
	return found_items;
}
