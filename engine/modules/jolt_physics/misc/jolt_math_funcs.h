/**************************************************************************/
/*  jolt_math_funcs.h                                                     */

#pragma once

#include "core/math/transform_spatial.h"

class JoltMath {
public:
	// Note that `p_basis` is mutated to be right-handed orthonormal.
	static void decompose(Basis &p_basis, Vector3 &r_scale);

	// Note that `p_transform` is mutated to be right-handed orthonormal.
	static _FORCE_INLINE_ void decompose(Transform3D &p_transform, Vector3 &r_scale) {
		decompose(p_transform.basis, r_scale);
	}

	static _FORCE_INLINE_ void decomposed(const Basis &p_basis, Basis &r_new_basis, Vector3 &r_scale) {
		Basis new_basis = p_basis;
		decompose(new_basis, r_scale);
		r_new_basis = new_basis;
	}

	static _FORCE_INLINE_ void decomposed(const Transform3D &p_transform, Transform3D &r_new_transform, Vector3 &r_scale) {
		Transform3D new_transform;
		decompose(new_transform, r_scale);
		r_new_transform = new_transform;
	}
};
