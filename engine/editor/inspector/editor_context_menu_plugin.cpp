/**************************************************************************/
/*  editor_context_menu_plugin.cpp                                        */

#include "editor_context_menu_plugin.h"

#include "core/input/shortcut.h"
#include "core/object/class_registry.h"
#include "editor/editor_string_names.h"
#include "scene/resources/texture.h"
#include "scene/ui/popup_menu.h"

void EditorContextMenuPlugin::get_options(const OptionsData &p_data) {
	context_data = p_data;
	ORBLIT_VIRTUAL_CALL(_get_menu_options, p_data);
}

void EditorContextMenuPlugin::add_menu_shortcut(const Ref<Shortcut> &p_shortcut, const Callable &p_callable) {
	context_menu_shortcuts.insert(p_shortcut, p_callable);
}

void EditorContextMenuPlugin::add_context_menu_item(const String &p_name, const Callable &p_callable, const Ref<Texture2D> &p_texture) {
	ERR_FAIL_COND_MSG(context_menu_items.has(p_name), "Context menu item already registered.");
	ERR_FAIL_COND_MSG(context_menu_items.size() == MAX_ITEMS, "Maximum number of context menu items reached.");

	ContextMenuItem item;
	item.item_name = p_name;
	item.callable = p_callable;
	item.icon = p_texture;
	context_menu_items.insert(p_name, item);
}

void EditorContextMenuPlugin::add_context_menu_item_from_shortcut(const String &p_name, const Ref<Shortcut> &p_shortcut, const Ref<Texture2D> &p_texture) {
	Callable *callback = context_menu_shortcuts.getptr(p_shortcut);
	ERR_FAIL_NULL_MSG(callback, "Shortcut not registered. Use add_menu_shortcut() first.");

	ContextMenuItem item;
	item.item_name = p_name;
	item.callable = *callback;
	item.icon = p_texture;
	item.shortcut = p_shortcut;
	context_menu_items.insert(p_name, item);
}

void EditorContextMenuPlugin::add_context_submenu_item(const String &p_name, PopupMenu *p_menu, const Ref<Texture2D> &p_texture) {
	ERR_FAIL_NULL(p_menu);

	ContextMenuItem item;
	item.item_name = p_name;
	item.icon = p_texture;
	item.submenu = p_menu;
	context_menu_items.insert(p_name, item);
}

void EditorContextMenuPlugin::register_members() {
	ClassRegistry::bind_method(D_METHOD("add_menu_shortcut", "shortcut", "callback"), &EditorContextMenuPlugin::add_menu_shortcut);
	ClassRegistry::bind_method(D_METHOD("add_context_menu_item", "name", "callback", "icon"), &EditorContextMenuPlugin::add_context_menu_item, DEFVAL(Ref<Texture2D>()));
	ClassRegistry::bind_method(D_METHOD("add_context_menu_item_from_shortcut", "name", "shortcut", "icon"), &EditorContextMenuPlugin::add_context_menu_item_from_shortcut, DEFVAL(Ref<Texture2D>()));
	ClassRegistry::bind_method(D_METHOD("add_context_submenu_item", "name", "menu", "icon"), &EditorContextMenuPlugin::add_context_submenu_item, DEFVAL(Ref<Texture2D>()));

	ORBLIT_VIRTUAL_BIND(_get_menu_options, "data");

	BIND_ENUM_CONSTANT(CONTEXT_SLOT_SCENE_TREE);
	BIND_ENUM_CONSTANT(CONTEXT_SLOT_FILESYSTEM);
	BIND_ENUM_CONSTANT(CONTEXT_SLOT_SCRIPT_EDITOR);
	BIND_ENUM_CONSTANT(CONTEXT_SLOT_FILESYSTEM_CREATE);
	BIND_ENUM_CONSTANT(CONTEXT_SLOT_SCRIPT_EDITOR_CODE);
	BIND_ENUM_CONSTANT(CONTEXT_SLOT_SCENE_TABS);
	BIND_ENUM_CONSTANT(CONTEXT_SLOT_2D_EDITOR);
	BIND_ENUM_CONSTANT(CONTEXT_SLOT_INSPECTOR_PROPERTY);
}

void EditorContextMenuPluginManager::add_plugin(EditorContextMenuPlugin::ContextMenuSlot p_slot, const Ref<EditorContextMenuPlugin> &p_plugin) {
	ERR_FAIL_COND(p_plugin.is_null());
	ERR_FAIL_COND(plugin_list.has(p_plugin));

	p_plugin->slot = p_slot;
	plugin_list.push_back(p_plugin);
}

void EditorContextMenuPluginManager::remove_plugin(const Ref<EditorContextMenuPlugin> &p_plugin) {
	ERR_FAIL_COND(p_plugin.is_null());
	ERR_FAIL_COND(!plugin_list.has(p_plugin));

	plugin_list.erase(p_plugin);
}

bool EditorContextMenuPluginManager::has_plugins_for_slot(ContextMenuSlot p_slot) {
	for (Ref<EditorContextMenuPlugin> &plugin : plugin_list) {
		if (plugin->slot == p_slot) {
			return true;
		}
	}
	return false;
}

void EditorContextMenuPluginManager::add_options_from_plugins(PopupMenu *p_popup, ContextMenuSlot p_slot, const EditorContextMenuPlugin::OptionsData &p_data, int p_id_offset) {
	bool separator_added = false;
	const int icon_size = p_popup->get_theme_constant(SNAME("class_icon_size"), EditorStringName(Editor));
	int id = EditorContextMenuPlugin::BASE_ID + p_id_offset;

	for (Ref<EditorContextMenuPlugin> &plugin : plugin_list) {
		if (plugin->slot != p_slot) {
			continue;
		}
		plugin->context_menu_items.clear();
		plugin->get_options(p_data);

		HashMap<String, EditorContextMenuPlugin::ContextMenuItem> &items = plugin->context_menu_items;
		if (!separator_added && !items.is_empty()) {
			separator_added = true;
			p_popup->add_separator();
		}

		for (KeyValue<String, EditorContextMenuPlugin::ContextMenuItem> &E : items) {
			EditorContextMenuPlugin::ContextMenuItem &item = E.value;
			item.id = id;

			if (item.submenu) {
				p_popup->add_submenu_entity_item(item.item_name, item.submenu, id);
			} else {
				p_popup->add_item(item.item_name, id);
			}

			if (item.icon.is_valid()) {
				p_popup->set_item_icon(-1, item.icon);
				p_popup->set_item_icon_max_width(-1, icon_size);
			}

			if (item.shortcut.is_valid()) {
				p_popup->set_item_shortcut(-1, item.shortcut, true);
			}
			id++;
		}
	}
}

Callable EditorContextMenuPluginManager::match_custom_shortcut(EditorContextMenuPlugin::ContextMenuSlot p_slot, const Ref<InputEvent> &p_event) {
	for (Ref<EditorContextMenuPlugin> &plugin : plugin_list) {
		if (plugin->slot != p_slot) {
			continue;
		}

		for (KeyValue<Ref<Shortcut>, Callable> &E : plugin->context_menu_shortcuts) {
			if (E.key->matches_event(p_event)) {
				return E.value;
			}
		}
	}
	return Callable();
}

bool EditorContextMenuPluginManager::activate_custom_option(ContextMenuSlot p_slot, int p_option) {
	for (Ref<EditorContextMenuPlugin> &plugin : plugin_list) {
		if (plugin->slot != p_slot) {
			continue;
		}

		for (KeyValue<String, EditorContextMenuPlugin::ContextMenuItem> &E : plugin->context_menu_items) {
			if (E.value.id == p_option) {
				invoke_callback(E.value.callable, plugin->context_data);
				return true;
			}
		}
	}
	return false;
}

void EditorContextMenuPluginManager::invoke_callback(const Callable &p_callback, const Variant &p_arg) {
	const Variant *argptr = &p_arg;
	Callable::CallError ce;
	Variant result;
	p_callback.callp(&argptr, 1, result, ce);

	if (ce.error != Callable::CallError::CALL_OK) {
		ERR_FAIL_MSG("Failed to execute context menu callback: " + Variant::get_callable_error_text(p_callback, &argptr, 1, ce) + ".");
	}
}

void EditorContextMenuPluginManager::create() {
	ERR_FAIL_COND(singleton != nullptr);
	singleton = memnew(EditorContextMenuPluginManager);
}

void EditorContextMenuPluginManager::cleanup() {
	ERR_FAIL_NULL(singleton);
	memdelete(singleton);
	singleton = nullptr;
}
