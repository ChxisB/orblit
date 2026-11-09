/**************************************************************************/
/*  joint_spatial.h                                                            */

#pragma once

#include "body_spatial.h"
#include "constraint_spatial.h"

class OrblitJoint3D : public OrblitConstraint3D {
protected:
	bool dynamic_A = false;
	bool dynamic_B = false;

	void plane_space(const Vector3 &n, Vector3 &p, Vector3 &q) {
		if (Math::abs(n.z) > Math::SQRT12) {
			// choose p in y-z plane
			real_t a = n[1] * n[1] + n[2] * n[2];
			real_t k = 1.0 / Math::sqrt(a);
			p = Vector3(0, -n[2] * k, n[1] * k);
			// set q = n x p
			q = Vector3(a * k, -n[0] * p[2], n[0] * p[1]);
		} else {
			// choose p in x-y plane
			real_t a = n.x * n.x + n.y * n.y;
			real_t k = 1.0 / Math::sqrt(a);
			p = Vector3(-n.y * k, n.x * k, 0);
			// set q = n x p
			q = Vector3(-n.z * p.y, n.z * p.x, a * k);
		}
	}

	_FORCE_INLINE_ real_t atan2fast(real_t y, real_t x) {
		real_t coeff_1 = Math::PI / 4.0f;
		real_t coeff_2 = 3.0f * coeff_1;
		real_t abs_y = Math::abs(y);
		real_t angle;
		if (x >= 0.0f) {
			real_t r = (x - abs_y) / (x + abs_y);
			angle = coeff_1 - coeff_1 * r;
		} else {
			real_t r = (x + abs_y) / (abs_y - x);
			angle = coeff_2 - coeff_1 * r;
		}
		return (y < 0.0f) ? -angle : angle;
	}

public:
	virtual bool setup(real_t p_step) override { return false; }
	virtual bool pre_solve(real_t p_step) override { return true; }
	virtual void solve(real_t p_step) override {}

	void copy_settings_from(OrblitJoint3D *p_joint) {
		set_self(p_joint->get_self());
		set_priority(p_joint->get_priority());
		disable_collisions_between_bodies(p_joint->is_disabled_collisions_between_bodies());
	}

	virtual PS3DE::JointType get_type() const { return PS3DE::JOINT_TYPE_MAX; }
	_FORCE_INLINE_ OrblitJoint3D(OrblitBody3D **p_body_ptr = nullptr, int p_body_count = 0) :
			OrblitConstraint3D(p_body_ptr, p_body_count) {
	}

	virtual ~OrblitJoint3D() {
		for (int i = 0; i < get_body_count(); i++) {
			OrblitBody3D *body = get_body_ptr()[i];
			if (body) {
				body->remove_constraint(this);
			}
		}
	}
};
