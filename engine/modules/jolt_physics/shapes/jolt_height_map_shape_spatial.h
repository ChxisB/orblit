/**************************************************************************/
/*  jolt_height_map_shape_spatial.h                                            */

#pragma once

#include "jolt_shape_spatial.h"

class JoltHeightMapShape3D final : public JoltShape3D {
	AABB aabb;

#ifdef REAL_T_IS_DOUBLE
	PackedFloat64Array heights;
#else
	PackedFloat32Array heights;
#endif

	int width = 0;
	int depth = 0;

	virtual JPH::ShapeRefC _build() const override;
	JPH::ShapeRefC _build_height_field() const;
	JPH::ShapeRefC _build_mesh() const;

	AABB _calculate_aabb() const;

public:
	virtual ShapeType get_type() const override { return ShapeType::SHAPE_HEIGHTMAP; }
	virtual bool is_convex() const override { return false; }
	virtual bool has_back_face_collision() const override { return true; }

	virtual Variant get_data() const override;
	virtual void set_data(const Variant &p_data) override;

	virtual float get_margin() const override { return 0.0f; }
	virtual void set_margin(float p_margin) override {}

	virtual AABB get_aabb() const override { return aabb; }

	String to_string() const;
};
