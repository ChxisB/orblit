/**************************************************************************/
/*  menu_button.h                                                         */

#pragma once

#include "scene/ui/button.h"
#include "scene/ui/popup_menu.h"
#include "scene/property_list_helper.h"

class MenuButton : public Button {
	ORBLIT_CLASS(MenuButton, Button);

	bool switch_on_hover = false;
	bool disable_shortcuts = false;
	PopupMenu *popup = nullptr;

	static inline PropertyListHelper base_property_helper;
	PropertyListHelper property_helper;

	void _popup_visibility_changed(bool p_visible);

protected:
	void _notification(int p_what);
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const { property_helper.get_property_list(p_list); }
	bool _property_can_revert(const StringName &p_name) const { return property_helper.property_can_revert(p_name); }
	bool _property_get_revert(const StringName &p_name, Variant &r_property) const { return property_helper.property_get_revert(p_name, r_property); }
	static void register_members();
	virtual void shortcut_input(const Ref<InputEvent> &p_event) override;

public:
	virtual void pressed() override;

	PopupMenu *get_popup() const;
	void show_popup();

	void set_switch_on_hover(bool p_enabled);
	bool is_switch_on_hover();
	void set_disable_shortcuts(bool p_disabled);

	void set_item_count(int p_count);
	int get_item_count() const;

#ifdef TOOLS_ENABLED
	PackedStringArray get_configuration_warnings() const override;
#endif

	MenuButton(const String &p_text = String());
	~MenuButton();
};
