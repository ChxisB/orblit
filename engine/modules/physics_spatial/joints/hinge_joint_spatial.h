/**************************************************************************/
/*  hinge_joint_spatial.h                                                      */

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

class OrblitHingeJoint3D : public OrblitJoint3D {
	union {
		struct {
			OrblitBody3D *A;
			OrblitBody3D *B;
		};

		OrblitBody3D *_arr[2] = {};
	};

	OrblitJacobianEntry3D m_jac[3]; //3 orthogonal linear constraints
	OrblitJacobianEntry3D m_jacAng[3]; //2 orthogonal angular constraints+ 1 for limit/motor

	Transform3D m_rbAFrame; // constraint axii. Assumes z is hinge axis.
	Transform3D m_rbBFrame;

	real_t m_motorTargetVelocity = 0.0;
	real_t m_maxMotorImpulse = 0.0;

	real_t m_limitSoftness = 0.9;
	real_t m_biasFactor = 0.3;
	real_t m_relaxationFactor = 1.0;

	real_t m_lowerLimit = Math::PI;
	real_t m_upperLimit = -Math::PI;

	real_t m_kHinge = 0.0;

	real_t m_limitSign = 0.0;
	real_t m_correction = 0.0;

	real_t m_accLimitImpulse = 0.0;

	real_t tau = 0.3;

	bool m_useLimit = false;
	bool m_angularOnly = false;
	bool m_enableAngularMotor = false;
	bool m_solveLimit = false;

	real_t m_appliedImpulse = 0.0;

public:
	virtual PS3DE::JointType get_type() const override { return PS3DE::JOINT_TYPE_HINGE; }

	virtual bool setup(real_t p_step) override;
	virtual void solve(real_t p_step) override;

	real_t get_hinge_angle();

	void set_param(PS3DE::HingeJointParam p_param, real_t p_value);
	real_t get_param(PS3DE::HingeJointParam p_param) const;

	void set_flag(PS3DE::HingeJointFlag p_flag, bool p_value);
	bool get_flag(PS3DE::HingeJointFlag p_flag) const;

	OrblitHingeJoint3D(OrblitBody3D *rbA, OrblitBody3D *rbB, const Transform3D &frameA, const Transform3D &frameB);
	OrblitHingeJoint3D(OrblitBody3D *rbA, OrblitBody3D *rbB, const Vector3 &pivotInA, const Vector3 &pivotInB, const Vector3 &axisInA, const Vector3 &axisInB);
};
