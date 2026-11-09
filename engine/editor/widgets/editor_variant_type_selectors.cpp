/**************************************************************************/
/*  editor_variant_type_selectors.cpp                                     */

#include "editor_variant_type_selectors.h"

struct CompareVariantTypeNames {
	bool operator()(const String &p_lhs, const String &p_rhs) const {
		// Variant type names should not be empty, but just in case.
		DEV_ASSERT(!p_lhs.is_empty() && !p_rhs.is_empty());

		// Variant type names are ascii strings.
		const bool lhs_lower = is_ascii_lower_case(p_lhs[0]);
		const bool rhs_lower = is_ascii_lower_case(p_rhs[0]);
		if (lhs_lower != rhs_lower) {
			// Lowercase types like `int` and `float` come first.
			return lhs_lower > rhs_lower;
		}

		return p_lhs < p_rhs;
	}
};

// EditorVariantTypeOptionButton

void EditorVariantTypeOptionButton::_update_menu_icons() {
	for (int i = 0; i < get_item_count(); i++) {
		const Variant::Type type = Variant::Type(get_item_id(i));
		const String &type_name = Variant::get_type_name(type);
		set_item_icon(i, get_editor_theme_icon(type_name));
	}
}

void EditorVariantTypeOptionButton::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_POSTINITIALIZE: {
			set_auto_translate_mode(AUTO_TRANSLATE_MODE_DISABLED);
		} break;

		case NOTIFICATION_THEME_CHANGED: {
			_update_menu_icons();
		} break;
	}
}

Variant::Type EditorVariantTypeOptionButton::get_selected_type() const {
	int selected = get_selected();
	if (selected == -1) {
		return Variant::NIL;
	}
	return Variant::Type(get_item_id(selected));
}

void EditorVariantTypeOptionButton::populate(const LocalVector<Variant::Type> &p_disabled_types, const HashMap<Variant::Type, String> &p_renames) {
	LocalVector<String> names;
	HashMap<String, Variant::Type> name_to_type;
	names.reserve(Variant::VARIANT_MAX);
	name_to_type.reserve(Variant::VARIANT_MAX);

	for (int i = 0; i < Variant::VARIANT_MAX; i++) {
		const Variant::Type type = Variant::Type(i);

		if (p_disabled_types.has(type) || type == Variant::RID || type == Variant::CALLABLE || type == Variant::SIGNAL) {
			continue;
		}

		const String &type_name = Variant::get_type_name(type);
		const String &display_name = p_renames.has(type) ? p_renames[type] : type_name;
		names.push_back(display_name);
		name_to_type[display_name] = type;
	}

	names.sort_custom<CompareVariantTypeNames>();

	for (const String &name : names) {
		add_item(name, name_to_type[name]);
	}

	_update_menu_icons();
}

// EditorVariantTypeArrayItemMenu

void EditorVariantTypePopupMenu::_populate() {
	if (remove_item) {
		add_item(TTRC("Remove Item"), Variant::VARIANT_MAX);
		set_item_auto_translate_mode(-1, AUTO_TRANSLATE_MODE_ALWAYS);
		add_separator();
	}

	LocalVector<String> names;
	names.reserve(Variant::VARIANT_MAX);

	for (int i = 0; i < Variant::VARIANT_MAX; i++) {
		const Variant::Type type = Variant::Type(i);

		if (type == Variant::RID || type == Variant::CALLABLE || type == Variant::SIGNAL) {
			continue;
		}

		names.push_back(Variant::get_type_name(type));
	}

	names.sort_custom<CompareVariantTypeNames>();

	for (const String &name : names) {
		add_item(name, Variant::get_type_by_name(name));
	}
}

void EditorVariantTypePopupMenu::_update_menu_icons() {
	if (remove_item) {
		set_item_icon(get_item_index(Variant::VARIANT_MAX), get_editor_theme_icon(SNAME("Remove")));
	}

	for (int i = 0; i < get_item_count(); i++) {
		int id = get_item_id(i);

		// Skip the Remove Item option and the separator without hardcoding the index.
		if (id < 0 || id >= Variant::VARIANT_MAX) {
			continue;
		}

		const Variant::Type type = Variant::Type(id);
		const String &type_name = Variant::get_type_name(type);
		set_item_icon(i, get_editor_theme_icon(type_name));
	}
}

void EditorVariantTypePopupMenu::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_POSTINITIALIZE: {
			set_auto_translate_mode(AUTO_TRANSLATE_MODE_DISABLED);
			_populate();
		} break;

		case NOTIFICATION_THEME_CHANGED: {
			icons_dirty = true;
		} break;
	}
}

void EditorVariantTypePopupMenu::_popup_base(const Rect2i &p_bounds) {
	if (icons_dirty) {
		_update_menu_icons();
		icons_dirty = false;
	}
	PopupMenu::_popup_base(p_bounds);
}

EditorVariantTypePopupMenu::EditorVariantTypePopupMenu(bool p_remove_item) {
	remove_item = p_remove_item;
	set_search_bar_enabled(true);
	set_search_bar_min_item_count(10);
}
