/**************************************************************************/
/*  constraint_spatial.h                                                       */

#pragma once

#include "core/containers/rid.h"
#include "core/typedefs.h"

class OrblitBody3D;
class OrblitSoftBody3D;

class OrblitConstraint3D {
	OrblitBody3D **_body_ptr;
	int _body_count;
	uint64_t island_step;
	int priority;
	bool disabled_collisions_between_bodies;

	RID self;

protected:
	OrblitConstraint3D(OrblitBody3D **p_body_ptr = nullptr, int p_body_count = 0) {
		_body_ptr = p_body_ptr;
		_body_count = p_body_count;
		island_step = 0;
		priority = 1;
		disabled_collisions_between_bodies = true;
	}

public:
	_FORCE_INLINE_ void set_self(const RID &p_self) { self = p_self; }
	_FORCE_INLINE_ RID get_self() const { return self; }

	_FORCE_INLINE_ uint64_t get_island_step() const { return island_step; }
	_FORCE_INLINE_ void set_island_step(uint64_t p_step) { island_step = p_step; }

	_FORCE_INLINE_ OrblitBody3D **get_body_ptr() const { return _body_ptr; }
	_FORCE_INLINE_ int get_body_count() const { return _body_count; }

	virtual OrblitSoftBody3D *get_soft_body_ptr(int p_index) const { return nullptr; }
	virtual int get_soft_body_count() const { return 0; }

	_FORCE_INLINE_ void set_priority(int p_priority) { priority = MAX(0, p_priority); }
	_FORCE_INLINE_ int get_priority() const { return priority; }

	_FORCE_INLINE_ void disable_collisions_between_bodies(const bool p_disabled) { disabled_collisions_between_bodies = p_disabled; }
	_FORCE_INLINE_ bool is_disabled_collisions_between_bodies() const { return disabled_collisions_between_bodies; }

	virtual bool setup(real_t p_step) = 0;
	virtual bool pre_solve(real_t p_step) = 0;
	virtual void solve(real_t p_step) = 0;

	virtual ~OrblitConstraint3D() {}
};
