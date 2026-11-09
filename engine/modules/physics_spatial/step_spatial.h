/**************************************************************************/
/*  step_spatial.h                                                             */

#pragma once

#include "space_spatial.h"

#include "core/containers/local_vector.h"

class OrblitStep3D {
	uint64_t _step = 1;

	int iterations = 0;
	real_t delta = 0.0;

	LocalVector<LocalVector<OrblitBody3D *>> body_islands;
	LocalVector<LocalVector<OrblitConstraint3D *>> constraint_islands;
	LocalVector<OrblitConstraint3D *> all_constraints;

	void _populate_island(OrblitBody3D *p_body, LocalVector<OrblitBody3D *> &p_body_island, LocalVector<OrblitConstraint3D *> &p_constraint_island);
	void _populate_island_soft_body(OrblitSoftBody3D *p_soft_body, LocalVector<OrblitBody3D *> &p_body_island, LocalVector<OrblitConstraint3D *> &p_constraint_island);
	void _setup_constraint(uint32_t p_constraint_index, void *p_userdata = nullptr);
	void _pre_solve_island(LocalVector<OrblitConstraint3D *> &p_constraint_island) const;
	void _solve_island(uint32_t p_island_index, void *p_userdata = nullptr);
	void _check_suspend(const LocalVector<OrblitBody3D *> &p_body_island) const;

public:
	void step(OrblitSpace3D *p_space, real_t p_delta);
	OrblitStep3D();
	~OrblitStep3D();
};
