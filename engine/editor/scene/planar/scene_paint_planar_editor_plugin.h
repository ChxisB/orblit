/**************************************************************************/
/*  scene_paint_planar_editor_plugin.h                                        */

#pragma once

#include "editor/plugins/editor_plugin.h"

class Entity2D;
class HBoxContainer;
class BaseButton;
class CheckBox;
class SpinBox;
class ItemList;
class OptionButton;

class ScenePaint2DEditor : public Control {
	ORBLIT_CLASS(ScenePaint2DEditor, Control);

	inline static ScenePaint2DEditor *singleton = nullptr;

	friend class ScenePaint2DEditorPlugin;

	enum PaintMode {
		PAINT_MODE_FREE,
		PAINT_MODE_SNAP_GRID,
		PAINT_MODE_SNAP_GRID_CELL_CENTER,
	};

	enum PickMode {
		PICK_FILE_SYSTEM,
		PICK_SCENE_TREE,
		PICK_CANVAS_ITEM,
		PICK_RECENT_LIST,
		PICK_CUSTOM_SOURCE,
	};

	enum InputTool {
		INPUT_TOOL_NONE,
		INPUT_TOOL_PAINT,
		INPUT_TOOL_ERASE,
		INPUT_TOOL_PAN,
		INPUT_TOOL_PICK,
		INPUT_TOOL_QUICK_PICK,
	};

	enum MenuItem {
		MENU_ITEM_FREE_PAINT_MODE,
		MENU_ITEM_SNAP_TO_GRID_PAINT_MODE,
		MENU_ITEM_SNAP_TO_GRID_CELL_CENTER_PAINT_MODE,
		MENU_ITEM_ALLOW_OVERLAPPING = 4,
	};

	bool pinned = false;
	bool edit_properties = false;
	bool grid = false;
	bool is_tool_selected = false;
	bool allow_overlapping = false;

	PaintMode paint_mode = PAINT_MODE_FREE;
	PaintMode quick_paint_mode = PAINT_MODE_SNAP_GRID_CELL_CENTER;
	InputTool input_tool = INPUT_TOOL_NONE;

	Size2i grid_step = Size2i(16, 16);
	Size2i grid_offset = Size2i(0, 0);

	int recent_idx = -1;

	PopupMenu *advanced_settings_popup = nullptr;
	Control *custom_overlay = nullptr;
	Control *viewport = nullptr;
	HashMap<Entity *, Entity *> pinned_nodes;

	Entity2D *cache_node = nullptr;
	Entity2D *node = nullptr;
	Entity2D *instance = nullptr;
	Entity2D *instance_container = nullptr;

	HBoxContainer *toolbar = nullptr;

	Button *pin_node_button = nullptr;
	Button *scene_picker_button = nullptr;
	OptionButton *recent_scenes_button = nullptr;
	Button *edit_properties_button = nullptr;

	Button *advanced_settings_button = nullptr;

	Entity2D *selected_scene = nullptr;

	HashMap<Control *, Callable> custom_sources;

	void _can_handle(bool p_is_node_2d, bool p_edit);

	void _edit(Object *p_object);

	void _update_node(Object *p_object = nullptr);
	bool _is_node_valid();

	void _add_instance(bool p_show = false);
	void _clear_instance(bool p_hide = false);
	void _update_instance();
	bool _is_instance_valid();

	void _draw_overlay();
	void _update_draw_overlay();
	void _update_hint_label();

	void _gui_input_viewport(const Ref<InputEvent> &p_event);
	void _add_node_at_pos();
	void _remove_node_at_pos();

	Vector2 _get_mouse_grid_cell();

	void _set_pinned(bool p_pinned, Entity *p_pinned_node = nullptr);
	void _pinned_toggled(bool p_pressed);
	void _scene_changed();

	void _scene_picker_toggled(bool p_pressed);
	void _update_scene_picker(int p_mode, Control *p_control);
	void _file_system_input(const Ref<InputEvent> &p_event);
	void _scene_tree_input(const Ref<InputEvent> &p_event);
	void _recent_item_selected(int p_idx);
	void _custom_source_input(const Ref<InputEvent> &p_event, Control *p_control);
	Entity2D *_get_scene_from_path(const String &p_path);
	void _set_picked_scene(Entity2D *p_scene);

	void _add_to_recent_scenes(const String &p_scene);
	void _update_recent_scenes();

	bool _is_selected_scene_valid(Entity2D *p_node) const;
	bool _is_scene_painted(Entity2D *p_node) const;
	Entity2D *_get_node_root(Entity *p_node) const;

	void _edit_properties_toggled(bool p_pressed);
	void _edit_properties();

	void _advanced_settings_pressed();
	void _advanced_settings_id_pressed(int p_id);
	void _grid_toggled(bool p_toggled);
	void _paint_mode_changed(int p_mode);
	void _update_paint_mode();
	PaintMode _reload_paint_mode();
	void _grid_step_changed();

protected:
	static void register_members();
	void _notification(int p_what);

public:
	static ScenePaint2DEditor *get_singleton() { return singleton; }

	void forward_canvas_draw_over_viewport(Control *p_overlay);

	void register_scene_provider(Control *p_control, const Callable &p_callback);
	void unregister_scene_provider(Control *p_control);

	void set_painted_scene(Entity2D *p_scene);
	Entity2D *get_painted_scene() const;

	ScenePaint2DEditor();
};

class ScenePaint2DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(ScenePaint2DEditorPlugin, EditorPlugin);

	ScenePaint2DEditor *scene_paint_2d_editor = nullptr;

	mutable bool is_node_2d = false;

	void _canvas_item_tool_changed(int p_tool);

protected:
	void _notification(int p_what);

public:
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;
	virtual void forward_canvas_draw_over_viewport(Control *p_overlay) override;

	ScenePaint2DEditorPlugin();
};
