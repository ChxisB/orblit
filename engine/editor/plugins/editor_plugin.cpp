/**************************************************************************/
/*  editor_plugin.cpp                                                     */

#include "editor_plugin.h"

#include "core/io/resource_importer.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "editor/debugger/editor_debugger_node.h"
#include "editor/debugger/editor_debugger_plugin.h"
#include "editor/docks/editor_dock.h"
#include "editor/docks/editor_dock_manager.h"
#include "editor/docks/inspector_dock.h"
#include "editor/docks/stage_dock.h"
#include "editor/editor_interface.h"
#include "editor/editor_main_screen.h"
#include "editor/editor_node.h"
#include "editor/editor_undo_redo_manager.h"
#include "editor/export/editor_export.h"
#include "editor/export/editor_export_platform.h"
#include "editor/file_system/editor_file_system.h"
#include "editor/import/editor_import_plugin.h"
#include "editor/import/spatial/resource_importer_scene.h"
#include "editor/inspector/editor_inspector.h"
#include "editor/plugins/editor_plugin_list.h"
#include "editor/plugins/editor_resource_conversion_plugin.h"
#include "editor/scene/canvas_item_editor_plugin.h"
#include "editor/scene/spatial/entity_spatial_editor_plugin.h"
#include "editor/scene/spatial/entity_spatial_editor_viewport.h"
#include "editor/script/script_editor_plugin.h"
#include "editor/settings/project_settings_editor.h"
#include "editor/translations/editor_translation_parser.h"
#include "editor/widgets/editor_bottom_panel.h"
#include "editor/widgets/editor_title_bar.h"
#include "scene/spatial/camera_spatial.h"
#include "scene/ui/popup_menu.h"

void EditorPlugin::add_custom_type(const String &p_type, const String &p_base, const Ref<Script> &p_script, const Ref<Texture2D> &p_icon) {
	EditorNode::get_editor_data().add_custom_type(p_type, p_base, p_script, p_icon);
}

void EditorPlugin::remove_custom_type(const String &p_type) {
	EditorNode::get_editor_data().remove_custom_type(p_type);
}

void EditorPlugin::add_autoload_singleton(const String &p_name, const String &p_path) {
	if (p_path.begins_with("res://")) {
		EditorNode::get_singleton()->get_project_settings()->get_autoload_settings()->autoload_add(p_name, p_path, false);
	} else {
		const Ref<Script> plugin_script = static_cast<Ref<Script>>(get_script());
		ERR_FAIL_COND(plugin_script.is_null());
		const String script_base_path = plugin_script->get_path().get_base_dir();
		EditorNode::get_singleton()->get_project_settings()->get_autoload_settings()->autoload_add(p_name, script_base_path.path_join(p_path), false);
	}
}

void EditorPlugin::remove_autoload_singleton(const String &p_name) {
	EditorNode::get_singleton()->get_project_settings()->get_autoload_settings()->autoload_remove(p_name);
}

void EditorPlugin::add_dock(EditorDock *p_dock) {
	EditorDockManager::get_singleton()->add_dock(p_dock);
}

void EditorPlugin::remove_dock(EditorDock *p_dock) {
	EditorDockManager::get_singleton()->remove_dock(p_dock);
}

void EditorPlugin::add_control_to_container(CustomControlContainer p_location, Control *p_control) {
	ERR_FAIL_NULL(p_control);

	switch (p_location) {
		case CONTAINER_TOOLBAR: {
			EditorNode::get_title_bar()->add_child(p_control);
		} break;

		case CONTAINER_SPATIAL_EDITOR_MENU: {
			Entity3DEditor::get_singleton()->add_control_to_menu_panel(p_control);

		} break;
		case CONTAINER_SPATIAL_EDITOR_SIDE_LEFT: {
			Entity3DEditor::get_singleton()->add_control_to_left_panel(p_control);
		} break;
		case CONTAINER_SPATIAL_EDITOR_SIDE_RIGHT: {
			Entity3DEditor::get_singleton()->add_control_to_right_panel(p_control);
		} break;
		case CONTAINER_SPATIAL_EDITOR_BOTTOM: {
			Entity3DEditor::get_singleton()->get_shader_split()->add_child(p_control);

		} break;
		case CONTAINER_CANVAS_EDITOR_MENU: {
			CanvasItemEditor::get_singleton()->add_control_to_menu_panel(p_control);

		} break;
		case CONTAINER_CANVAS_EDITOR_SIDE_LEFT: {
			CanvasItemEditor::get_singleton()->add_control_to_left_panel(p_control);
		} break;
		case CONTAINER_CANVAS_EDITOR_SIDE_RIGHT: {
			CanvasItemEditor::get_singleton()->add_control_to_right_panel(p_control);
		} break;
		case CONTAINER_CANVAS_EDITOR_BOTTOM: {
			CanvasItemEditor::get_singleton()->get_bottom_split()->add_child(p_control);

		} break;
		case CONTAINER_INSPECTOR_BOTTOM: {
			InspectorDock::get_singleton()->get_addon_area()->add_child(p_control);

		} break;
		case CONTAINER_PROJECT_SETTING_TAB_LEFT: {
			ProjectSettingsEditor::get_singleton()->get_tabs()->add_child(p_control);
			ProjectSettingsEditor::get_singleton()->get_tabs()->move_child(p_control, 0);

		} break;
		case CONTAINER_PROJECT_SETTING_TAB_RIGHT: {
			ProjectSettingsEditor::get_singleton()->get_tabs()->add_child(p_control);

		} break;
	}
}

void EditorPlugin::remove_control_from_container(CustomControlContainer p_location, Control *p_control) {
	ERR_FAIL_NULL(p_control);

	switch (p_location) {
		case CONTAINER_TOOLBAR: {
			EditorNode::get_title_bar()->remove_child(p_control);
		} break;

		case CONTAINER_SPATIAL_EDITOR_MENU: {
			Entity3DEditor::get_singleton()->remove_control_from_menu_panel(p_control);

		} break;
		case CONTAINER_SPATIAL_EDITOR_SIDE_LEFT: {
			Entity3DEditor::get_singleton()->remove_control_from_left_panel(p_control);
		} break;
		case CONTAINER_SPATIAL_EDITOR_SIDE_RIGHT: {
			Entity3DEditor::get_singleton()->remove_control_from_right_panel(p_control);
		} break;
		case CONTAINER_SPATIAL_EDITOR_BOTTOM: {
			Entity3DEditor::get_singleton()->get_shader_split()->remove_child(p_control);

		} break;
		case CONTAINER_CANVAS_EDITOR_MENU: {
			CanvasItemEditor::get_singleton()->remove_control_from_menu_panel(p_control);

		} break;
		case CONTAINER_CANVAS_EDITOR_SIDE_LEFT: {
			CanvasItemEditor::get_singleton()->remove_control_from_left_panel(p_control);
		} break;
		case CONTAINER_CANVAS_EDITOR_SIDE_RIGHT: {
			CanvasItemEditor::get_singleton()->remove_control_from_right_panel(p_control);
		} break;
		case CONTAINER_CANVAS_EDITOR_BOTTOM: {
			CanvasItemEditor::get_singleton()->get_bottom_split()->remove_child(p_control);

		} break;
		case CONTAINER_INSPECTOR_BOTTOM: {
			InspectorDock::get_singleton()->get_addon_area()->remove_child(p_control);

		} break;
		case CONTAINER_PROJECT_SETTING_TAB_LEFT:
		case CONTAINER_PROJECT_SETTING_TAB_RIGHT: {
			ProjectSettingsEditor::get_singleton()->get_tabs()->remove_child(p_control);

		} break;
	}
}

void EditorPlugin::add_tool_menu_item(const String &p_name, const Callable &p_callable) {
	EditorNode::get_singleton()->add_tool_menu_item(p_name, p_callable);
}

void EditorPlugin::add_tool_submenu_item(const String &p_name, PopupMenu *p_submenu) {
	ERR_FAIL_NULL(p_submenu);
	EditorNode::get_singleton()->add_tool_submenu_item(p_name, p_submenu);
}

void EditorPlugin::remove_tool_menu_item(const String &p_name) {
	EditorNode::get_singleton()->remove_tool_menu_item(p_name);
}

PopupMenu *EditorPlugin::get_export_as_menu() {
	return EditorNode::get_singleton()->get_export_as_menu();
}

void EditorPlugin::set_input_event_forwarding_always_enabled() {
	input_event_forwarding_always_enabled = true;
	EditorNode::get_singleton()->get_editor_plugins_force_input_forwarding()->add_plugin(this);
}

void EditorPlugin::set_force_draw_over_forwarding_enabled() {
	force_draw_over_forwarding_enabled = true;
	EditorNode::get_singleton()->get_editor_plugins_force_over()->add_plugin(this);
}

void EditorPlugin::notify_scene_changed(const Entity *scn_root) {
	emit_signal(SNAME("scene_changed"), scn_root);
}

void EditorPlugin::notify_main_screen_changed(const String &screen_name) {
	if (screen_name == last_main_screen_name) {
		return;
	}

	emit_signal(SNAME("main_screen_changed"), screen_name);
	last_main_screen_name = screen_name;
}

void EditorPlugin::notify_scene_closed(const String &scene_filepath) {
	emit_signal(SNAME("scene_closed"), scene_filepath);
}

void EditorPlugin::notify_resource_saved(const Ref<Resource> &p_resource) {
	emit_signal(SNAME("resource_saved"), p_resource);
}

void EditorPlugin::notify_scene_saved(const String &p_scene_filepath) {
	emit_signal(SNAME("scene_saved"), p_scene_filepath);
}

bool EditorPlugin::forward_canvas_gui_input(const Ref<InputEvent> &p_event) {
	bool success = false;
	ORBLIT_VIRTUAL_CALL(_forward_canvas_gui_input, p_event, success);
	return success;
}

void EditorPlugin::forward_canvas_draw_over_viewport(Control *p_overlay) {
	ORBLIT_VIRTUAL_CALL(_forward_canvas_draw_over_viewport, p_overlay);
}

void EditorPlugin::forward_canvas_force_draw_over_viewport(Control *p_overlay) {
	ORBLIT_VIRTUAL_CALL(_forward_canvas_force_draw_over_viewport, p_overlay);
}

// Updates the overlays of the 2D viewport or, if in 3D mode, of every 3D viewport.
int EditorPlugin::update_overlays() const {
	if (Entity3DEditor::get_singleton()->is_visible()) {
		int count = 0;
		for (uint32_t i = 0; i < Entity3DEditor::VIEWPORTS_COUNT; i++) {
			Entity3DEditorViewport *vp = Entity3DEditor::get_singleton()->get_editor_viewport(i);
			if (vp->is_visible()) {
				vp->update_surface();
				count++;
			}
		}
		return count;
	} else {
		// This will update the normal viewport itself as well
		CanvasItemEditor::get_singleton()->get_viewport_control()->queue_redraw();
		return 1;
	}
}

EditorPlugin::AfterGUIInput EditorPlugin::forward_3d_gui_input(Camera3D *p_camera, const Ref<InputEvent> &p_event) {
	int success = EditorPlugin::AFTER_GUI_INPUT_PASS;
	ORBLIT_VIRTUAL_CALL(_forward_3d_gui_input, p_camera, p_event, success);
	return static_cast<EditorPlugin::AfterGUIInput>(success);
}

void EditorPlugin::forward_3d_draw_over_viewport(Control *p_overlay) {
	ORBLIT_VIRTUAL_CALL(_forward_3d_draw_over_viewport, p_overlay);
}

void EditorPlugin::forward_3d_force_draw_over_viewport(Control *p_overlay) {
	ORBLIT_VIRTUAL_CALL(_forward_3d_force_draw_over_viewport, p_overlay);
}

String EditorPlugin::get_plugin_name() const {
	String name;
	ORBLIT_VIRTUAL_CALL(_get_plugin_name, name);
	return name;
}

String EditorPlugin::get_plugin_version() const {
	return plugin_version;
}

void EditorPlugin::set_plugin_version(const String &p_version) {
	plugin_version = p_version;
}

void EditorPlugin::make_visible(bool p_visible) {
	ORBLIT_VIRTUAL_CALL(_make_visible, p_visible);
}

void EditorPlugin::edit(Object *p_object) {
	ORBLIT_VIRTUAL_CALL(_edit, p_object);
}

bool EditorPlugin::handles(Object *p_object) const {
	bool success = false;
	ORBLIT_VIRTUAL_CALL(_handles, p_object, success);
	return success;
}

bool EditorPlugin::can_auto_hide() const {
	return true;
}

Dictionary EditorPlugin::get_state() const {
	Dictionary state;
	ORBLIT_VIRTUAL_CALL(_get_state, state);
	return state;
}

void EditorPlugin::set_state(const Dictionary &p_state) {
	ORBLIT_VIRTUAL_CALL(_set_state, p_state);
}

void EditorPlugin::clear() {
	ORBLIT_VIRTUAL_CALL(_clear);
}

String EditorPlugin::get_unsaved_status(const String &p_for_scene) const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_unsaved_status, p_for_scene, ret);
	return ret;
}

void EditorPlugin::save_external_data() {
	ORBLIT_VIRTUAL_CALL(_save_external_data);
}

// if changes are pending in editor, apply them
void EditorPlugin::apply_changes() {
	ORBLIT_VIRTUAL_CALL(_apply_changes);
}

void EditorPlugin::get_breakpoints(List<String> *p_breakpoints) {
	PackedStringArray arr;
	if (ORBLIT_VIRTUAL_CALL(_get_breakpoints, arr)) {
		for (int i = 0; i < arr.size(); i++) {
			p_breakpoints->push_back(arr[i]);
		}
	}
}

bool EditorPlugin::get_remove_list(List<Entity *> *p_list) {
	return false;
}

void EditorPlugin::add_undo_redo_inspector_hook_callback(Callable p_callable) {
	EditorNode::get_editor_data().add_undo_redo_inspector_hook_callback(p_callable);
}

void EditorPlugin::remove_undo_redo_inspector_hook_callback(Callable p_callable) {
	EditorNode::get_editor_data().remove_undo_redo_inspector_hook_callback(p_callable);
}

void EditorPlugin::add_translation_parser_plugin(const Ref<EditorTranslationParserPlugin> &p_parser) {
	ERR_FAIL_COND(p_parser.is_null());
	EditorTranslationParser::get_singleton()->add_parser(p_parser, EditorTranslationParser::CUSTOM);
}

void EditorPlugin::remove_translation_parser_plugin(const Ref<EditorTranslationParserPlugin> &p_parser) {
	ERR_FAIL_COND(p_parser.is_null());
	EditorTranslationParser::get_singleton()->remove_parser(p_parser, EditorTranslationParser::CUSTOM);
}

void EditorPlugin::add_import_plugin(const Ref<EditorImportPlugin> &p_importer, bool p_first_priority) {
	ERR_FAIL_COND(p_importer.is_null());
	ResourceFormatImporter::get_singleton()->add_importer(p_importer, p_first_priority);
	// Plugins are now loaded during the first scan. It's important not to start another scan,
	// even a deferred one, as it would cause a scan during a scan at the next main thread iteration.
	if (!EditorFileSystem::get_singleton()->doing_first_scan()) {
		callable_mp(EditorFileSystem::get_singleton(), &EditorFileSystem::scan).call_deferred();
	}
}

void EditorPlugin::remove_import_plugin(const Ref<EditorImportPlugin> &p_importer) {
	ERR_FAIL_COND(p_importer.is_null());
	ResourceFormatImporter::get_singleton()->remove_importer(p_importer);
	// Plugins are now loaded during the first scan. It's important not to start another scan,
	// even a deferred one, as it would cause a scan during a scan at the next main thread iteration.
	if (!EditorNode::get_singleton()->is_exiting() && !EditorFileSystem::get_singleton()->doing_first_scan()) {
		callable_mp(EditorFileSystem::get_singleton(), &EditorFileSystem::scan).call_deferred();
	}
}

void EditorPlugin::add_export_plugin(const Ref<EditorExportPlugin> &p_exporter) {
	ERR_FAIL_COND(p_exporter.is_null());
	EditorExport::get_singleton()->add_export_plugin(p_exporter);
}

void EditorPlugin::remove_export_plugin(const Ref<EditorExportPlugin> &p_exporter) {
	ERR_FAIL_COND(p_exporter.is_null());
	EditorExport::get_singleton()->remove_export_plugin(p_exporter);
}

void EditorPlugin::add_export_platform(const Ref<EditorExportPlatform> &p_platform) {
	ERR_FAIL_COND(p_platform.is_null());
	EditorExport::get_singleton()->add_export_platform(p_platform);
}

void EditorPlugin::remove_export_platform(const Ref<EditorExportPlatform> &p_platform) {
	ERR_FAIL_COND(p_platform.is_null());
	EditorExport::get_singleton()->remove_export_platform(p_platform);
}

void EditorPlugin::add_entity_3d_gizmo_plugin(const Ref<EditorEntity3DGizmoPlugin> &p_gizmo_plugin) {
	ERR_FAIL_COND(p_gizmo_plugin.is_null());
	Entity3DEditor::get_singleton()->add_gizmo_plugin(p_gizmo_plugin);
}

void EditorPlugin::remove_entity_3d_gizmo_plugin(const Ref<EditorEntity3DGizmoPlugin> &p_gizmo_plugin) {
	ERR_FAIL_COND(p_gizmo_plugin.is_null());
	Entity3DEditor::get_singleton()->remove_gizmo_plugin(p_gizmo_plugin);
}

void EditorPlugin::add_scene_format_importer_plugin(const Ref<EditorSceneFormatImporter> &p_importer, bool p_first_priority) {
	ERR_FAIL_COND(p_importer.is_null());
	ResourceImporterScene::add_scene_importer(p_importer, p_first_priority);
}

void EditorPlugin::remove_scene_format_importer_plugin(const Ref<EditorSceneFormatImporter> &p_importer) {
	ERR_FAIL_COND(p_importer.is_null());
	ResourceImporterScene::remove_scene_importer(p_importer);
}

void EditorPlugin::add_scene_post_import_plugin(const Ref<EditorScenePostImportPlugin> &p_plugin, bool p_first_priority) {
	ResourceImporterScene::add_post_importer_plugin(p_plugin, p_first_priority);
}

void EditorPlugin::remove_scene_post_import_plugin(const Ref<EditorScenePostImportPlugin> &p_plugin) {
	ResourceImporterScene::remove_post_importer_plugin(p_plugin);
}

void EditorPlugin::add_inspector_plugin(const Ref<EditorInspectorPlugin> &p_plugin) {
	ERR_FAIL_COND(p_plugin.is_null());
	EditorInspector::add_inspector_plugin(p_plugin);
}

void EditorPlugin::remove_inspector_plugin(const Ref<EditorInspectorPlugin> &p_plugin) {
	ERR_FAIL_COND(p_plugin.is_null());
	EditorInspector::remove_inspector_plugin(p_plugin);
}

void EditorPlugin::add_context_menu_plugin(EditorContextMenuPlugin::ContextMenuSlot p_slot, const Ref<EditorContextMenuPlugin> &p_plugin) {
	EditorContextMenuPluginManager::get_singleton()->add_plugin(p_slot, p_plugin);
}

void EditorPlugin::remove_context_menu_plugin(const Ref<EditorContextMenuPlugin> &p_plugin) {
	EditorContextMenuPluginManager::get_singleton()->remove_plugin(p_plugin);
}

int find(const PackedStringArray &a, const String &v) {
	const String *r = a.ptr();
	for (int j = 0; j < a.size(); ++j) {
		if (r[j] == v) {
			return j;
		}
	}
	return -1;
}

void EditorPlugin::enable_plugin() {
	// Called when the plugin gets enabled in project settings, after it's added to the tree.
	// You can implement it to register autoloads.
	ORBLIT_VIRTUAL_CALL(_enable_plugin);
}

void EditorPlugin::disable_plugin() {
	// Last function called when the plugin gets disabled in project settings.
	// Implement it to cleanup things from the project, such as unregister autoloads.
	ORBLIT_VIRTUAL_CALL(_disable_plugin);
}

void EditorPlugin::set_window_layout(Ref<ConfigFile> p_layout) {
	ORBLIT_VIRTUAL_CALL(_set_window_layout, p_layout);
}

void EditorPlugin::get_window_layout(Ref<ConfigFile> p_layout) {
	ORBLIT_VIRTUAL_CALL(_get_window_layout, p_layout);
}

bool EditorPlugin::build() {
	bool success = true;
	ORBLIT_VIRTUAL_CALL(_build, success);
	return success;
}

void EditorPlugin::run_scene(const String &p_scene, Vector<String> &r_args) {
	Vector<String> new_args;
	if (ORBLIT_VIRTUAL_CALL(_run_scene, p_scene, r_args, new_args)) {
		r_args = new_args;
	}
}

void EditorPlugin::queue_save_layout() {
	EditorNode::get_singleton()->save_editor_layout_delayed();
}

void EditorPlugin::make_bottom_panel_item_visible(Control *p_item) {
	EditorNode::get_bottom_panel()->make_item_visible(p_item);
}

void EditorPlugin::hide_bottom_panel() {
	EditorNode::get_bottom_panel()->hide_bottom_panel();
}

ScriptCreateDialog *EditorPlugin::get_script_create_dialog() {
	return StageDock::get_singleton()->get_script_create_dialog();
}

void EditorPlugin::add_debugger_plugin(const Ref<EditorDebuggerPlugin> &p_plugin) {
	EditorDebuggerNode::get_singleton()->add_debugger_plugin(p_plugin);
}

void EditorPlugin::remove_debugger_plugin(const Ref<EditorDebuggerPlugin> &p_plugin) {
	EditorDebuggerNode::get_singleton()->remove_debugger_plugin(p_plugin);
}

void EditorPlugin::add_resource_conversion_plugin(const Ref<EditorResourceConversionPlugin> &p_plugin) {
	EditorNode::get_singleton()->add_resource_conversion_plugin(p_plugin);
}

void EditorPlugin::remove_resource_conversion_plugin(const Ref<EditorResourceConversionPlugin> &p_plugin) {
	EditorNode::get_singleton()->remove_resource_conversion_plugin(p_plugin);
}

void EditorPlugin::_notification(int p_what) {
}

void EditorPlugin::register_members() {
	ClassRegistry::bind_method(D_METHOD("add_dock", "dock"), &EditorPlugin::add_dock);
	ClassRegistry::bind_method(D_METHOD("remove_dock", "dock"), &EditorPlugin::remove_dock);
	ClassRegistry::bind_method(D_METHOD("add_control_to_container", "container", "control"), &EditorPlugin::add_control_to_container);
	ClassRegistry::bind_method(D_METHOD("remove_control_from_container", "container", "control"), &EditorPlugin::remove_control_from_container);
	ClassRegistry::bind_method(D_METHOD("add_tool_menu_item", "name", "callable"), &EditorPlugin::add_tool_menu_item);
	ClassRegistry::bind_method(D_METHOD("add_tool_submenu_item", "name", "submenu"), &EditorPlugin::add_tool_submenu_item);
	ClassRegistry::bind_method(D_METHOD("remove_tool_menu_item", "name"), &EditorPlugin::remove_tool_menu_item);
	ClassRegistry::bind_method(D_METHOD("get_export_as_menu"), &EditorPlugin::get_export_as_menu);
	ClassRegistry::bind_method(D_METHOD("add_custom_type", "type", "base", "script", "icon"), &EditorPlugin::add_custom_type);
	ClassRegistry::bind_method(D_METHOD("remove_custom_type", "type"), &EditorPlugin::remove_custom_type);

	ClassRegistry::bind_method(D_METHOD("add_autoload_singleton", "name", "path"), &EditorPlugin::add_autoload_singleton);
	ClassRegistry::bind_method(D_METHOD("remove_autoload_singleton", "name"), &EditorPlugin::remove_autoload_singleton);

	ClassRegistry::bind_method(D_METHOD("update_overlays"), &EditorPlugin::update_overlays);

	ClassRegistry::bind_method(D_METHOD("make_bottom_panel_item_visible", "item"), &EditorPlugin::make_bottom_panel_item_visible);
	ClassRegistry::bind_method(D_METHOD("hide_bottom_panel"), &EditorPlugin::hide_bottom_panel);

	ClassRegistry::bind_method(D_METHOD("get_undo_redo"), &EditorPlugin::get_undo_redo);
	ClassRegistry::bind_method(D_METHOD("add_undo_redo_inspector_hook_callback", "callable"), &EditorPlugin::add_undo_redo_inspector_hook_callback);
	ClassRegistry::bind_method(D_METHOD("remove_undo_redo_inspector_hook_callback", "callable"), &EditorPlugin::remove_undo_redo_inspector_hook_callback);
	ClassRegistry::bind_method(D_METHOD("queue_save_layout"), &EditorPlugin::queue_save_layout);
	ClassRegistry::bind_method(D_METHOD("add_translation_parser_plugin", "parser"), &EditorPlugin::add_translation_parser_plugin);
	ClassRegistry::bind_method(D_METHOD("remove_translation_parser_plugin", "parser"), &EditorPlugin::remove_translation_parser_plugin);
	ClassRegistry::bind_method(D_METHOD("add_import_plugin", "importer", "first_priority"), &EditorPlugin::add_import_plugin, DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("remove_import_plugin", "importer"), &EditorPlugin::remove_import_plugin);
	ClassRegistry::bind_method(D_METHOD("add_export_plugin", "plugin"), &EditorPlugin::add_export_plugin);
	ClassRegistry::bind_method(D_METHOD("remove_export_plugin", "plugin"), &EditorPlugin::remove_export_plugin);
	ClassRegistry::bind_method(D_METHOD("add_export_platform", "platform"), &EditorPlugin::add_export_platform);
	ClassRegistry::bind_method(D_METHOD("remove_export_platform", "platform"), &EditorPlugin::remove_export_platform);
	ClassRegistry::bind_method(D_METHOD("add_inspector_plugin", "plugin"), &EditorPlugin::add_inspector_plugin);
	ClassRegistry::bind_method(D_METHOD("remove_inspector_plugin", "plugin"), &EditorPlugin::remove_inspector_plugin);
	ClassRegistry::bind_method(D_METHOD("add_resource_conversion_plugin", "plugin"), &EditorPlugin::add_resource_conversion_plugin);
	ClassRegistry::bind_method(D_METHOD("remove_resource_conversion_plugin", "plugin"), &EditorPlugin::remove_resource_conversion_plugin);
	ClassRegistry::bind_method(D_METHOD("set_input_event_forwarding_always_enabled"), &EditorPlugin::set_input_event_forwarding_always_enabled);
	ClassRegistry::bind_method(D_METHOD("set_force_draw_over_forwarding_enabled"), &EditorPlugin::set_force_draw_over_forwarding_enabled);
	ClassRegistry::bind_method(D_METHOD("add_context_menu_plugin", "slot", "plugin"), &EditorPlugin::add_context_menu_plugin);
	ClassRegistry::bind_method(D_METHOD("remove_context_menu_plugin", "plugin"), &EditorPlugin::remove_context_menu_plugin);

	ClassRegistry::bind_method(D_METHOD("add_entity_3d_gizmo_plugin", "plugin"), &EditorPlugin::add_entity_3d_gizmo_plugin);
	ClassRegistry::bind_method(D_METHOD("remove_entity_3d_gizmo_plugin", "plugin"), &EditorPlugin::remove_entity_3d_gizmo_plugin);
	ClassRegistry::bind_method(D_METHOD("add_scene_format_importer_plugin", "scene_format_importer", "first_priority"), &EditorPlugin::add_scene_format_importer_plugin, DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("remove_scene_format_importer_plugin", "scene_format_importer"), &EditorPlugin::remove_scene_format_importer_plugin);
	ClassRegistry::bind_method(D_METHOD("add_scene_post_import_plugin", "scene_import_plugin", "first_priority"), &EditorPlugin::add_scene_post_import_plugin, DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("remove_scene_post_import_plugin", "scene_import_plugin"), &EditorPlugin::remove_scene_post_import_plugin);

	ClassRegistry::bind_method(D_METHOD("get_script_create_dialog"), &EditorPlugin::get_script_create_dialog);
	ClassRegistry::bind_method(D_METHOD("add_debugger_plugin", "script"), &EditorPlugin::add_debugger_plugin);
	ClassRegistry::bind_method(D_METHOD("remove_debugger_plugin", "script"), &EditorPlugin::remove_debugger_plugin);
	ClassRegistry::bind_method(D_METHOD("get_plugin_version"), &EditorPlugin::get_plugin_version);

	ORBLIT_VIRTUAL_BIND(_forward_canvas_gui_input, "event");
	ORBLIT_VIRTUAL_BIND(_forward_canvas_draw_over_viewport, "viewport_control");
	ORBLIT_VIRTUAL_BIND(_forward_canvas_force_draw_over_viewport, "viewport_control");
	ORBLIT_VIRTUAL_BIND(_forward_3d_gui_input, "viewport_camera", "event");
	ORBLIT_VIRTUAL_BIND(_forward_3d_draw_over_viewport, "viewport_control");
	ORBLIT_VIRTUAL_BIND(_forward_3d_force_draw_over_viewport, "viewport_control");
	ORBLIT_VIRTUAL_BIND(_get_plugin_name);
	ORBLIT_VIRTUAL_BIND(_make_visible, "visible");
	ORBLIT_VIRTUAL_BIND(_edit, "object");
	ORBLIT_VIRTUAL_BIND(_handles, "object");
	ORBLIT_VIRTUAL_BIND(_get_state);
	ORBLIT_VIRTUAL_BIND(_set_state, "state");
	ORBLIT_VIRTUAL_BIND(_clear);
	ORBLIT_VIRTUAL_BIND(_get_unsaved_status, "for_scene");
	ORBLIT_VIRTUAL_BIND(_save_external_data);
	ORBLIT_VIRTUAL_BIND(_apply_changes);
	ORBLIT_VIRTUAL_BIND(_get_breakpoints);
	ORBLIT_VIRTUAL_BIND(_set_window_layout, "configuration");
	ORBLIT_VIRTUAL_BIND(_get_window_layout, "configuration");
	ORBLIT_VIRTUAL_BIND(_build);
	ORBLIT_VIRTUAL_BIND(_run_scene, "scene", "args");
	ORBLIT_VIRTUAL_BIND(_enable_plugin);
	ORBLIT_VIRTUAL_BIND(_disable_plugin);

	ADD_SIGNAL(MethodInfo("scene_changed", PropertyInfo(Variant::OBJECT, "scene_root", PROPERTY_HINT_RESOURCE_TYPE, Entity::get_static_class_name())));
	ADD_SIGNAL(MethodInfo("scene_closed", PropertyInfo(Variant::STRING, "filepath")));
	ADD_SIGNAL(MethodInfo("main_screen_changed", PropertyInfo(Variant::STRING, "screen_name")));
	ADD_SIGNAL(MethodInfo("resource_saved", PropertyInfo(Variant::OBJECT, "resource", PROPERTY_HINT_RESOURCE_TYPE, Resource::get_static_class_name())));
	ADD_SIGNAL(MethodInfo("scene_saved", PropertyInfo(Variant::STRING, "filepath")));

	BIND_ENUM_CONSTANT(CONTAINER_TOOLBAR);
	BIND_ENUM_CONSTANT(CONTAINER_SPATIAL_EDITOR_MENU);
	BIND_ENUM_CONSTANT(CONTAINER_SPATIAL_EDITOR_SIDE_LEFT);
	BIND_ENUM_CONSTANT(CONTAINER_SPATIAL_EDITOR_SIDE_RIGHT);
	BIND_ENUM_CONSTANT(CONTAINER_SPATIAL_EDITOR_BOTTOM);
	BIND_ENUM_CONSTANT(CONTAINER_CANVAS_EDITOR_MENU);
	BIND_ENUM_CONSTANT(CONTAINER_CANVAS_EDITOR_SIDE_LEFT);
	BIND_ENUM_CONSTANT(CONTAINER_CANVAS_EDITOR_SIDE_RIGHT);
	BIND_ENUM_CONSTANT(CONTAINER_CANVAS_EDITOR_BOTTOM);
	BIND_ENUM_CONSTANT(CONTAINER_INSPECTOR_BOTTOM);
	BIND_ENUM_CONSTANT(CONTAINER_PROJECT_SETTING_TAB_LEFT);
	BIND_ENUM_CONSTANT(CONTAINER_PROJECT_SETTING_TAB_RIGHT);

	BIND_ENUM_CONSTANT(AFTER_GUI_INPUT_PASS);
	BIND_ENUM_CONSTANT(AFTER_GUI_INPUT_STOP);
	BIND_ENUM_CONSTANT(AFTER_GUI_INPUT_CUSTOM);
}

EditorUndoRedoManager *EditorPlugin::get_undo_redo() {
	return EditorUndoRedoManager::get_singleton();
}

EditorPluginCreateFunc EditorPlugins::creation_funcs[MAX_CREATE_FUNCS];

int EditorPlugins::creation_func_count = 0;
