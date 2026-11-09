/**************************************************************************/
/*  broad_phase_spatial.h                                                      */

#pragma once

#include "core/math/aabb.h"

class OrblitCollisionObject3D;

class OrblitBroadPhase3D {
public:
	typedef OrblitBroadPhase3D *(*CreateFunction)();

	static inline CreateFunction create_func = nullptr;

	typedef uint32_t ID;

	typedef void *(*PairCallback)(OrblitCollisionObject3D *A, int p_subindex_A, OrblitCollisionObject3D *B, int p_subindex_B, void *p_userdata);
	typedef void (*UnpairCallback)(OrblitCollisionObject3D *A, int p_subindex_A, OrblitCollisionObject3D *B, int p_subindex_B, void *p_data, void *p_userdata);

	// 0 is an invalid ID
	virtual ID create(OrblitCollisionObject3D *p_object_, int p_subindex = 0, const AABB &p_aabb = AABB(), bool p_static = false) = 0;
	virtual void move(ID p_id, const AABB &p_aabb) = 0;
	virtual void set_static(ID p_id, bool p_static) = 0;
	virtual void remove(ID p_id) = 0;

	virtual OrblitCollisionObject3D *get_object(ID p_id) const = 0;
	virtual bool is_static(ID p_id) const = 0;
	virtual int get_subindex(ID p_id) const = 0;

	virtual int cull_point(const Vector3 &p_point, OrblitCollisionObject3D **p_results, int p_max_results, int *p_result_indices = nullptr) = 0;
	virtual int cull_segment(const Vector3 &p_from, const Vector3 &p_to, OrblitCollisionObject3D **p_results, int p_max_results, int *p_result_indices = nullptr) = 0;
	virtual int cull_aabb(const AABB &p_aabb, OrblitCollisionObject3D **p_results, int p_max_results, int *p_result_indices = nullptr) = 0;

	virtual void set_pair_callback(PairCallback p_pair_callback, void *p_userdata) = 0;
	virtual void set_unpair_callback(UnpairCallback p_unpair_callback, void *p_userdata) = 0;

	virtual void update() = 0;

	virtual ~OrblitBroadPhase3D() {}
};
