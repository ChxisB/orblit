/**************************************************************************/
/*  native_menu_macos.h                                                   */

#pragma once

#include "core/containers/hash_map.h"
#include "core/containers/rid_owner.h"
#include "systems/display/native_menu.h"

#import <AppKit/AppKit.h>
#import <ApplicationServices/ApplicationServices.h>

class NativeMenuMacOS : public NativeMenu {
	ORBLIT_CLASS(NativeMenuMacOS, NativeMenu)

	struct MenuData {
		NSMenu *menu = nullptr;

		Callable open_cb;
		Callable close_cb;
		bool is_open = false;
		bool is_system = false;
	};

	mutable RID_PtrOwner<MenuData> menus;
	HashMap<NSMenu *, RID> menu_lookup;

	NSMenu *main_menu_ns = nullptr;
	NSMenu *application_menu_ns = nullptr;
	NSMenu *window_menu_ns = nullptr;
	NSMenu *help_menu_ns = nullptr;
	NSMenu *dock_menu_ns = nullptr;

	RID main_menu;
	RID application_menu;
	RID window_menu;
	RID help_menu;
	RID dock_menu;

	void _update_align(MenuData *p_md);

	int _get_system_menu_start(const NSMenu *p_menu) const;
	int _get_system_menu_count(const NSMenu *p_menu) const;
	bool _is_menu_opened(NSMenu *p_menu) const;
	NSMenuItem *_menu_add_item(NSMenu *p_menu, const String &p_label, Key p_accel, int p_index, int *r_out);

public:
	void _register_system_menus(NSMenu *p_main_menu, NSMenu *p_application_menu, NSMenu *p_window_menu, NSMenu *p_help_menu, NSMenu *p_dock_menu);
	NSMenu *_get_dock_menu();

	void _menu_need_update(NSMenu *p_menu);
	void _menu_open(NSMenu *p_menu);
	void _menu_close(NSMenu *p_menu);
	void _menu_close_cb(const RID &p_rid);

	virtual bool has_feature(Feature p_feature) const override;

	virtual bool has_system_menu(SystemMenus p_menu_id) const override;
	virtual RID get_system_menu(SystemMenus p_menu_id) const override;

	virtual RID create_menu() override;
	virtual bool has_menu(const RID &p_rid) const override;
	virtual void free_menu(const RID &p_rid) override;

	NSMenu *get_native_menu_handle(const RID &p_rid);

	virtual String get_system_menu_text(SystemMenus p_menu_id) const override;
	virtual void set_system_menu_text(SystemMenus p_menu_id, const String &p_name) override;

	virtual Size2 get_size(const RID &p_rid) const override;
	virtual void popup(const RID &p_rid, const Vector2i &p_position) override;

	virtual void set_interface_direction(const RID &p_rid, bool p_is_rtl) override;
	virtual void set_popup_open_callback(const RID &p_rid, const Callable &p_callback) override;
	virtual Callable get_popup_open_callback(const RID &p_rid) const override;
	virtual void set_popup_close_callback(const RID &p_rid, const Callable &p_callback) override;
	virtual Callable get_popup_close_callback(const RID &p_rid) const override;
	virtual void set_minimum_width(const RID &p_rid, float p_width) override;
	virtual float get_minimum_width(const RID &p_rid) const override;

	virtual bool is_opened(const RID &p_rid) const override;

	virtual int add_submenu_item(const RID &p_rid, const String &p_label, const RID &p_submenu_rid, const Variant &p_tag = Variant(), int p_index = -1) override;
	virtual int add_item(const RID &p_rid, const String &p_label, const Callable &p_callback = Callable(), const Callable &p_key_callback = Callable(), const Variant &p_tag = Variant(), Key p_accel = Key::NONE, int p_index = -1) override;
	virtual int add_check_item(const RID &p_rid, const String &p_label, const Callable &p_callback = Callable(), const Callable &p_key_callback = Callable(), const Variant &p_tag = Variant(), Key p_accel = Key::NONE, int p_index = -1) override;
	virtual int add_icon_item(const RID &p_rid, const Ref<Texture2D> &p_icon, const String &p_label, const Callable &p_callback = Callable(), const Callable &p_key_callback = Callable(), const Variant &p_tag = Variant(), Key p_accel = Key::NONE, int p_index = -1) override;
	virtual int add_icon_check_item(const RID &p_rid, const Ref<Texture2D> &p_icon, const String &p_label, const Callable &p_callback = Callable(), const Callable &p_key_callback = Callable(), const Variant &p_tag = Variant(), Key p_accel = Key::NONE, int p_index = -1) override;
	virtual int add_radio_check_item(const RID &p_rid, const String &p_label, const Callable &p_callback = Callable(), const Callable &p_key_callback = Callable(), const Variant &p_tag = Variant(), Key p_accel = Key::NONE, int p_index = -1) override;
	virtual int add_icon_radio_check_item(const RID &p_rid, const Ref<Texture2D> &p_icon, const String &p_label, const Callable &p_callback = Callable(), const Callable &p_key_callback = Callable(), const Variant &p_tag = Variant(), Key p_accel = Key::NONE, int p_index = -1) override;
	virtual int add_multistate_item(const RID &p_rid, const String &p_label, int p_max_states, int p_default_state, const Callable &p_callback = Callable(), const Callable &p_key_callback = Callable(), const Variant &p_tag = Variant(), Key p_accel = Key::NONE, int p_index = -1) override;
	virtual int add_separator(const RID &p_rid, int p_index = -1) override;

	virtual int find_item_index_with_text(const RID &p_rid, const String &p_text) const override;
	virtual int find_item_index_with_tag(const RID &p_rid, const Variant &p_tag) const override;

	virtual bool is_item_checked(const RID &p_rid, int p_idx) const override;
	virtual bool is_item_indeterminate(const RID &p_rid, int p_idx) const override;
	virtual bool is_item_checkable(const RID &p_rid, int p_idx) const override;
	virtual bool is_item_radio_checkable(const RID &p_rid, int p_idx) const override;
	virtual Callable get_item_callback(const RID &p_rid, int p_idx) const override;
	virtual Callable get_item_key_callback(const RID &p_rid, int p_idx) const override;
	virtual Variant get_item_tag(const RID &p_rid, int p_idx) const override;
	virtual String get_item_text(const RID &p_rid, int p_idx) const override;
	virtual RID get_item_submenu(const RID &p_rid, int p_idx) const override;
	virtual Key get_item_accelerator(const RID &p_rid, int p_idx) const override;
	virtual bool is_item_disabled(const RID &p_rid, int p_idx) const override;
	virtual bool is_item_hidden(const RID &p_rid, int p_idx) const override;
	virtual String get_item_tooltip(const RID &p_rid, int p_idx) const override;
	virtual int get_item_state(const RID &p_rid, int p_idx) const override;
	virtual int get_item_max_states(const RID &p_rid, int p_idx) const override;
	virtual Ref<Texture2D> get_item_icon(const RID &p_rid, int p_idx) const override;
	virtual int get_item_indentation_level(const RID &p_rid, int p_idx) const override;

	virtual void set_item_checked(const RID &p_rid, int p_idx, bool p_checked) override;
	virtual void set_item_indeterminate(const RID &p_rid, int p_idx, bool p_cindeterminate) override;
	virtual void set_item_checkable(const RID &p_rid, int p_idx, bool p_checkable) override;
	virtual void set_item_radio_checkable(const RID &p_rid, int p_idx, bool p_checkable) override;
	virtual void set_item_callback(const RID &p_rid, int p_idx, const Callable &p_callback) override;
	virtual void set_item_key_callback(const RID &p_rid, int p_idx, const Callable &p_key_callback) override;
	virtual void set_item_hover_callbacks(const RID &p_rid, int p_idx, const Callable &p_callback) override;
	virtual void set_item_tag(const RID &p_rid, int p_idx, const Variant &p_tag) override;
	virtual void set_item_text(const RID &p_rid, int p_idx, const String &p_text) override;
	virtual void set_item_submenu(const RID &p_rid, int p_idx, const RID &p_submenu_rid) override;
	virtual void set_item_accelerator(const RID &p_rid, int p_idx, Key p_keycode) override;
	virtual void set_item_disabled(const RID &p_rid, int p_idx, bool p_disabled) override;
	virtual void set_item_hidden(const RID &p_rid, int p_idx, bool p_hidden) override;
	virtual void set_item_tooltip(const RID &p_rid, int p_idx, const String &p_tooltip) override;
	virtual void set_item_state(const RID &p_rid, int p_idx, int p_state) override;
	virtual void set_item_max_states(const RID &p_rid, int p_idx, int p_max_states) override;
	virtual void set_item_icon(const RID &p_rid, int p_idx, const Ref<Texture2D> &p_icon) override;
	virtual void set_item_indentation_level(const RID &p_rid, int p_idx, int p_level) override;
	virtual int set_item_index(const RID &p_rid, int p_idx, int p_target_idx) override;

	virtual int get_item_count(const RID &p_rid) const override;
	virtual bool is_system_menu(const RID &p_rid) const override;

	virtual void remove_item(const RID &p_rid, int p_idx) override;
	virtual void clear(const RID &p_rid) override;

	NativeMenuMacOS();
	~NativeMenuMacOS();
};
