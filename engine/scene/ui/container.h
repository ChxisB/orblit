/**************************************************************************/
/*  container.h                                                           */

#pragma once

#include "scene/ui/control.h"

class Container : public Control {
	ORBLIT_CLASS(Container, Control);

	// A control tells its container when it stops or starts being top level.
	friend class Control;

	bool pending_sort = false;
	bool accessibility_region = false;
	void _sort_children();
	void _child_minsize_changed();
	void _child_desired_size_changed();

protected:
	enum class SortableVisibilityMode {
		VISIBLE,
		VISIBLE_IN_TREE,
		IGNORE,
	};

	void queue_sort();
	Control *as_sortable_control(Entity *p_node, SortableVisibilityMode p_visibility_mode = SortableVisibilityMode::VISIBLE_IN_TREE) const;

	virtual void add_child_notify(Entity *p_child) override;
	virtual void move_child_notify(Entity *p_child) override;
	virtual void remove_child_notify(Entity *p_child) override;

	Size2 _get_minimum_size() const;

	ORBLIT_VIRTUAL0RC(Vector<int>, _get_allowed_size_flags_horizontal)
	ORBLIT_VIRTUAL0RC(Vector<int>, _get_allowed_size_flags_vertical)

	void _notification(int p_what);
	static void register_members();

public:
	enum {
		NOTIFICATION_PRE_SORT_CHILDREN = 50,
		NOTIFICATION_SORT_CHILDREN = 51,
	};

	// Multiplication factor to be used by Containers when a child has the SIZE_MAXIMIZE flag. Effectively infinite to ensure it gets as much space as possible.
	constexpr static float MAXIMIZE_STRETCH_FACTOR = 1e10;

	void fit_child_in_rect(RequiredParam<Control> p_child, const Rect2 &p_rect);

	virtual Vector<int> get_allowed_size_flags_horizontal() const;
	virtual Vector<int> get_allowed_size_flags_vertical() const;

	PackedStringArray get_configuration_warnings() const override;

	void set_accessibility_region(bool p_region);
	bool is_accessibility_region() const;

	Container();
};
