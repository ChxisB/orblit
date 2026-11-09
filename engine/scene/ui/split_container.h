/**************************************************************************/
/*  split_container.h                                                     */

#pragma once

#include "scene/ui/container.h"

class TextureRect;
class SplitContainer;

class SplitContainerMultiDragger : public Control {
	ORBLIT_CLASS(SplitContainerMultiDragger, Control);

	bool dragging = false;
	int drag_from = 0;
	int start_drag_split_offset = 0;

protected:
	void _notification(int p_what);
	virtual void gui_input(const Ref<InputEvent> &p_event) override;

public:
	int dragger_index = -1;
	SplitContainer *split_container = nullptr;

	virtual CursorShape get_cursor_shape(const Point2 &p_pos = Point2i()) const override;

	void update_position();
	void stop_dragging();

	SplitContainerMultiDragger();
};

class SplitContainerDragger : public Control {
	ORBLIT_CLASS(SplitContainerDragger, Control);
	friend class SplitContainer;

	bool dragging = false;
	int drag_from = 0;
	int start_drag_split_offset = 0;
	bool mouse_inside = false;

	Rect2 split_bar_rect;
	TextureRect *touch_dragger = nullptr;

	void _touch_dragger_mouse_exited();
	void _touch_dragger_gui_input(const Ref<InputEvent> &p_event);

protected:
	void _notification(int p_what);
	virtual void gui_input(const Ref<InputEvent> &p_event) override;

	void _accessibility_action_inc(const Variant &p_data);
	void _accessibility_action_dec(const Variant &p_data);
	void _accessibility_action_set_value(const Variant &p_data);

	virtual String _get_accessibility_name() const override;

public:
	int dragger_index = -1;

	virtual CursorShape get_cursor_shape(const Point2 &p_pos = Point2i()) const override;

	void set_touch_dragger_enabled(bool p_enabled);
	void update_touch_dragger();
	void stop_dragging();

	SplitContainerDragger();
};

class SplitContainer : public Container {
	ORBLIT_CLASS(SplitContainer, Container);
	friend class SplitContainerDragger;
	friend class SplitContainerMultiDragger;

public:
	enum DraggerVisibility {
		DRAGGER_VISIBLE,
		DRAGGER_HIDDEN,
		DRAGGER_HIDDEN_COLLAPSED
	};

private:
	bool show_drag_area = false;
	int drag_area_margin_begin = 0;
	int drag_area_margin_end = 0;
	int drag_area_offset = 0;

	PackedInt32Array split_offsets;
	LocalVector<int> default_dragger_positions;
	LocalVector<int> dragger_positions;
	LocalVector<Control *> valid_children;
	LocalVector<SplitContainerDragger *> dragging_area_controls;

	bool vertical = false;
	bool collapsed = false;
	DraggerVisibility dragger_visibility = DRAGGER_VISIBLE;
	bool dragging_enabled = true;
	bool split_offset_pending = false;
	bool can_use_desired_sizes = false;
	bool initialized = false;

	bool touch_dragger_enabled = false;
	bool drag_nested_intersections = false;
	int force_show_grabber_icon = -1;

	struct ThemeCache {
		Color touch_dragger_color;
		Color touch_dragger_pressed_color;
		Color touch_dragger_hover_color;
		int separation = 0;
		int minimum_grab_thickness = 0;
		bool autohide = false;
		Ref<Texture2D> touch_dragger_icon;
		Ref<Texture2D> touch_dragger_icon_h;
		Ref<Texture2D> touch_dragger_icon_v;
		Ref<Texture2D> grabber_icon;
		Ref<Texture2D> grabber_icon_h;
		Ref<Texture2D> grabber_icon_v;
		float base_scale = 1.0;
		Ref<StyleBox> split_bar_background;
	} theme_cache;

	Ref<Texture2D> _get_grabber_icon() const;
	Ref<Texture2D> _get_touch_dragger_icon() const;
	Point2i _get_valid_range(int p_dragger_index) const;

	PackedInt32Array _get_desired_sizes() const;
	void _set_desired_sizes(const PackedInt32Array &p_desired_sizes, int p_priority_index = -1);

	void _update_default_dragger_positions();
	void _update_dragger_positions(int p_clamp_index = -1);
	int _get_separation() const;
	void _resort();
	void _update_draggers();
	void _on_child_visibility_changed(Control *p_control);
	void _add_valid_child(Control *p_control);
	void _remove_valid_child(Control *p_control);

	void _remove_nested_descendent(SplitContainer *p_nested_sc);
	void _update_nested_descendent(SplitContainer *p_nested_sc, Control *p_direct_child);
	void _update_nested_ancestors(bool p_remove = false);
	void _update_all_nested_descendents(Control *p_control, Control *p_first_child = nullptr);

	Size2 _get_minimum_size(bool p_use_desired_sizes) const;

protected:
	bool is_fixed = false;

	void _notification(int p_what);
	void _validate_property(PropertyInfo &p_property) const;

	virtual void add_child_notify(Entity *p_child) override;
	virtual void remove_child_notify(Entity *p_child) override;
	virtual void move_child_notify(Entity *p_child) override;
	static void register_members();

public:
	void set_split_offset(int p_offset, int p_index = 0);
	int get_split_offset(int p_index = 0) const;

	void set_split_offsets(const PackedInt32Array &p_offsets);
	PackedInt32Array get_split_offsets() const;

	void clamp_split_offset(int p_priority_index = 0);

	void set_collapsed(bool p_collapsed);
	bool is_collapsed() const;

	void set_dragger_visibility(DraggerVisibility p_visibility);
	DraggerVisibility get_dragger_visibility() const;

	void set_vertical(bool p_vertical);
	bool is_vertical() const;

	void set_dragging_enabled(bool p_enabled);
	bool is_dragging_enabled() const;

	virtual Size2 get_minimum_size() const override;
	virtual Size2 get_desired_size() const override;

	virtual Vector<int> get_allowed_size_flags_horizontal() const override;
	virtual Vector<int> get_allowed_size_flags_vertical() const override;

	void set_drag_area_margin_begin(int p_margin);
	int get_drag_area_margin_begin() const;

	void set_drag_area_margin_end(int p_margin);
	int get_drag_area_margin_end() const;

	void set_drag_area_offset(int p_offset);
	int get_drag_area_offset() const;

	void set_show_drag_area_enabled(bool p_enabled);
	bool is_show_drag_area_enabled() const;

	TypedArray<Control> get_drag_area_controls();

	void set_touch_dragger_enabled(bool p_enabled);
	bool is_touch_dragger_enabled() const;

	void show_grabber_icon(int p_index);

	void set_drag_nested_intersections(bool p_enabled);
	bool is_dragging_nested_intersections() const;

	SplitContainer(bool p_vertical = false);
};

VARIANT_ENUM_CAST(SplitContainer::DraggerVisibility);

class HSplitContainer : public SplitContainer {
	ORBLIT_CLASS(HSplitContainer, SplitContainer);

public:
	HSplitContainer() :
			SplitContainer(false) { is_fixed = true; }
};

class VSplitContainer : public SplitContainer {
	ORBLIT_CLASS(VSplitContainer, SplitContainer);

public:
	VSplitContainer() :
			SplitContainer(true) { is_fixed = true; }
};
