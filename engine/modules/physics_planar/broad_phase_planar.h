/**************************************************************************/
/*  broad_phase_planar.h                                                      */

#pragma once

#include "core/math/rect2.h"

class OrblitCollisionObject2D;

class OrblitBroadPhase2D {
public:
	typedef OrblitBroadPhase2D *(*CreateFunction)();

	static inline CreateFunction create_func = nullptr;

	typedef uint32_t ID;

	typedef void *(*PairCallback)(OrblitCollisionObject2D *A, int p_subindex_A, OrblitCollisionObject2D *B, int p_subindex_B, void *p_userdata);
	typedef void (*UnpairCallback)(OrblitCollisionObject2D *A, int p_subindex_A, OrblitCollisionObject2D *B, int p_subindex_B, void *p_data, void *p_userdata);

	// 0 is an invalid ID
	virtual ID create(OrblitCollisionObject2D *p_object_, int p_subindex = 0, const Rect2 &p_aabb = Rect2(), bool p_static = false) = 0;
	virtual void move(ID p_id, const Rect2 &p_aabb) = 0;
	virtual void set_static(ID p_id, bool p_static) = 0;
	virtual void remove(ID p_id) = 0;

	virtual OrblitCollisionObject2D *get_object(ID p_id) const = 0;
	virtual bool is_static(ID p_id) const = 0;
	virtual int get_subindex(ID p_id) const = 0;

	virtual int cull_segment(const Vector2 &p_from, const Vector2 &p_to, OrblitCollisionObject2D **p_results, int p_max_results, int *p_result_indices = nullptr) = 0;
	virtual int cull_aabb(const Rect2 &p_aabb, OrblitCollisionObject2D **p_results, int p_max_results, int *p_result_indices = nullptr) = 0;

	virtual void set_pair_callback(PairCallback p_pair_callback, void *p_userdata) = 0;
	virtual void set_unpair_callback(UnpairCallback p_unpair_callback, void *p_userdata) = 0;

	virtual void update() = 0;

	virtual ~OrblitBroadPhase2D() {}
};
