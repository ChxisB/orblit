/**************************************************************************/
/*  editor_plugin.h                                                       */

#pragma once

#include "core/io/config_file.h"
#include "editor/inspector/editor_context_menu_plugin.h"
#include "scene/spatial/camera_spatial.h"
#include "scene/ui/control.h"
#include "scene/stage/entity.h"

class Entity3D;
class Button;
class PopupMenu;
class EditorDebuggerPlugin;
class EditorDock;
class EditorExport;
class EditorExportPlugin;
class EditorExportPlatform;
class EditorImportPlugin;
class EditorInspectorPlugin;
class EditorInterface;
class EditorEntity3DGizmoPlugin;
class EditorResourceConversionPlugin;
class EditorSceneFormatImporter;
class EditorScenePostImportPlugin;
class EditorToolAddons;
class EditorTranslationParserPlugin;
class EditorUndoRedoManager;
class ScriptCreateDialog;

class EditorPlugin : public Entity {
	ORBLIT_CLASS(EditorPlugin, Entity);
	friend class EditorData;

	bool input_event_forwarding_always_enabled = false;
	bool force_draw_over_forwarding_enabled = false;

	String last_main_screen_name;
	String plugin_version;

public:
	enum CustomControlContainer {
		CONTAINER_TOOLBAR,
		CONTAINER_SPATIAL_EDITOR_MENU,
		CONTAINER_SPATIAL_EDITOR_SIDE_LEFT,
		CONTAINER_SPATIAL_EDITOR_SIDE_RIGHT,
		CONTAINER_SPATIAL_EDITOR_BOTTOM,
		CONTAINER_CANVAS_EDITOR_MENU,
		CONTAINER_CANVAS_EDITOR_SIDE_LEFT,
		CONTAINER_CANVAS_EDITOR_SIDE_RIGHT,
		CONTAINER_CANVAS_EDITOR_BOTTOM,
		CONTAINER_INSPECTOR_BOTTOM,
		CONTAINER_PROJECT_SETTING_TAB_LEFT,
		CONTAINER_PROJECT_SETTING_TAB_RIGHT,
	};

	enum AfterGUIInput {
		AFTER_GUI_INPUT_PASS,
		AFTER_GUI_INPUT_STOP,
		AFTER_GUI_INPUT_CUSTOM,
	};

protected:
	void _notification(int p_what);

	static void register_members();
	EditorUndoRedoManager *get_undo_redo();

	void add_custom_type(const String &p_type, const String &p_base, const Ref<Script> &p_script, const Ref<Texture2D> &p_icon);
	void remove_custom_type(const String &p_type);

	ORBLIT_VIRTUAL1R(bool, _forward_canvas_gui_input, Ref<InputEvent>)
	ORBLIT_VIRTUAL1(_forward_canvas_draw_over_viewport, Control *)
	ORBLIT_VIRTUAL1(_forward_canvas_force_draw_over_viewport, Control *)
	ORBLIT_VIRTUAL2R(int, _forward_3d_gui_input, Camera3D *, Ref<InputEvent>)
	ORBLIT_VIRTUAL1(_forward_3d_draw_over_viewport, Control *)
	ORBLIT_VIRTUAL1(_forward_3d_force_draw_over_viewport, Control *)
	ORBLIT_VIRTUAL0RC(String, _get_plugin_name)
	ORBLIT_VIRTUAL1(_make_visible, bool)
	ORBLIT_VIRTUAL1(_edit, Object *)
	ORBLIT_VIRTUAL1RC(bool, _handles, Object *)
	ORBLIT_VIRTUAL0RC(Dictionary, _get_state)
	ORBLIT_VIRTUAL1(_set_state, Dictionary)
	ORBLIT_VIRTUAL0(_clear)
	ORBLIT_VIRTUAL1RC(String, _get_unsaved_status, String)
	ORBLIT_VIRTUAL0(_save_external_data)
	ORBLIT_VIRTUAL0(_apply_changes)
	ORBLIT_VIRTUAL0RC(Vector<String>, _get_breakpoints)
	ORBLIT_VIRTUAL1(_set_window_layout, Ref<ConfigFile>)
	ORBLIT_VIRTUAL1(_get_window_layout, Ref<ConfigFile>)
	ORBLIT_VIRTUAL0R(bool, _build)
	ORBLIT_VIRTUAL2RC(Vector<String>, _run_scene, String, Vector<String>)
	ORBLIT_VIRTUAL0(_enable_plugin)
	ORBLIT_VIRTUAL0(_disable_plugin)

public:
	//TODO: send a resource for editing to the editor node?

	void add_control_to_container(CustomControlContainer p_location, Control *p_control);
	void remove_control_from_container(CustomControlContainer p_location, Control *p_control);

	void add_dock(EditorDock *p_dock);
	void remove_dock(EditorDock *p_dock);

	void add_tool_menu_item(const String &p_name, const Callable &p_callable);
	void add_tool_submenu_item(const String &p_name, PopupMenu *p_submenu);
	void remove_tool_menu_item(const String &p_name);

	PopupMenu *get_export_as_menu();

	void set_input_event_forwarding_always_enabled();
	bool is_input_event_forwarding_always_enabled() { return input_event_forwarding_always_enabled; }

	void set_force_draw_over_forwarding_enabled();
	bool is_force_draw_over_forwarding_enabled() { return force_draw_over_forwarding_enabled; }

	void notify_main_screen_changed(const String &screen_name);
	void notify_scene_changed(const Entity *scn_root);
	void notify_scene_closed(const String &scene_filepath);
	void notify_resource_saved(const Ref<Resource> &p_resource);
	void notify_scene_saved(const String &p_scene_filepath);

	virtual bool forward_canvas_gui_input(const Ref<InputEvent> &p_event);
	virtual void forward_canvas_draw_over_viewport(Control *p_overlay);
	virtual void forward_canvas_force_draw_over_viewport(Control *p_overlay);

	virtual EditorPlugin::AfterGUIInput forward_3d_gui_input(Camera3D *p_camera, const Ref<InputEvent> &p_event);
	virtual void forward_3d_draw_over_viewport(Control *p_overlay);
	virtual void forward_3d_force_draw_over_viewport(Control *p_overlay);

	virtual String get_plugin_name() const;
	virtual String get_plugin_version() const;
	virtual void set_plugin_version(const String &p_version);
	virtual void make_visible(bool p_visible);
	virtual void set_current() {}
	virtual void edit(Object *p_object);
	virtual bool handles(Object *p_object) const;
	virtual bool can_auto_hide() const;
	virtual Dictionary get_state() const; //save editor state so it can't be reloaded when reloading scene
	virtual void set_state(const Dictionary &p_state); //restore editor state (likely was saved with the scene)
	virtual void clear(); // clear any temporary data in the editor, reset it (likely new scene or load another scene)
	virtual String get_unsaved_status(const String &p_for_scene = "") const;
	virtual void save_external_data(); // if editor references external resources/scenes, save them
	virtual void apply_changes(); // if changes are pending in editor, apply them
	virtual void get_breakpoints(List<String> *p_breakpoints);
	virtual bool get_remove_list(List<Entity *> *p_list);
	virtual void set_window_layout(Ref<ConfigFile> p_layout);
	virtual void get_window_layout(Ref<ConfigFile> p_layout);
	virtual void edited_scene_changed() {} // if changes are pending in editor, apply them
	virtual bool build(); // builds with external tools. Returns true if safe to continue running scene.
	virtual void run_scene(const String &p_scene, Vector<String> &r_args);

	ScriptCreateDialog *get_script_create_dialog();

	void add_undo_redo_inspector_hook_callback(Callable p_callable);
	void remove_undo_redo_inspector_hook_callback(Callable p_callable);

	int update_overlays() const;

	void queue_save_layout();

	void make_bottom_panel_item_visible(Control *p_item);
	void hide_bottom_panel();

	void add_translation_parser_plugin(const Ref<EditorTranslationParserPlugin> &p_parser);
	void remove_translation_parser_plugin(const Ref<EditorTranslationParserPlugin> &p_parser);

	void add_import_plugin(const Ref<EditorImportPlugin> &p_importer, bool p_first_priority = false);
	void remove_import_plugin(const Ref<EditorImportPlugin> &p_importer);

	void add_export_plugin(const Ref<EditorExportPlugin> &p_exporter);
	void remove_export_plugin(const Ref<EditorExportPlugin> &p_exporter);

	void add_export_platform(const Ref<EditorExportPlatform> &p_platform);
	void remove_export_platform(const Ref<EditorExportPlatform> &p_platform);

	void add_entity_3d_gizmo_plugin(const Ref<EditorEntity3DGizmoPlugin> &p_gizmo_plugin);
	void remove_entity_3d_gizmo_plugin(const Ref<EditorEntity3DGizmoPlugin> &p_gizmo_plugin);

	void add_scene_format_importer_plugin(const Ref<EditorSceneFormatImporter> &p_importer, bool p_first_priority = false);
	void remove_scene_format_importer_plugin(const Ref<EditorSceneFormatImporter> &p_importer);

	void add_scene_post_import_plugin(const Ref<EditorScenePostImportPlugin> &p_importer, bool p_first_priority = false);
	void remove_scene_post_import_plugin(const Ref<EditorScenePostImportPlugin> &p_importer);

	void add_inspector_plugin(const Ref<EditorInspectorPlugin> &p_plugin);
	void remove_inspector_plugin(const Ref<EditorInspectorPlugin> &p_plugin);

	void add_autoload_singleton(const String &p_name, const String &p_path);
	void remove_autoload_singleton(const String &p_name);

	void add_debugger_plugin(const Ref<EditorDebuggerPlugin> &p_plugin);
	void remove_debugger_plugin(const Ref<EditorDebuggerPlugin> &p_plugin);

	void add_resource_conversion_plugin(const Ref<EditorResourceConversionPlugin> &p_plugin);
	void remove_resource_conversion_plugin(const Ref<EditorResourceConversionPlugin> &p_plugin);

	void add_context_menu_plugin(EditorContextMenuPlugin::ContextMenuSlot p_slot, const Ref<EditorContextMenuPlugin> &p_plugin);
	void remove_context_menu_plugin(const Ref<EditorContextMenuPlugin> &p_plugin);

	void enable_plugin();
	void disable_plugin();
};

VARIANT_ENUM_CAST(EditorPlugin::CustomControlContainer);
VARIANT_ENUM_CAST(EditorPlugin::AfterGUIInput);

typedef EditorPlugin *(*EditorPluginCreateFunc)();

class EditorPlugins {
	enum {
		MAX_CREATE_FUNCS = 128
	};

	static EditorPluginCreateFunc creation_funcs[MAX_CREATE_FUNCS];
	static int creation_func_count;

	template <typename T>
	static EditorPlugin *creator() {
		return memnew(T);
	}

public:
	static int get_plugin_count() { return creation_func_count; }
	static EditorPlugin *create(int p_idx) {
		ERR_FAIL_INDEX_V(p_idx, creation_func_count, nullptr);
		return creation_funcs[p_idx]();
	}

	template <typename T>
	static void add_by_type() {
		add_create_func(creator<T>);
	}

	static void add_create_func(EditorPluginCreateFunc p_func) {
		ERR_FAIL_COND(creation_func_count >= MAX_CREATE_FUNCS);
		creation_funcs[creation_func_count++] = p_func;
	}
};
