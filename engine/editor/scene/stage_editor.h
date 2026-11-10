/**************************************************************************/
/*  stage_editor.h                                                        */

#pragma once

#include "scene/ui/dialogs.h"
#include "scene/ui/tree.h"

class CheckBox;
class CheckButton;
class EditorSelection;
class FilterLineEdit;
class Label;
class TextureRect;
class Timer;

class StageEditor : public Control {
	ORBLIT_CLASS(StageEditor, Control);

	EditorSelection *editor_selection = nullptr;

	enum StageEditorButton {
		BUTTON_SUBSCENE = 0,
		BUTTON_VISIBILITY = 1,
		BUTTON_SCRIPT = 2,
		BUTTON_LOCK = 3,
		BUTTON_GROUP = 4,
		BUTTON_WARNING = 5,
		BUTTON_SIGNALS = 6,
		BUTTON_GROUPS = 7,
		BUTTON_PIN = 8,
		BUTTON_UNIQUE = 9,
	};

	struct CachedNode {
		Entity *node = nullptr;
		TreeItem *item = nullptr;
		int index = -1;
		bool dirty = true;
		bool has_moved_children = false;
		bool removed = false;

		// Store the iterator for faster removal. This is safe as
		// HashMap never moves elements.
		HashMap<Entity *, CachedNode>::Iterator cache_iterator;
		// This is safe because it gets compared to a uint8_t.
		uint16_t delete_serial = UINT16_MAX;

		// To know whether to update children or not.
		bool can_run = false;

		CachedNode() = delete; // Always an error.
		CachedNode(Entity *p_node, TreeItem *p_item) :
				node(p_node), item(p_item) {}
	};

	struct NodeCache {
		~NodeCache() {
			clear();
		}

		NodeCache(StageEditor *p_editor) :
				editor(p_editor) {}

		HashMap<Entity *, CachedNode>::Iterator add(Entity *p_node, TreeItem *p_item);
		HashMap<Entity *, CachedNode>::Iterator get(Entity *p_node, bool p_deleted_ok = true);
		bool has(Entity *p_node);
		void remove(Entity *p_node, bool p_recursive = false);
		void mark_dirty(Entity *p_node, bool p_parents = true);
		void mark_children_dirty(Entity *p_node, bool p_recursive = false);

		void delete_pending();
		void clear();

		StageEditor *editor;
		HashMap<Entity *, CachedNode> cache;
		HashSet<CachedNode *> to_delete;
		ObjectID current_scene_id;
		Entity *current_pinned_node = nullptr;
		bool current_has_pin = false;
		bool force_update = false;
		uint8_t delete_serial = 0;
	};

	NodeCache node_cache;

	Tree *tree = nullptr;
	Entity *selected = nullptr;

	String filter;
	String filter_term_warning;
	bool show_all_nodes = false;

	AcceptDialog *error = nullptr;
	AcceptDialog *warning = nullptr;

	ConfirmationDialog *revoke_dialog = nullptr;
	Label *revoke_dialog_label = nullptr;
	CheckBox *ask_before_revoke_checkbox = nullptr;
	Entity *revoke_node = nullptr;

	bool auto_expand_selected = true;
	bool hide_filtered_out_parents = false;
	bool accessibility_warnings = false;
	bool connect_to_script_mode = false;
	bool connecting_signal = false;
	bool update_when_invisible = true;

	int blocked;

	void _compute_hash(Entity *p_node, uint64_t &hash);
	void _reset();
	PackedStringArray _get_node_configuration_warnings(Entity *p_node);
	PackedStringArray _get_node_accessibility_configuration_warnings(Entity *p_node);

	void _update_node_path(Entity *p_node, bool p_recursive = true);
	void _update_node_subtree(Entity *p_node, TreeItem *p_parent, bool p_force = false);
	void _update_node(Entity *p_node, TreeItem *p_item, bool p_part_of_subscene);
	void _update_if_clean();

	void _test_update_tree();
	bool _update_filter(TreeItem *p_parent = nullptr, bool p_scroll_to_selected = false);
	bool _update_filter_helper(TreeItem *p_parent, bool p_scroll_to_selected, TreeItem *&r_last_selected);
	bool _node_matches_class_term(const Entity *p_item_node, const String &p_term);
	bool _item_matches_all_terms(TreeItem *p_item, const PackedStringArray &p_terms);
	void _stage_changed();
	void _stage_run_mode_changed();

	void _move_node_children(HashMap<Entity *, CachedNode>::Iterator &p_I);
	void _move_node_item(TreeItem *p_parent, HashMap<Entity *, CachedNode>::Iterator &p_I, TreeItem *p_correct_prev = nullptr);

	void _node_child_order_changed(Entity *p_node);
	void _node_editor_state_changed(Entity *p_node);
	void _node_added(Entity *p_node);
	void _node_removed(Entity *p_node);
	void _node_renamed(Entity *p_node);

	TreeItem *_find(TreeItem *p_node, const EntityPath &p_path);
	void _notification(int p_what);
	void _selected_changed();
	void _deselect_items();

	void _cell_collapsed(Object *p_obj);

	uint64_t last_hash;

	bool can_rename;
	bool can_open_instance;
	bool updating_tree = false;
	bool show_enabled_subscene = false;
	bool is_scene_tree_dock = false;

	void _edited();
	void _renamed(TreeItem *p_item, TreeItem *p_batch_item, Entity *p_node = nullptr);

	HashSet<Entity *> marked;
	bool marked_selectable = false;
	bool marked_children_selectable = false;
	bool display_foreign = false;
	bool tree_dirty = true;
	bool pending_test_update = false;
	bool pending_selection_update = false;
	Timer *update_node_tooltip_delay = nullptr;

	bool visibility_drag_value = false;
	Vector2 visibility_drag_start_pos;
	ObjectID visibility_drag_start_node;
	LocalVector<ObjectID> visibility_drag_nodes;

	static void register_members();

	void _gui_input(const Ref<InputEvent> &p_event);
	void _cell_button_pressed(Object *p_item, int p_column, int p_id, MouseButton p_button);
	void _toggle_visible(Entity *p_node);
	void _cell_multi_selected(Object *p_object, int p_cell, bool p_selected);
	void _process_selection_update();
	void _update_selection(TreeItem *item);
	void _node_script_changed(Entity *p_node);
	void _node_visibility_changed(Entity *p_node);
	void _update_visibility_color(Entity *p_node, TreeItem *p_item);
	void _set_item_custom_color(TreeItem *p_item, Color p_color);
	void _update_node_tooltip(Entity *p_node, TreeItem *p_item);
	void _queue_update_node_tooltip(Entity *p_node, TreeItem *p_item);
	void _tree_scroll_to_item(ObjectID p_item_id, bool p_center_on_item);
	void _reset_visibility_drag();

	void _selection_changed();
	Entity *get_scene_node() const;

	Variant get_drag_data_fw(const Point2 &p_point, Control *p_from);
	bool can_drop_data_fw(const Point2 &p_point, const Variant &p_data, Control *p_from) const;
	void drop_data_fw(const Point2 &p_point, const Variant &p_data, Control *p_from);

	void _empty_clicked(const Vector2 &p_pos, MouseButton p_button);
	void _rmb_select(const Vector2 &p_pos, MouseButton p_button = MouseButton::RIGHT);

	void _warning_changed(Entity *p_for_node);
	void _update_marking_list(const HashSet<Entity *> &p_marked);

	Timer *update_timer = nullptr;

	LocalVector<StringName> *script_types;
	bool _has_drop_selection(TreeItem *p_item, const Point2 &p_point) const;
	bool _is_script_type(const StringName &p_type) const;
	EntityPath _get_node_path(Entity *p_node) const;
	Entity *_get_node(const EntityPath &p_path) const;

	Vector<StringName> valid_types;

	void _update_ask_before_revoking_unique_name();
	void _revoke_unique_name();

public:
	// Public for use as signal callback.
	void _update_tree(bool p_scroll_to_selected = false);

	void rename_node(Entity *p_node, const String &p_name, TreeItem *p_item = nullptr);

	void set_filter(const String &p_filter);
	String get_filter() const;
	String get_filter_term_warning();
	void set_show_all_nodes(bool p_show_all_nodes);

	void set_as_scene_tree_dock();
	void set_display_foreign_nodes(bool p_display);

	void set_marked(const HashSet<Entity *> &p_marked, bool p_selectable = true, bool p_children_selectable = true);
	void set_marked(Entity *p_marked, bool p_selectable = true, bool p_children_selectable = true);
	void set_selected(Entity *p_node, bool p_emit_selected = true);
	Entity *get_selected();
	void set_can_rename(bool p_can_rename) { can_rename = p_can_rename; }
	void set_editor_selection(EditorSelection *p_selection);
	TreeItem *get_node_item(Entity *p_node) const;

	void set_show_enabled_subscene(bool p_show) { show_enabled_subscene = p_show; }
	void set_valid_types(const Vector<StringName> &p_valid);
	void clear_cache();

	inline void update_tree() { _update_tree(); }

	void set_auto_expand_selected(bool p_auto, bool p_update_settings);
	void set_hide_filtered_out_parents(bool p_hide, bool p_update_settings);
	void set_accessibility_warnings(bool p_enable, bool p_update_settings);
	void set_connect_to_script_mode(bool p_enable);
	void set_connecting_signal(bool p_enable);
	void set_update_when_invisible(bool p_enable);

	Tree *get_scene_tree() { return tree; }

	void update_warning();

	StageEditor(bool p_label = true, bool p_can_rename = false, bool p_can_open_instance = false);
	~StageEditor();
};

class StageDialog : public ConfirmationDialog {
	ORBLIT_CLASS(StageDialog, ConfirmationDialog);

	VBoxContainer *content = nullptr;
	StageEditor *tree = nullptr;
	FilterLineEdit *filter = nullptr;
	CheckButton *show_all_nodes = nullptr;
	LocalVector<TextureRect *> valid_type_icons;
	HBoxContainer *allowed_types_hbox = nullptr;

	void _select();
	void _cancel();
	void _selected_changed();
	void _filter_changed(const String &p_filter);
	void _show_all_nodes_changed(bool p_button_pressed);

protected:
	void _update_valid_type_icons();
	void _notification(int p_what);
	static void register_members();

public:
	void popup_scenetree_dialog(Entity *p_selected_node = nullptr, Entity *p_marked_node = nullptr, bool p_marked_node_selectable = true, bool p_marked_node_children_selectable = true);
	void set_valid_types(const Vector<StringName> &p_valid);

	StageEditor *get_scene_tree() { return tree; }
	LineEdit *get_filter_line_edit();

	StageDialog();
};
