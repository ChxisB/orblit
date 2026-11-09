/**************************************************************************/
/*  physics_material.h                                                    */

#pragma once

#if !defined(PHYSICS_PLANAR_DISABLED) || !defined(PHYSICS_SPATIAL_DISABLED)
#include "core/io/resource.h"

class PhysicsMaterial : public Resource {
	ORBLIT_CLASS(PhysicsMaterial, Resource);
	OBJ_SAVE_TYPE(PhysicsMaterial);
	RES_BASE_EXTENSION("phymat");

	real_t friction = 1.0;
	bool rough = false;
	real_t bounce = 0.0;
	bool absorbent = false;

protected:
	static void register_members();

public:
	void set_friction(real_t p_val);
	_FORCE_INLINE_ real_t get_friction() const { return friction; }

	void set_rough(bool p_val);
	_FORCE_INLINE_ bool is_rough() const { return rough; }

	_FORCE_INLINE_ real_t computed_friction() const {
		return rough ? -friction : friction;
	}

	void set_bounce(real_t p_val);
	_FORCE_INLINE_ real_t get_bounce() const { return bounce; }

	void set_absorbent(bool p_val);
	_FORCE_INLINE_ bool is_absorbent() const { return absorbent; }

	_FORCE_INLINE_ real_t computed_bounce() const {
		return absorbent ? -bounce : bounce;
	}
};
#endif // !defined(PHYSICS_PLANAR_DISABLED) || !defined(PHYSICS_SPATIAL_DISABLED)
