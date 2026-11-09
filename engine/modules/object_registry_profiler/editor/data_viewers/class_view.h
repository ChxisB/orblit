/**************************************************************************/
/*  class_view.h                                                          */

#pragma once

#include "../snapshot_data.h"
#include "snapshot_view.h"

class Tree;
class TreeItem;

struct ClassData {
	ClassData() {}
	ClassData(const String &p_name, const String &p_parent) :
			class_name(p_name), parent_class_name(p_parent) {}
	String class_name;
	String parent_class_name;
	HashSet<String> child_classes;
	LocalVector<SnapshotDataObject *> instances;
	TreeItem *tree_node = nullptr;
	HashMap<GameStateSnapshot *, int> recursive_instance_count_cache;

	int instance_count(GameStateSnapshot *p_snapshot = nullptr);
	int get_recursive_instance_count(HashMap<String, ClassData> &p_all_classes, GameStateSnapshot *p_snapshot = nullptr);
};

class SnapshotClassView : public SnapshotView {
	ORBLIT_CLASS(SnapshotClassView, SnapshotView);

protected:
	Tree *class_tree = nullptr;
	Tree *object_list = nullptr;
	Tree *diff_object_list = nullptr;

	void _object_selected(Tree *p_tree);
	void _class_selected();
	void _add_objects_to_class_map(HashMap<String, ClassData> &p_class_map, GameStateSnapshot *p_objects);
	void _notification(int p_what);

	Tree *_make_object_list_tree(const String &p_column_name);
	void _populate_object_list(GameStateSnapshot *p_snapshot, Tree *p_list, const String &p_name_base);
	void _update_lists();

public:
	SnapshotClassView();
	virtual void show_snapshot(GameStateSnapshot *p_data, GameStateSnapshot *p_diff_data) override;
};
