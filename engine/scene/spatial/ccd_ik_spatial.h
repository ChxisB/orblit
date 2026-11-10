/**************************************************************************/
/*  ccd_ik_spatial.h                                                           */

#pragma once

#include "scene/spatial/iterate_ik_spatial.h"

class CCDIK3D : public IterateIK3D {
	ORBLIT_CLASS(CCDIK3D, IterateIK3D);

protected:
	virtual void _solve_iteration(double p_delta, Skeleton3D *p_skeleton, IterateIK3DSetting *p_setting, const Vector3 &p_destination) override;
};
