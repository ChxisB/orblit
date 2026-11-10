/**************************************************************************/
/*  joints_planar.h                                                           */

#pragma once

#include "body_planar.h"
#include "constraint_planar.h"

class OrblitJoint2D : public OrblitConstraint2D {
	real_t bias = 0;
	real_t max_bias = 3.40282e+38;
	real_t max_force = 3.40282e+38;

protected:
	bool dynamic_A = false;
	bool dynamic_B = false;

public:
	_FORCE_INLINE_ void set_max_force(real_t p_force) { max_force = p_force; }
	_FORCE_INLINE_ real_t get_max_force() const { return max_force; }

	_FORCE_INLINE_ void set_bias(real_t p_bias) { bias = p_bias; }
	_FORCE_INLINE_ real_t get_bias() const { return bias; }

	_FORCE_INLINE_ void set_max_bias(real_t p_bias) { max_bias = p_bias; }
	_FORCE_INLINE_ real_t get_max_bias() const { return max_bias; }

	virtual bool setup(real_t p_step) override { return false; }
	virtual bool pre_solve(real_t p_step) override { return false; }
	virtual void solve(real_t p_step) override {}

	void copy_settings_from(OrblitJoint2D *p_joint);

	virtual PS2DE::JointType get_type() const { return PS2DE::JOINT_TYPE_MAX; }
	OrblitJoint2D(OrblitBody2D **p_body_ptr = nullptr, int p_body_count = 0) :
			OrblitConstraint2D(p_body_ptr, p_body_count) {}

	virtual ~OrblitJoint2D() {
		for (int i = 0; i < get_body_count(); i++) {
			OrblitBody2D *body = get_body_ptr()[i];
			if (body) {
				body->remove_constraint(this, i);
			}
		}
	}
};

class OrblitPinJoint2D : public OrblitJoint2D {
	union {
		struct {
			OrblitBody2D *A;
			OrblitBody2D *B;
		};

		OrblitBody2D *_arr[2] = { nullptr, nullptr };
	};

	Transform2D M;
	Vector2 rA, rB;
	Vector2 anchor_A;
	Vector2 anchor_B;
	Vector2 bias;
	real_t initial_angle = 0.0;
	real_t bias_velocity = 0.0;
	real_t jn_max = 0.0;
	real_t j_acc = 0.0;
	real_t i_sum = 0.0;
	Vector2 P;
	real_t softness = 0.0;
	real_t angular_limit_lower = 0.0;
	real_t angular_limit_upper = 0.0;
	real_t motor_target_velocity = 0.0;
	bool is_joint_at_limit = false;
	bool motor_enabled = false;
	bool angular_limit_enabled = false;

public:
	virtual PS2DE::JointType get_type() const override { return PS2DE::JOINT_TYPE_PIN; }

	virtual bool setup(real_t p_step) override;
	virtual bool pre_solve(real_t p_step) override;
	virtual void solve(real_t p_step) override;

	void set_param(PS2DE::PinJointParam p_param, real_t p_value);
	real_t get_param(PS2DE::PinJointParam p_param) const;

	void set_flag(PS2DE::PinJointFlag p_flag, bool p_enabled);
	bool get_flag(PS2DE::PinJointFlag p_flag) const;

	OrblitPinJoint2D(const Vector2 &p_pos, OrblitBody2D *p_body_a, OrblitBody2D *p_body_b = nullptr);
};

class OrblitGrooveJoint2D : public OrblitJoint2D {
	union {
		struct {
			OrblitBody2D *A;
			OrblitBody2D *B;
		};

		OrblitBody2D *_arr[2] = { nullptr, nullptr };
	};

	Vector2 A_groove_1;
	Vector2 A_groove_2;
	Vector2 A_groove_normal;
	Vector2 B_anchor;
	Vector2 jn_acc;
	Vector2 gbias;
	real_t jn_max = 0.0;
	real_t clamp = 0.0;
	Vector2 xf_normal;
	Vector2 rA, rB;
	Vector2 k1, k2;

	bool correct = false;

public:
	virtual PS2DE::JointType get_type() const override { return PS2DE::JOINT_TYPE_GROOVE; }

	virtual bool setup(real_t p_step) override;
	virtual bool pre_solve(real_t p_step) override;
	virtual void solve(real_t p_step) override;

	OrblitGrooveJoint2D(const Vector2 &p_a_groove1, const Vector2 &p_a_groove2, const Vector2 &p_b_anchor, OrblitBody2D *p_body_a, OrblitBody2D *p_body_b);
};

class OrblitDampedSpringJoint2D : public OrblitJoint2D {
	union {
		struct {
			OrblitBody2D *A;
			OrblitBody2D *B;
		};

		OrblitBody2D *_arr[2] = { nullptr, nullptr };
	};

	Vector2 anchor_A;
	Vector2 anchor_B;

	real_t rest_length = 0.0;
	real_t damping = 1.5;
	real_t stiffness = 20.0;

	Vector2 rA, rB;
	Vector2 n;
	Vector2 j;
	real_t n_mass = 0.0;
	real_t target_vrn = 0.0;
	real_t v_coef = 0.0;

public:
	virtual PS2DE::JointType get_type() const override { return PS2DE::JOINT_TYPE_DAMPED_SPRING; }

	virtual bool setup(real_t p_step) override;
	virtual bool pre_solve(real_t p_step) override;
	virtual void solve(real_t p_step) override;

	void set_param(PS2DE::DampedSpringParam p_param, real_t p_value);
	real_t get_param(PS2DE::DampedSpringParam p_param) const;

	OrblitDampedSpringJoint2D(const Vector2 &p_anchor_a, const Vector2 &p_anchor_b, OrblitBody2D *p_body_a, OrblitBody2D *p_body_b);
};
