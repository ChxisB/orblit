/**************************************************************************/
/*  step_planar.h                                                             */

#pragma once

#include "space_planar.h"

#include "core/containers/local_vector.h"

class OrblitStep2D {
	uint64_t _step = 1;

	int iterations = 0;
	real_t delta = 0.0;

	LocalVector<LocalVector<OrblitBody2D *>> body_islands;
	LocalVector<LocalVector<OrblitConstraint2D *>> constraint_islands;
	LocalVector<OrblitConstraint2D *> all_constraints;

	void _populate_island(OrblitBody2D *p_body, LocalVector<OrblitBody2D *> &p_body_island, LocalVector<OrblitConstraint2D *> &p_constraint_island);
	void _setup_constraint(uint32_t p_constraint_index, void *p_userdata = nullptr);
	void _pre_solve_island(LocalVector<OrblitConstraint2D *> &p_constraint_island) const;
	void _solve_island(uint32_t p_island_index, void *p_userdata = nullptr) const;
	void _check_suspend(LocalVector<OrblitBody2D *> &p_body_island) const;

public:
	void step(OrblitSpace2D *p_space, real_t p_delta);
	OrblitStep2D();
	~OrblitStep2D();
};
