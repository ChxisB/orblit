/**************************************************************************/
/*  body_pair_spatial.h                                                        */

#pragma once

#include "body_spatial.h"
#include "constraint_spatial.h"
#include "soft_body_spatial.h"

#include "core/containers/local_vector.h"

class OrblitBodyContact3D : public OrblitConstraint3D {
protected:
	struct Contact {
		Vector3 position;
		Vector3 normal;
		int index_A = 0, index_B = 0;
		Vector3 local_A, local_B;
		Vector3 acc_impulse; // accumulated impulse - only one of the object's impulse is needed as impulse_a == -impulse_b
		real_t acc_normal_impulse = 0.0; // accumulated normal impulse (Pn)
		Vector3 acc_tangent_impulse; // accumulated tangent impulse (Pt)
		real_t acc_bias_impulse = 0.0; // accumulated normal impulse for position bias (Pnb)
		real_t acc_bias_impulse_center_of_mass = 0.0; // accumulated normal impulse for position bias applied to com
		real_t mass_normal = 0.0;
		real_t bias = 0.0;
		real_t bounce = 0.0;

		real_t depth = 0.0;
		bool active = false;
		bool used = false;
		Vector3 rA, rB; // Offset in world orientation with respect to center of mass
	};

	Vector3 sep_axis;
	bool collided = false;
	bool check_ccd = false;

	OrblitSpace3D *space = nullptr;

	OrblitBodyContact3D(OrblitBody3D **p_body_ptr = nullptr, int p_body_count = 0) :
			OrblitConstraint3D(p_body_ptr, p_body_count) {
	}
};

class OrblitBodyPair3D : public OrblitBodyContact3D {
	enum {
		MAX_CONTACTS = 4
	};

	union {
		struct {
			OrblitBody3D *A;
			OrblitBody3D *B;
		};

		OrblitBody3D *_arr[2] = { nullptr, nullptr };
	};

	int shape_A = 0;
	int shape_B = 0;

	bool collide_A = false;
	bool collide_B = false;

	bool report_contacts_only = false;

	Vector3 offset_B; //use local A coordinates to avoid numerical issues on collision detection

	Contact contacts[MAX_CONTACTS];
	int contact_count = 0;

	static void _contact_added_callback(const Vector3 &p_point_A, int p_index_A, const Vector3 &p_point_B, int p_index_B, const Vector3 &normal, void *p_userdata);

	void contact_added_callback(const Vector3 &p_point_A, int p_index_A, const Vector3 &p_point_B, int p_index_B, const Vector3 &normal);

	void validate_contacts();
	bool _test_ccd(real_t p_step, OrblitBody3D *p_A, int p_shape_A, const Transform3D &p_xform_A, OrblitBody3D *p_B, int p_shape_B, const Transform3D &p_xform_B);

public:
	virtual bool setup(real_t p_step) override;
	virtual bool pre_solve(real_t p_step) override;
	virtual void solve(real_t p_step) override;

	OrblitBodyPair3D(OrblitBody3D *p_A, int p_shape_A, OrblitBody3D *p_B, int p_shape_B);
	~OrblitBodyPair3D();
};

class OrblitBodySoftBodyPair3D : public OrblitBodyContact3D {
	OrblitBody3D *body = nullptr;
	OrblitSoftBody3D *soft_body = nullptr;

	int body_shape = 0;

	bool body_collides = false;
	bool soft_body_collides = false;

	bool report_contacts_only = false;

	LocalVector<Contact> contacts;

	static void _contact_added_callback(const Vector3 &p_point_A, int p_index_A, const Vector3 &p_point_B, int p_index_B, const Vector3 &normal, void *p_userdata);

	void contact_added_callback(const Vector3 &p_point_A, int p_index_A, const Vector3 &p_point_B, int p_index_B, const Vector3 &normal);

	void validate_contacts();

public:
	virtual bool setup(real_t p_step) override;
	virtual bool pre_solve(real_t p_step) override;
	virtual void solve(real_t p_step) override;

	virtual OrblitSoftBody3D *get_soft_body_ptr(int p_index) const override { return soft_body; }
	virtual int get_soft_body_count() const override { return 1; }

	OrblitBodySoftBodyPair3D(OrblitBody3D *p_A, int p_shape_A, OrblitSoftBody3D *p_B);
	~OrblitBodySoftBodyPair3D();
};
