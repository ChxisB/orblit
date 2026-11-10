/**************************************************************************/
/*  area_pair_planar.h                                                        */

#pragma once

#include "area_planar.h"
#include "body_planar.h"
#include "constraint_planar.h"

class OrblitAreaPair2D : public OrblitConstraint2D {
	OrblitBody2D *body = nullptr;
	OrblitArea2D *area = nullptr;
	int body_shape = 0;
	int area_shape = 0;
	bool colliding = false;
	bool has_space_override = false;
	bool process_collision = false;
	bool body_has_attached_area = false;

public:
	virtual bool setup(real_t p_step) override;
	virtual bool pre_solve(real_t p_step) override;
	virtual void solve(real_t p_step) override;

	OrblitAreaPair2D(OrblitBody2D *p_body, int p_body_shape, OrblitArea2D *p_area, int p_area_shape);
	~OrblitAreaPair2D();
};

class OrblitArea2Pair2D : public OrblitConstraint2D {
	OrblitArea2D *area_a = nullptr;
	OrblitArea2D *area_b = nullptr;
	int shape_a = 0;
	int shape_b = 0;
	bool colliding_a = false;
	bool colliding_b = false;
	bool process_collision_a = false;
	bool process_collision_b = false;
	bool area_a_monitorable;
	bool area_b_monitorable;

public:
	virtual bool setup(real_t p_step) override;
	virtual bool pre_solve(real_t p_step) override;
	virtual void solve(real_t p_step) override;

	OrblitArea2Pair2D(OrblitArea2D *p_area_a, int p_shape_a, OrblitArea2D *p_area_b, int p_shape_b);
	~OrblitArea2Pair2D();
};
