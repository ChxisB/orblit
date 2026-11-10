/**************************************************************************/
/*  area_pair_planar.cpp                                                      */

#include "area_pair_planar.h"

#include "collision_solver_planar.h"

bool OrblitAreaPair2D::setup(real_t p_step) {
	bool result = false;
	if (area->collides_with(body) && OrblitCollisionSolver2D::solve(body->get_shape(body_shape), body->get_transform() * body->get_shape_transform(body_shape), Vector2(), area->get_shape(area_shape), area->get_transform() * area->get_shape_transform(area_shape), Vector2(), nullptr, this)) {
		result = true;
	}

	process_collision = false;
	has_space_override = false;
	if (result != colliding) {
		if ((int)area->get_param(PS2DE::AREA_PARAM_GRAVITY_OVERRIDE_MODE) != PS2DE::AREA_SPACE_OVERRIDE_DISABLED) {
			has_space_override = true;
		} else if ((int)area->get_param(PS2DE::AREA_PARAM_LINEAR_DAMP_OVERRIDE_MODE) != PS2DE::AREA_SPACE_OVERRIDE_DISABLED) {
			has_space_override = true;
		} else if ((int)area->get_param(PS2DE::AREA_PARAM_ANGULAR_DAMP_OVERRIDE_MODE) != PS2DE::AREA_SPACE_OVERRIDE_DISABLED) {
			has_space_override = true;
		}
		process_collision = has_space_override;

		if (area->has_monitor_callback()) {
			process_collision = true;
		}

		colliding = result;
	}

	return process_collision;
}

bool OrblitAreaPair2D::pre_solve(real_t p_step) {
	if (!process_collision) {
		return false;
	}

	if (colliding) {
		if (has_space_override) {
			body_has_attached_area = true;
			body->add_area(area);
		}

		if (area->has_monitor_callback()) {
			area->add_body_to_query(body, body_shape, area_shape);
		}
	} else {
		if (has_space_override) {
			body_has_attached_area = false;
			body->remove_area(area);
		}

		if (area->has_monitor_callback()) {
			area->remove_body_from_query(body, body_shape, area_shape);
		}
	}

	return false; // Never do any post solving.
}

void OrblitAreaPair2D::solve(real_t p_step) {
	// Nothing to do.
}

OrblitAreaPair2D::OrblitAreaPair2D(OrblitBody2D *p_body, int p_body_shape, OrblitArea2D *p_area, int p_area_shape) {
	body = p_body;
	area = p_area;
	body_shape = p_body_shape;
	area_shape = p_area_shape;
	body->add_constraint(this, 0);
	area->add_constraint(this);
	if (p_body->get_mode() == PS2DE::BODY_MODE_KINEMATIC) { //need to be active to process pair
		p_body->set_active(true);
	}
}

OrblitAreaPair2D::~OrblitAreaPair2D() {
	if (colliding) {
		if (body_has_attached_area) {
			body_has_attached_area = false;
			body->remove_area(area);
		}
		if (area->has_monitor_callback()) {
			area->remove_body_from_query(body, body_shape, area_shape);
		}
	}
	body->remove_constraint(this, 0);
	area->remove_constraint(this);
}

//////////////////////////////////

bool OrblitArea2Pair2D::setup(real_t p_step) {
	bool result_a = area_a->collides_with(area_b);
	bool result_b = area_b->collides_with(area_a);
	if ((result_a || result_b) && !OrblitCollisionSolver2D::solve(area_a->get_shape(shape_a), area_a->get_transform() * area_a->get_shape_transform(shape_a), Vector2(), area_b->get_shape(shape_b), area_b->get_transform() * area_b->get_shape_transform(shape_b), Vector2(), nullptr, this)) {
		result_a = false;
		result_b = false;
	}

	bool process_collision = false;

	process_collision_a = false;
	if (result_a != colliding_a) {
		if (area_a->has_area_monitor_callback() && area_b_monitorable) {
			process_collision_a = true;
			process_collision = true;
		}
		colliding_a = result_a;
	}

	process_collision_b = false;
	if (result_b != colliding_b) {
		if (area_b->has_area_monitor_callback() && area_a_monitorable) {
			process_collision_b = true;
			process_collision = true;
		}
		colliding_b = result_b;
	}

	return process_collision;
}

bool OrblitArea2Pair2D::pre_solve(real_t p_step) {
	if (process_collision_a) {
		if (colliding_a) {
			area_a->add_area_to_query(area_b, shape_b, shape_a);
		} else {
			area_a->remove_area_from_query(area_b, shape_b, shape_a);
		}
	}

	if (process_collision_b) {
		if (colliding_b) {
			area_b->add_area_to_query(area_a, shape_a, shape_b);
		} else {
			area_b->remove_area_from_query(area_a, shape_a, shape_b);
		}
	}

	return false; // Never do any post solving.
}

void OrblitArea2Pair2D::solve(real_t p_step) {
	// Nothing to do.
}

OrblitArea2Pair2D::OrblitArea2Pair2D(OrblitArea2D *p_area_a, int p_shape_a, OrblitArea2D *p_area_b, int p_shape_b) {
	area_a = p_area_a;
	area_b = p_area_b;
	shape_a = p_shape_a;
	shape_b = p_shape_b;
	area_a_monitorable = area_a->is_monitorable();
	area_b_monitorable = area_b->is_monitorable();
	area_a->add_constraint(this);
	area_b->add_constraint(this);
}

OrblitArea2Pair2D::~OrblitArea2Pair2D() {
	if (colliding_a) {
		if (area_a->has_area_monitor_callback() && area_b_monitorable) {
			area_a->remove_area_from_query(area_b, shape_b, shape_a);
		}
	}

	if (colliding_b) {
		if (area_b->has_area_monitor_callback() && area_a_monitorable) {
			area_b->remove_area_from_query(area_a, shape_a, shape_b);
		}
	}

	area_a->remove_constraint(this);
	area_b->remove_constraint(this);
}
