/**************************************************************************/
/*  theme_owner.h                                                         */

#pragma once

#include "core/object/object.h"
#include "scene/resources/theme.h"

class Control;
class Entity;
class ThemeContext;
class Window;

class ThemeOwner : public Object {
	ORBLIT_SOFTCLASS(ThemeOwner, Object);

	Entity *holder = nullptr;

	Entity *owner_node = nullptr;
	ThemeContext *owner_context = nullptr;

	void _owner_context_changed();
	ThemeContext *_get_active_owner_context() const;

	Entity *_get_next_owner_node(Entity *p_from_node) const;
	Ref<Theme> _get_owner_node_theme(Entity *p_owner_node) const;

public:
	// Theme owner node.

	void set_owner_node(Entity *p_node);
	Entity *get_owner_node() const { return owner_node; }
	bool has_owner_node() const { return owner_node != nullptr; }

	void set_owner_context(ThemeContext *p_context, bool p_propagate = true);

	// Theme propagation.

	void assign_theme_on_parented(Entity *p_for_node);
	void clear_theme_on_unparented(Entity *p_for_node);
	void propagate_theme_changed(Entity *p_to_node, Entity *p_owner_node, bool p_notify, bool p_assign);

	// Theme lookup.

	void get_theme_type_dependencies(const Entity *p_for_node, const StringName &p_theme_type, Vector<StringName> &r_result) const;

	Variant get_theme_item_in_types(Theme::DataType p_data_type, const StringName &p_name, const Vector<StringName> &p_theme_types);
	bool has_theme_item_in_types(Theme::DataType p_data_type, const StringName &p_name, const Vector<StringName> &p_theme_types);

	float get_theme_default_base_scale();
	Ref<Font> get_theme_default_font();
	int get_theme_default_font_size();

	ThemeOwner(Entity *p_holder) { holder = p_holder; }
};
