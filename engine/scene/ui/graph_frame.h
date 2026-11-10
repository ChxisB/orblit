/**************************************************************************/
/*  graph_frame.h                                                         */

#pragma once

#include "scene/ui/graph_element.h"

class HBoxContainer;

class GraphFrame : public GraphElement {
	ORBLIT_CLASS(GraphFrame, GraphElement);

	struct ThemeCache {
		Ref<StyleBox> panel;
		Ref<StyleBox> panel_selected;
		Ref<StyleBox> titlebar;
		Ref<StyleBox> titlebar_selected;

		Ref<Texture2D> resizer;
		Color resizer_color;
	} theme_cache;

private:
	String title;

	HBoxContainer *titlebar_hbox = nullptr;
	Label *title_label = nullptr;

	bool autoshrink_enabled = true;
	int autoshrink_margin = 40;
	int drag_margin = 16;

	bool tint_color_enabled = false;
	Color tint_color = Color(0.3, 0.3, 0.3, 0.75);

	Size2 _get_minimum_size(bool p_use_desired_sizes) const;

protected:
	virtual void gui_input(const Ref<InputEvent> &p_event) override;
	virtual CursorShape get_cursor_shape(const Point2 &p_pos = Point2i()) const override;

	void _notification(int p_what);
	static void register_members();

	void _validate_property(PropertyInfo &p_property) const;

	virtual void _resort() override;

public:
	void set_title(const String &p_title);
	String get_title() const;

	void set_autoshrink_enabled(bool p_enable);
	bool is_autoshrink_enabled() const;

	void set_autoshrink_margin(const int &p_margin);
	int get_autoshrink_margin() const;

	HBoxContainer *get_titlebar_hbox();
	Size2 get_titlebar_size() const;

	void set_drag_margin(int p_margin);
	int get_drag_margin() const;

	void set_tint_color_enabled(bool p_enable);
	bool is_tint_color_enabled() const;

	void set_tint_color(const Color &p_tint_color);
	Color get_tint_color() const;

	virtual bool has_point(const Point2 &p_point) const override;
	virtual Size2 get_minimum_size() const override;
	virtual Size2 get_desired_size() const override;

	GraphFrame();
};
