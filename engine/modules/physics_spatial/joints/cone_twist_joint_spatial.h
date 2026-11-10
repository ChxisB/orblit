/**************************************************************************/
/*  cone_twist_joint_spatial.h                                                 */

#pragma once

/*
Adapted to Orblit from the Bullet library.
*/

/*
Bullet Continuous Collision Detection and Physics Library
OrblitConeTwistJoint3D is Copyright (c) 2007 Starbreeze Studios

This software is provided 'as-is', without any express or implied warranty.
In no event will the authors be held liable for any damages arising from the use of this software.
Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it freely,
subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not claim that you wrote the original software. If you use this software in a product, an acknowledgment in the product documentation would be appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.

Written by: Marcus Hennix
*/

#include "../joint_spatial.h"
#include "jacobian_entry_spatial.h"

// OrblitConeTwistJoint3D can be used to simulate ragdoll joints (upper arm, leg etc).
class OrblitConeTwistJoint3D : public OrblitJoint3D {
#ifdef IN_PARALLELL_SOLVER
public:
#endif

	union {
		struct {
			OrblitBody3D *A;
			OrblitBody3D *B;
		};

		OrblitBody3D *_arr[2] = { nullptr, nullptr };
	};

	OrblitJacobianEntry3D m_jac[3] = {}; //3 orthogonal linear constraints

	real_t m_appliedImpulse = 0.0;
	Transform3D m_rbAFrame;
	Transform3D m_rbBFrame;

	real_t m_limitSoftness = 0.0;
	real_t m_biasFactor = 0.3;
	real_t m_relaxationFactor = 1.0;

	real_t m_swingSpan1 = Math::TAU / 8.0;
	real_t m_swingSpan2 = 0.0;
	real_t m_twistSpan = 0.0;

	Vector3 m_swingAxis;
	Vector3 m_twistAxis;

	real_t m_kSwing = 0.0;
	real_t m_kTwist = 0.0;

	real_t m_twistLimitSign = 0.0;
	real_t m_swingCorrection = 0.0;
	real_t m_twistCorrection = 0.0;

	real_t m_accSwingLimitImpulse = 0.0;
	real_t m_accTwistLimitImpulse = 0.0;

	bool m_angularOnly = false;
	bool m_solveTwistLimit = false;
	bool m_solveSwingLimit = false;

public:
	virtual PS3DE::JointType get_type() const override { return PS3DE::JOINT_TYPE_CONE_TWIST; }

	virtual bool setup(real_t p_step) override;
	virtual void solve(real_t p_step) override;

	OrblitConeTwistJoint3D(OrblitBody3D *rbA, OrblitBody3D *rbB, const Transform3D &rbAFrame, const Transform3D &rbBFrame);

	void setAngularOnly(bool angularOnly) {
		m_angularOnly = angularOnly;
	}

	void setLimit(real_t _swingSpan1, real_t _swingSpan2, real_t _twistSpan, real_t _softness = 0.8f, real_t _biasFactor = 0.3f, real_t _relaxationFactor = 1.0f) {
		m_swingSpan1 = _swingSpan1;
		m_swingSpan2 = _swingSpan2;
		m_twistSpan = _twistSpan;

		m_limitSoftness = _softness;
		m_biasFactor = _biasFactor;
		m_relaxationFactor = _relaxationFactor;
	}

	inline int getSolveTwistLimit() {
		return m_solveTwistLimit;
	}

	inline int getSolveSwingLimit() {
		return m_solveTwistLimit;
	}

	inline real_t getTwistLimitSign() {
		return m_twistLimitSign;
	}

	void set_param(PS3DE::ConeTwistJointParam p_param, real_t p_value);
	real_t get_param(PS3DE::ConeTwistJointParam p_param) const;
};
