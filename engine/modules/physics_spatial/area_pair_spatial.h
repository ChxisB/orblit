/**************************************************************************/
/*  area_pair_spatial.h                                                        */

#pragma once

#include "area_spatial.h"
#include "body_spatial.h"
#include "constraint_spatial.h"
#include "soft_body_spatial.h"

class OrblitAreaPair3D : public OrblitConstraint3D {
	OrblitBody3D *body = nullptr;
	OrblitArea3D *area = nullptr;
	int body_shape;
	int area_shape;
	bool colliding = false;
	bool process_collision = false;
	bool has_space_override = false;
	bool body_has_attached_area = false;

public:
	virtual bool setup(real_t p_step) override;
	virtual bool pre_solve(real_t p_step) override;
	virtual void solve(real_t p_step) override;

	OrblitAreaPair3D(OrblitBody3D *p_body, int p_body_shape, OrblitArea3D *p_area, int p_area_shape);
	~OrblitAreaPair3D();
};

class OrblitArea2Pair3D : public OrblitConstraint3D {
	OrblitArea3D *area_a = nullptr;
	OrblitArea3D *area_b = nullptr;
	int shape_a;
	int shape_b;
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

	OrblitArea2Pair3D(OrblitArea3D *p_area_a, int p_shape_a, OrblitArea3D *p_area_b, int p_shape_b);
	~OrblitArea2Pair3D();
};

class OrblitAreaSoftBodyPair3D : public OrblitConstraint3D {
	OrblitSoftBody3D *soft_body = nullptr;
	OrblitArea3D *area = nullptr;
	int soft_body_shape;
	int area_shape;
	bool colliding = false;
	bool process_collision = false;
	bool has_space_override = false;
	bool body_has_attached_area = false;

public:
	virtual bool setup(real_t p_step) override;
	virtual bool pre_solve(real_t p_step) override;
	virtual void solve(real_t p_step) override;

	OrblitAreaSoftBodyPair3D(OrblitSoftBody3D *p_sof_body, int p_soft_body_shape, OrblitArea3D *p_area, int p_area_shape);
	~OrblitAreaSoftBodyPair3D();
};
