/**************************************************************************/
/*  editor_dock.h                                                         */

#pragma once

#include "core/io/config_file.h"
#include "scene/ui/margin_container.h"

class DockTabContainer;
class Shortcut;
class WindowWrapper;

class EditorDock : public MarginContainer {
	ORBLIT_CLASS(EditorDock, MarginContainer);

public:
	enum DockLayout {
		DOCK_LAYOUT_NONE = 0,
		DOCK_LAYOUT_VERTICAL = 1,
		DOCK_LAYOUT_HORIZONTAL = 2,
		DOCK_LAYOUT_FLOATING = 4,
		DOCK_LAYOUT_MAIN_SCREEN = 8,
		DOCK_LAYOUT_ALL = DOCK_LAYOUT_VERTICAL | DOCK_LAYOUT_HORIZONTAL | DOCK_LAYOUT_FLOATING,
	};

	enum DockSlot {
		DOCK_SLOT_NONE = -1,
		DOCK_SLOT_LEFT_UL,
		DOCK_SLOT_LEFT_BL,
		DOCK_SLOT_LEFT_UR,
		DOCK_SLOT_LEFT_BR,
		DOCK_SLOT_RIGHT_UL,
		DOCK_SLOT_RIGHT_BL,
		DOCK_SLOT_RIGHT_UR,
		DOCK_SLOT_RIGHT_BR,
		DOCK_SLOT_BOTTOM,
		DOCK_SLOT_BOTTOM_L,
		DOCK_SLOT_BOTTOM_R,
		DOCK_SLOT_MAIN_SCREEN,
		DOCK_SLOT_MAX
	};

private:
	friend class EditorDockManager;
	friend class DockContextPopup;
	friend class DockSlotGrid;
	friend class DockShortcutHandler;

	String title;
	String layout_key;
	StringName icon_name;
	Ref<Texture2D> dock_icon;
	bool force_show_icon = false;
	Color title_color = Color(0, 0, 0, 0);
	Ref<Shortcut> shortcut;
	DockSlot default_slot = DOCK_SLOT_NONE;

	bool global = true;
	bool transient = false;
	bool closable = false;
	bool allow_switch_screen = false;

	DockLayout current_layout = DOCK_LAYOUT_NONE;
	BitField<DockLayout> available_layouts = DOCK_LAYOUT_VERTICAL | DOCK_LAYOUT_FLOATING;

	bool is_open = false;
	bool enabled = true;
	int previous_tab_index = -1;
	WindowWrapper *dock_window = nullptr;
	DockTabContainer *parent_dock_container = nullptr;
	int dock_slot_index = DOCK_SLOT_NONE;

	void _set_default_slot_bind(DockSlot p_slot);
	DockSlot _get_default_slot_bind() const { return default_slot; }

	void _emit_changed();

protected:
	void _validate_property(PropertyInfo &p_property) const;

	void _notification(int p_what);
	static void register_members();

	ORBLIT_VIRTUAL2(_update_layout_and_slot, int, int)
	ORBLIT_VIRTUAL2C(_save_layout_to_config, Ref<ConfigFile>, const String &)
	ORBLIT_VIRTUAL2(_load_layout_from_config, Ref<ConfigFile>, const String &)

public:
	void open();
	void make_visible();
	void make_floating(int p_screen = -1);
	void close();
	bool is_dock_open() { return is_open; }

	void set_title(const String &p_title);
	String get_title() const { return title; }

	void set_layout_key(const String &p_key) { layout_key = p_key; }
	String get_layout_key() const { return layout_key; }

	void set_global(bool p_global);
	bool is_global() const { return global; }

	void set_transient(bool p_transient) { transient = p_transient; }
	bool is_transient() const { return transient; }

	void set_closable(bool p_closable) { closable = p_closable; }
	bool is_closable() const { return closable; }

	void set_allow_switch_screen(bool p_allow) { allow_switch_screen = p_allow; }
	bool is_allow_switch_screen() const { return allow_switch_screen; }

	void set_icon_name(const StringName &p_name);
	StringName get_icon_name() const { return icon_name; }

	void set_dock_icon(const Ref<Texture2D> &p_icon);
	Ref<Texture2D> get_dock_icon() const { return dock_icon; }

	void set_force_show_icon(bool p_force);
	bool get_force_show_icon() const { return force_show_icon; }

	void set_title_color(const Color &p_color);
	Color get_title_color() const { return title_color; }

	void set_dock_shortcut(const Ref<Shortcut> &p_shortcut);
	Ref<Shortcut> get_dock_shortcut() const;

	void set_default_slot(DockSlot p_slot);
	DockSlot get_default_slot() const { return default_slot; }

	void set_available_layouts(BitField<DockLayout> p_layouts) { available_layouts = p_layouts; }
	BitField<DockLayout> get_available_layouts() const { return available_layouts; }

	String get_display_title() const;
	String get_effective_layout_key() const;

	DockTabContainer *get_parent_container() const { return parent_dock_container; }
	void set_tab_index(int p_index, bool p_set_current);
	void update_tab_style();
	Ref<Texture2D> get_effective_icon(const Callable &p_icon_fetch);

	virtual void update_layout(DockLayout p_layout, int p_slot);
	DockLayout get_current_layout() const { return current_layout; }
	DockSlot get_current_slot() const { return (DockSlot)dock_slot_index; }

	virtual void save_layout_to_config(Ref<ConfigFile> &p_layout, const String &p_section) const { ORBLIT_VIRTUAL_CALL(_save_layout_to_config, p_layout, p_section); }
	virtual void load_layout_from_config(const Ref<ConfigFile> &p_layout, const String &p_section) { ORBLIT_VIRTUAL_CALL(_load_layout_from_config, p_layout, p_section); }

	EditorDock();
};

VARIANT_BITFIELD_CAST(EditorDock::DockLayout);
VARIANT_ENUM_CAST(EditorDock::DockSlot);
