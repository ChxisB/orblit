/**************************************************************************/
/*  editor_variant_type_selectors.h                                       */

#pragma once

#include "core/variant/variant.h"
#include "scene/ui/option_button.h"
#include "scene/ui/popup_menu.h"

class EditorVariantTypeOptionButton : public OptionButton {
	ORBLIT_CLASS(EditorVariantTypeOptionButton, OptionButton);

	void _update_menu_icons();

protected:
	void _notification(int p_what);

public:
	Variant::Type get_selected_type() const;

	void populate(const LocalVector<Variant::Type> &p_disabled_types, const HashMap<Variant::Type, String> &p_renames = {});
};

class EditorVariantTypePopupMenu : public PopupMenu {
	ORBLIT_CLASS(EditorVariantTypePopupMenu, PopupMenu);

	bool remove_item = false;
	bool icons_dirty = true;

	void _populate();
	void _update_menu_icons();

protected:
	void _notification(int p_what);
	virtual void _popup_base(const Rect2i &p_bounds = Rect2i()) override;

public:
	EditorVariantTypePopupMenu(bool p_remove_item);
};
