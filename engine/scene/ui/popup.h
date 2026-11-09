/**************************************************************************/
/*  popup.h                                                               */

#pragma once

#include "core/containers/local_vector.h"
#include "scene/stage/window.h"

class Panel;

class Popup : public Window {
	ORBLIT_CLASS(Popup, Window);

	LocalVector<Window *> visible_parents;
	bool popped_up = false;

public:
	enum HideReason {
		HIDE_REASON_NONE,
		HIDE_REASON_CANCELED, // E.g., because of rupture of UI flow (app unfocused). Includes closed programmatically.
		HIDE_REASON_UNFOCUSED, // E.g., user clicked outside.
	};

private:
	HideReason hide_reason = HIDE_REASON_NONE;

	void _initialize_visible_parents();
	void _deinitialize_visible_parents();

protected:
	void _close_pressed();
	virtual Rect2i _popup_adjust_rect() const override;
	virtual void _input_from_window(const Ref<InputEvent> &p_event) override;

	void _notification(int p_what);
	void _validate_property(PropertyInfo &p_property) const;
	static void register_members();

	virtual void _parent_focused();

	virtual void _post_popup() override;

public:
	HideReason get_hide_reason() const { return hide_reason; }

	Popup();
	~Popup();
};

class PopupPanel : public Popup {
	ORBLIT_CLASS(PopupPanel, Popup);

	Panel *panel = nullptr;

	struct ThemeCache {
		Ref<StyleBox> panel_style;
	} theme_cache;

	mutable Rect2i pre_popup_rect;

protected:
	virtual void _input_from_window(const Ref<InputEvent> &p_event) override;

	virtual Rect2i _popup_adjust_rect() const override;

	void _update_shadow_offsets() const;
	void _update_child_rects() const;

	void _notification(int p_what);
	static void register_members();

	virtual Size2 _get_contents_minimum_size() const override;

public:
#ifdef TOOLS_ENABLED
	PackedStringArray get_configuration_warnings() const override;
#endif

	PopupPanel();
};
