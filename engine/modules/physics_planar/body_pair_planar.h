/**************************************************************************/
/*  body_pair_planar.h                                                        */

#pragma once

#include "body_planar.h"
#include "constraint_planar.h"

class OrblitBodyPair2D : public OrblitConstraint2D {
	enum {
		MAX_CONTACTS = 2
	};
	union {
		struct {
			OrblitBody2D *A;
			OrblitBody2D *B;
		};

		OrblitBody2D *_arr[2] = { nullptr, nullptr };
	};

	int shape_A = 0;
	int shape_B = 0;

	bool collide_A = false;
	bool collide_B = false;

	OrblitSpace2D *space = nullptr;

	struct Contact {
		Vector2 position;
		Vector2 normal;
		Vector2 local_A, local_B;
		Vector2 acc_impulse; // accumulated impulse
		real_t acc_normal_impulse = 0.0; // accumulated normal impulse (Pn)
		real_t acc_tangent_impulse = 0.0; // accumulated tangent impulse (Pt)
		real_t acc_bias_impulse = 0.0; // accumulated normal impulse for position bias (Pnb)
		real_t acc_bias_impulse_center_of_mass = 0.0; // accumulated normal impulse for position bias applied to com
		real_t mass_normal, mass_tangent = 0.0;
		real_t bias = 0.0;

		real_t depth = 0.0;
		bool active = false;
		bool used = false;
		Vector2 rA, rB;
		real_t bounce = 0.0;
	};

	Vector2 offset_B; //use local A coordinates to avoid numerical issues on collision detection

	Vector2 sep_axis;
	Contact contacts[MAX_CONTACTS];
	int contact_count = 0;
	bool collided = false;
	bool check_ccd = false;
	bool oneway_disabled = false;
	bool report_contacts_only = false;

	bool _test_ccd(real_t p_step, OrblitBody2D *p_A, int p_shape_A, const Transform2D &p_xform_A, OrblitBody2D *p_B, int p_shape_B, const Transform2D &p_xform_B);
	void _validate_contacts();
	static void _add_contact(const Vector2 &p_point_A, const Vector2 &p_point_B, void *p_self);
	_FORCE_INLINE_ void _contact_added_callback(const Vector2 &p_point_A, const Vector2 &p_point_B);

public:
	virtual bool setup(real_t p_step) override;
	virtual bool pre_solve(real_t p_step) override;
	virtual void solve(real_t p_step) override;

	OrblitBodyPair2D(OrblitBody2D *p_A, int p_shape_A, OrblitBody2D *p_B, int p_shape_B);
	~OrblitBodyPair2D();
};
