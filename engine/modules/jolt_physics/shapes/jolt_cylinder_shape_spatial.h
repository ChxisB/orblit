/**************************************************************************/
/*  jolt_cylinder_shape_spatial.h                                              */

#pragma once

#include "jolt_shape_spatial.h"

class JoltCylinderShape3D final : public JoltShape3D {
	float height = 0.0f;
	float radius = 0.0f;
	float margin = 0.04f;

	virtual JPH::ShapeRefC _build() const override;

public:
	virtual ShapeType get_type() const override { return ShapeType::SHAPE_CYLINDER; }
	virtual bool is_convex() const override { return true; }
	virtual bool has_back_face_collision() const override { return false; }

	virtual Variant get_data() const override;
	virtual void set_data(const Variant &p_data) override;

	virtual float get_margin() const override { return margin; }
	virtual void set_margin(float p_margin) override;

	virtual AABB get_aabb() const override;

	String to_string() const;
};
