/**************************************************************************/
/*  path_planar.h                                                             */

#pragma once

#include "scene/planar/entity_planar.h"
#include "scene/resources/curve.h"

class Timer;

class Path2D : public Entity2D {
	ORBLIT_CLASS(Path2D, Entity2D);

	Ref<Curve2D> curve;

	void _curve_changed();

#ifdef DEBUG_ENABLED
	RID debug_mesh_rid;
	RID debug_instance;

	void _debug_create();
	void _debug_update();
	void _debug_free();
#endif // DEBUG_ENABLED

protected:
	void _notification(int p_what);
	static void register_members();

public:
#ifdef DEBUG_ENABLED
	virtual Rect2 _edit_get_rect() const override;
	virtual bool _edit_use_rect() const override;
	virtual bool _edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const override;
#endif

	void set_curve(const Ref<Curve2D> &p_curve);
	Ref<Curve2D> get_curve() const;
};

class PathFollow2D : public Entity2D {
	ORBLIT_CLASS(PathFollow2D, Entity2D);

public:
private:
	Path2D *path = nullptr;
	real_t progress = 0.0;
	Timer *update_timer = nullptr;
	real_t h_offset = 0.0;
	real_t v_offset = 0.0;
	bool cubic = true;
	bool loop = true;
	bool rotates = true;

	void _update_transform();

protected:
	void _validate_property(PropertyInfo &p_property) const;

	void _notification(int p_what);
	static void register_members();

public:
	void path_changed();

	void set_progress(real_t p_progress);
	real_t get_progress() const;

	void set_h_offset(real_t p_h_offset);
	real_t get_h_offset() const;

	void set_v_offset(real_t p_v_offset);
	real_t get_v_offset() const;

	void set_progress_ratio(real_t p_ratio);
	real_t get_progress_ratio() const;

	void set_loop(bool p_loop);
	bool has_loop() const;

	void set_rotation_enabled(bool p_enabled);
	bool is_rotation_enabled() const;

	void set_cubic_interpolation_enabled(bool p_enabled);
	bool is_cubic_interpolation_enabled() const;

	PackedStringArray get_configuration_warnings() const override;
};
