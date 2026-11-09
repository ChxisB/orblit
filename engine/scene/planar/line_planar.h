/**************************************************************************/
/*  line_planar.h                                                             */

#pragma once

#include "scene/planar/entity_planar.h"
#include "scene/resources/curve.h"
#include "scene/resources/gradient.h"

class Line2D : public Entity2D {
	ORBLIT_CLASS(Line2D, Entity2D);

public:
	enum LineJointMode {
		LINE_JOINT_SHARP = 0,
		LINE_JOINT_BEVEL,
		LINE_JOINT_ROUND
	};

	enum LineCapMode {
		LINE_CAP_NONE = 0,
		LINE_CAP_BOX,
		LINE_CAP_ROUND
	};

	enum LineTextureMode {
		LINE_TEXTURE_NONE = 0,
		LINE_TEXTURE_TILE,
		LINE_TEXTURE_STRETCH
	};

#ifdef DEBUG_ENABLED
	virtual Rect2 _edit_get_rect() const override;
	virtual bool _edit_use_rect() const override;
	virtual bool _edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const override;
#endif

	Line2D();

	void set_points(const Vector<Vector2> &p_points);
	Vector<Vector2> get_points() const;

	void set_point_position(int i, Vector2 pos);
	Vector2 get_point_position(int i) const;

	int get_point_count() const;

	void clear_points();

	void add_point(Vector2 pos, int atpos = -1);
	void remove_point(int i);

	void set_closed(bool p_closed);
	bool is_closed() const;

	void set_width(float width);
	float get_width() const;

	void set_curve(const Ref<Curve> &curve);
	Ref<Curve> get_curve() const;

	void set_default_color(Color color);
	Color get_default_color() const;

	void set_gradient(const Ref<Gradient> &gradient);
	Ref<Gradient> get_gradient() const;

	void set_texture(const Ref<Texture2D> &texture);
	Ref<Texture2D> get_texture() const;

	void set_texture_mode(const LineTextureMode mode);
	LineTextureMode get_texture_mode() const;

	void set_joint_mode(LineJointMode mode);
	LineJointMode get_joint_mode() const;

	void set_begin_cap_mode(LineCapMode mode);
	LineCapMode get_begin_cap_mode() const;

	void set_end_cap_mode(LineCapMode mode);
	LineCapMode get_end_cap_mode() const;

	void set_sharp_limit(float limit);
	float get_sharp_limit() const;

	void set_round_precision(int precision);
	int get_round_precision() const;

	void set_antialiased(bool p_antialiased);
	bool get_antialiased() const;

protected:
	void _notification(int p_what);
	void _draw();

	static void register_members();

private:
	void _gradient_changed();
	void _curve_changed();

private:
	Vector<Vector2> _points;
	LineJointMode _joint_mode = LINE_JOINT_SHARP;
	LineCapMode _begin_cap_mode = LINE_CAP_NONE;
	LineCapMode _end_cap_mode = LINE_CAP_NONE;
	bool _closed = false;
	float _width = 10.0;
	Ref<Curve> _curve;
	Color _default_color = Color(1, 1, 1);
	Ref<Gradient> _gradient;
	Ref<Texture2D> _texture;
	LineTextureMode _texture_mode = LINE_TEXTURE_NONE;
	float _sharp_limit = 2.f;
	int _round_precision = 8;
	bool _antialiased = false;
};

// Needed so we can bind functions
VARIANT_ENUM_CAST(Line2D::LineJointMode)
VARIANT_ENUM_CAST(Line2D::LineCapMode)
VARIANT_ENUM_CAST(Line2D::LineTextureMode)
