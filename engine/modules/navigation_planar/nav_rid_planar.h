/**************************************************************************/
/*  nav_rid_planar.h                                                          */

#pragma once

#include "core/containers/rid.h"

class NavRid2D {
	RID self;

public:
	_FORCE_INLINE_ void set_self(const RID &p_self) { self = p_self; }
	_FORCE_INLINE_ RID get_self() const { return self; }
};
