/**************************************************************************/
/*  jolt_convex_polygon_shape_spatial.h                                        */

#pragma once

#include "jolt_shape_spatial.h"

class JoltConvexPolygonShape3D final : public JoltShape3D {
	AABB aabb;
	PackedVector3Array vertices;
	float margin = 0.04f;

	virtual JPH::ShapeRefC _build() const override;

	AABB _calculate_aabb() const;

public:
	virtual ShapeType get_type() const override { return ShapeType::SHAPE_CONVEX_POLYGON; }
	virtual bool is_convex() const override { return true; }
	virtual bool has_back_face_collision() const override { return false; }

	virtual Variant get_data() const override;
	virtual void set_data(const Variant &p_data) override;

	virtual float get_margin() const override { return margin; }
	virtual void set_margin(float p_margin) override;

	virtual AABB get_aabb() const override { return aabb; }

	String to_string() const;
};
