/**************************************************************************/
/*  parallax_planar.h                                                         */

#pragma once

#include "scene/planar/entity_planar.h"

class Parallax2D : public Entity2D {
	ORBLIT_CLASS(Parallax2D, Entity2D);

	static constexpr real_t DEFAULT_LIMIT = 10000000;

	String group_name;
	Size2 scroll_scale = Size2(1, 1);
	Point2 scroll_offset;
	Point2 screen_offset;
	Vector2 repeat_size;
	int repeat_times = 1;
	Point2 limit_begin = Point2(-DEFAULT_LIMIT, -DEFAULT_LIMIT);
	Point2 limit_end = Point2(DEFAULT_LIMIT, DEFAULT_LIMIT);
	Point2 autoscroll;
	bool follow_viewport = true;
	bool ignore_camera_scroll = false;

	void _update_process();
	void _update_repeat();
	void _update_scroll();

protected:
#ifdef TOOLS_ENABLED
	void _edit_set_position(const Point2 &p_position) override;
#endif // TOOLS_ENABLED
	void _validate_property(PropertyInfo &p_property) const;
	void _camera_moved(const Point2 &p_adj_screen_offset);
	void _notification(int p_what);
	static void register_members();

public:
	void set_scroll_scale(const Size2 &p_scale);
	Size2 get_scroll_scale() const;

	void set_repeat_size(const Size2 &p_repeat_size);
	Size2 get_repeat_size() const;

	void set_repeat_times(int p_repeat_times);
	int get_repeat_times() const;

	void set_autoscroll(const Point2 &p_autoscroll);
	Point2 get_autoscroll() const;

	void set_scroll_offset(const Point2 &p_offset);
	Point2 get_scroll_offset() const;

	void set_screen_offset(const Point2 &p_offset);
	Point2 get_screen_offset() const;

	void set_limit_begin(const Point2 &p_offset);
	Point2 get_limit_begin() const;

	void set_limit_end(const Point2 &p_offset);
	Point2 get_limit_end() const;

	void set_follow_viewport(bool p_follow);
	bool get_follow_viewport();

	void set_ignore_camera_scroll(bool p_ignore);
	bool is_ignore_camera_scroll();

	Parallax2D();
};
