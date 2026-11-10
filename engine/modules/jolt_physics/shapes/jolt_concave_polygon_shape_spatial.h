/**************************************************************************/
/*  jolt_concave_polygon_shape_spatial.h                                       */

#pragma once

#include "jolt_shape_spatial.h"

class JoltConcavePolygonShape3D final : public JoltShape3D {
	AABB aabb;
	PackedVector3Array faces;
	bool back_face_collision = false;

	virtual JPH::ShapeRefC _build() const override;

	AABB _calculate_aabb() const;

public:
	virtual ShapeType get_type() const override { return ShapeType::SHAPE_CONCAVE_POLYGON; }
	virtual bool is_convex() const override { return false; }
	virtual bool has_back_face_collision() const override { return back_face_collision; }

	virtual Variant get_data() const override;
	virtual void set_data(const Variant &p_data) override;

	virtual float get_margin() const override { return 0.0f; }
	virtual void set_margin(float p_margin) override {}

	virtual AABB get_aabb() const override { return aabb; }

	String to_string() const;
};
