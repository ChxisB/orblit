/**************************************************************************/
/*  area_pair_spatial.cpp                                                      */

#include "area_pair_spatial.h"

#include "collision_solver_spatial.h"

bool OrblitAreaPair3D::setup(real_t p_step) {
	bool result = false;
	if (area->collides_with(body) && OrblitCollisionSolver3D::solve_static(body->get_shape(body_shape), body->get_transform() * body->get_shape_transform(body_shape), area->get_shape(area_shape), area->get_transform() * area->get_shape_transform(area_shape), nullptr, this)) {
		result = true;
	}

	process_collision = false;
	has_space_override = false;
	if (result != colliding) {
		if ((int)area->get_param(PS3DE::AREA_PARAM_GRAVITY_OVERRIDE_MODE) != PS3DE::AREA_SPACE_OVERRIDE_DISABLED) {
			has_space_override = true;
		} else if ((int)area->get_param(PS3DE::AREA_PARAM_LINEAR_DAMP_OVERRIDE_MODE) != PS3DE::AREA_SPACE_OVERRIDE_DISABLED) {
			has_space_override = true;
		} else if ((int)area->get_param(PS3DE::AREA_PARAM_ANGULAR_DAMP_OVERRIDE_MODE) != PS3DE::AREA_SPACE_OVERRIDE_DISABLED) {
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

bool OrblitAreaPair3D::pre_solve(real_t p_step) {
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

void OrblitAreaPair3D::solve(real_t p_step) {
	// Nothing to do.
}

OrblitAreaPair3D::OrblitAreaPair3D(OrblitBody3D *p_body, int p_body_shape, OrblitArea3D *p_area, int p_area_shape) {
	body = p_body;
	area = p_area;
	body_shape = p_body_shape;
	area_shape = p_area_shape;
	body->add_constraint(this, 0);
	area->add_constraint(this);
	if (p_body->get_mode() == PS3DE::BODY_MODE_KINEMATIC) {
		p_body->set_active(true);
	}
}

OrblitAreaPair3D::~OrblitAreaPair3D() {
	if (colliding) {
		if (body_has_attached_area) {
			body_has_attached_area = false;
			body->remove_area(area);
		}
		if (area->has_monitor_callback()) {
			area->remove_body_from_query(body, body_shape, area_shape);
		}
	}
	body->remove_constraint(this);
	area->remove_constraint(this);
}

////////////////////////////////////////////////////

bool OrblitArea2Pair3D::setup(real_t p_step) {
	bool result_a = area_a->collides_with(area_b);
	bool result_b = area_b->collides_with(area_a);
	if ((result_a || result_b) && !OrblitCollisionSolver3D::solve_static(area_a->get_shape(shape_a), area_a->get_transform() * area_a->get_shape_transform(shape_a), area_b->get_shape(shape_b), area_b->get_transform() * area_b->get_shape_transform(shape_b), nullptr, this)) {
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

bool OrblitArea2Pair3D::pre_solve(real_t p_step) {
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

void OrblitArea2Pair3D::solve(real_t p_step) {
	// Nothing to do.
}

OrblitArea2Pair3D::OrblitArea2Pair3D(OrblitArea3D *p_area_a, int p_shape_a, OrblitArea3D *p_area_b, int p_shape_b) {
	area_a = p_area_a;
	area_b = p_area_b;
	shape_a = p_shape_a;
	shape_b = p_shape_b;
	area_a_monitorable = area_a->is_monitorable();
	area_b_monitorable = area_b->is_monitorable();
	area_a->add_constraint(this);
	area_b->add_constraint(this);
}

OrblitArea2Pair3D::~OrblitArea2Pair3D() {
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

////////////////////////////////////////////////////

bool OrblitAreaSoftBodyPair3D::setup(real_t p_step) {
	bool result = false;
	if (
			area->collides_with(soft_body) &&
			OrblitCollisionSolver3D::solve_static(
					soft_body->get_shape(soft_body_shape),
					soft_body->get_transform() * soft_body->get_shape_transform(soft_body_shape),
					area->get_shape(area_shape),
					area->get_transform() * area->get_shape_transform(area_shape),
					nullptr,
					this)) {
		result = true;
	}

	process_collision = false;
	has_space_override = false;
	if (result != colliding) {
		if ((int)area->get_param(PS3DE::AREA_PARAM_GRAVITY_OVERRIDE_MODE) != PS3DE::AREA_SPACE_OVERRIDE_DISABLED) {
			has_space_override = true;
		} else if (area->get_wind_force_magnitude() > CMP_EPSILON) {
			has_space_override = true;
		}

		if (area->has_monitor_callback()) {
			process_collision = true;
		}

		colliding = result;
	}

	return process_collision;
}

bool OrblitAreaSoftBodyPair3D::pre_solve(real_t p_step) {
	if (!process_collision) {
		return false;
	}

	if (colliding) {
		if (has_space_override) {
			body_has_attached_area = true;
			soft_body->add_area(area);
		}

		if (area->has_monitor_callback()) {
			area->add_soft_body_to_query(soft_body, soft_body_shape, area_shape);
		}
	} else {
		if (has_space_override) {
			body_has_attached_area = false;
			soft_body->remove_area(area);
		}

		if (area->has_monitor_callback()) {
			area->remove_soft_body_from_query(soft_body, soft_body_shape, area_shape);
		}
	}

	return false; // Never do any post solving.
}

void OrblitAreaSoftBodyPair3D::solve(real_t p_step) {
	// Nothing to do.
}

OrblitAreaSoftBodyPair3D::OrblitAreaSoftBodyPair3D(OrblitSoftBody3D *p_soft_body, int p_soft_body_shape, OrblitArea3D *p_area, int p_area_shape) {
	soft_body = p_soft_body;
	area = p_area;
	soft_body_shape = p_soft_body_shape;
	area_shape = p_area_shape;
	soft_body->add_constraint(this);
	area->add_constraint(this);
}

OrblitAreaSoftBodyPair3D::~OrblitAreaSoftBodyPair3D() {
	if (colliding) {
		if (body_has_attached_area) {
			body_has_attached_area = false;
			soft_body->remove_area(area);
		}
		if (area->has_monitor_callback()) {
			area->remove_soft_body_from_query(soft_body, soft_body_shape, area_shape);
		}
	}
	soft_body->remove_constraint(this);
	area->remove_constraint(this);
}
