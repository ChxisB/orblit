/**************************************************************************/
/*  pin_joint_spatial.h                                                        */

#pragma once

/*
Adapted to Orblit from the Bullet library.
*/

#include "../joint_spatial.h"
#include "jacobian_entry_spatial.h"

/*
Bullet Continuous Collision Detection and Physics Library
Copyright (c) 2003-2006 Erwin Coumans  http://continuousphysics.com/Bullet/

This software is provided 'as-is', without any express or implied warranty.
In no event will the authors be held liable for any damages arising from the use of this software.
Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it freely,
subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not claim that you wrote the original software. If you use this software in a product, an acknowledgment in the product documentation would be appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.
*/

class OrblitPinJoint3D : public OrblitJoint3D {
	union {
		struct {
			OrblitBody3D *A;
			OrblitBody3D *B;
		};

		OrblitBody3D *_arr[2] = {};
	};

	real_t m_tau = 0.3; //bias
	real_t m_damping = 1.0;
	real_t m_impulseClamp = 0.0;
	real_t m_appliedImpulse = 0.0;

	OrblitJacobianEntry3D m_jac[3] = {}; //3 orthogonal linear constraints

	Vector3 m_pivotInA;
	Vector3 m_pivotInB;

public:
	virtual PS3DE::JointType get_type() const override { return PS3DE::JOINT_TYPE_PIN; }

	virtual bool setup(real_t p_step) override;
	virtual void solve(real_t p_step) override;

	void set_param(PS3DE::PinJointParam p_param, real_t p_value);
	real_t get_param(PS3DE::PinJointParam p_param) const;

	void set_pos_a(const Vector3 &p_pos) { m_pivotInA = p_pos; }
	void set_pos_b(const Vector3 &p_pos) { m_pivotInB = p_pos; }

	Vector3 get_position_a() { return m_pivotInA; }
	Vector3 get_position_b() { return m_pivotInB; }

	OrblitPinJoint3D(OrblitBody3D *p_body_a, const Vector3 &p_pos_a, OrblitBody3D *p_body_b, const Vector3 &p_pos_b);
	~OrblitPinJoint3D();
};
