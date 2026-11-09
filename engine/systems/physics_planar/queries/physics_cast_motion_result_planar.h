/**************************************************************************/
/*  physics_cast_motion_result_planar.h                                       */

#pragma once

#include "core/object/ref_counted.h"

class PhysicsCastMotionResult2D : public RefCounted {
	ORBLIT_CLASS(PhysicsCastMotionResult2D, RefCounted);

	friend class PhysicsDirectSpaceState2D;

	real_t safe_fraction = 1.0;
	real_t unsafe_fraction = 1.0;

protected:
	static void register_members();

public:
	real_t get_safe_fraction() const;
	real_t get_unsafe_fraction() const;
};
