/**************************************************************************/
/*  editor_context_menu_plugin.h                                          */

#pragma once

#include "core/object/orblit_virtual.gen.h"
#include "core/object/ref_counted.h"
#include "core/variant/typed_dictionary.h"

class InputEvent;
class PopupMenu;
class Shortcut;
class Texture2D;

class EditorContextMenuPlugin : public RefCounted {
	ORBLIT_CLASS(EditorContextMenuPlugin, RefCounted);

	friend class EditorContextMenuPluginManager;

	static constexpr int MAX_ITEMS = 100;

	int slot = -1;

public:
	enum ContextMenuSlot {
		CONTEXT_SLOT_SCENE_TREE,
		CONTEXT_SLOT_FILESYSTEM,
		CONTEXT_SLOT_SCRIPT_EDITOR,
		CONTEXT_SLOT_FILESYSTEM_CREATE,
		CONTEXT_SLOT_SCRIPT_EDITOR_CODE,
		CONTEXT_SLOT_SCENE_TABS,
		CONTEXT_SLOT_2D_EDITOR,
		CONTEXT_SLOT_INSPECTOR_PROPERTY,
	};
	static constexpr int BASE_ID = 2000;

	using OptionsData = TypedDictionary<StringName, Variant>;

	struct ContextMenuItem {
		int id = 0;
		String item_name;
		Callable callable;
		Ref<Texture2D> icon;
		Ref<Shortcut> shortcut;
		PopupMenu *submenu = nullptr;
	};

private:
	HashMap<String, ContextMenuItem> context_menu_items;
	HashMap<Ref<Shortcut>, Callable> context_menu_shortcuts;

	OptionsData context_data;

protected:
	static void register_members();

	ORBLIT_VIRTUAL1(_get_menu_options, const OptionsData &);

public:
	virtual void get_options(const OptionsData &p_data);

	void add_menu_shortcut(const Ref<Shortcut> &p_shortcut, const Callable &p_callable);
	void add_context_menu_item(const String &p_name, const Callable &p_callable, const Ref<Texture2D> &p_texture);
	void add_context_menu_item_from_shortcut(const String &p_name, const Ref<Shortcut> &p_shortcut, const Ref<Texture2D> &p_texture);
	void add_context_submenu_item(const String &p_name, PopupMenu *p_menu, const Ref<Texture2D> &p_texture);
};

VARIANT_ENUM_CAST(EditorContextMenuPlugin::ContextMenuSlot);

class EditorContextMenuPluginManager : public Object {
	ORBLIT_CLASS(EditorContextMenuPluginManager, Object);

	using ContextMenuSlot = EditorContextMenuPlugin::ContextMenuSlot;
	static inline EditorContextMenuPluginManager *singleton = nullptr;

	LocalVector<Ref<EditorContextMenuPlugin>> plugin_list;

public:
	static EditorContextMenuPluginManager *get_singleton() { return singleton; }

	void add_plugin(ContextMenuSlot p_slot, const Ref<EditorContextMenuPlugin> &p_plugin);
	void remove_plugin(const Ref<EditorContextMenuPlugin> &p_plugin);

	bool has_plugins_for_slot(ContextMenuSlot p_slot);
	void add_options_from_plugins(PopupMenu *p_popup, ContextMenuSlot p_slot, const EditorContextMenuPlugin::OptionsData &p_data, int p_id_offset = 0);
	void add_options_from_plugins(PopupMenu *p_popup, ContextMenuSlot p_slot, const Vector<String> &p_paths, const Variant &p_data, int p_id_offset = 0);
	Callable match_custom_shortcut(ContextMenuSlot p_slot, const Ref<InputEvent> &p_event);
	bool activate_custom_option(ContextMenuSlot p_slot, int p_option);

	void invoke_callback(const Callable &p_callback, const Variant &p_arg);

	static void create();
	static void cleanup();
};
