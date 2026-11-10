/**************************************************************************/
/*  broad_phase_spatial_bvh.h                                                  */

#pragma once

#include "broad_phase_spatial.h"

#include "core/math/bvh.h"

class OrblitBroadPhase3DBVH : public OrblitBroadPhase3D {
	template <typename T>
	class UserPairTestFunction {
	public:
		static bool user_pair_check(const T *p_a, const T *p_b) {
			// return false if no collision, decided by masks etc
			return p_a->interacts_with(p_b);
		}
	};

	template <typename T>
	class UserCullTestFunction {
	public:
		static bool user_cull_check(const T *p_a, const T *p_b) {
			return true;
		}
	};

	enum Tree {
		TREE_STATIC = 0,
		TREE_DYNAMIC = 1,
	};

	enum TreeFlag {
		TREE_FLAG_STATIC = 1 << TREE_STATIC,
		TREE_FLAG_DYNAMIC = 1 << TREE_DYNAMIC,
	};

	BVH_Manager<OrblitCollisionObject3D, 2, true, 128, UserPairTestFunction<OrblitCollisionObject3D>, UserCullTestFunction<OrblitCollisionObject3D>> bvh;

	static void *_pair_callback(void *, uint32_t, OrblitCollisionObject3D *, int, uint32_t, OrblitCollisionObject3D *, int);
	static void _unpair_callback(void *, uint32_t, OrblitCollisionObject3D *, int, uint32_t, OrblitCollisionObject3D *, int, void *);

	PairCallback pair_callback = nullptr;
	void *pair_userdata = nullptr;
	UnpairCallback unpair_callback = nullptr;
	void *unpair_userdata = nullptr;

public:
	// 0 is an invalid ID
	virtual ID create(OrblitCollisionObject3D *p_object, int p_subindex = 0, const AABB &p_aabb = AABB(), bool p_static = false) override;
	virtual void move(ID p_id, const AABB &p_aabb) override;
	virtual void set_static(ID p_id, bool p_static) override;
	virtual void remove(ID p_id) override;

	virtual OrblitCollisionObject3D *get_object(ID p_id) const override;
	virtual bool is_static(ID p_id) const override;
	virtual int get_subindex(ID p_id) const override;

	virtual int cull_point(const Vector3 &p_point, OrblitCollisionObject3D **p_results, int p_max_results, int *p_result_indices = nullptr) override;
	virtual int cull_segment(const Vector3 &p_from, const Vector3 &p_to, OrblitCollisionObject3D **p_results, int p_max_results, int *p_result_indices = nullptr) override;
	virtual int cull_aabb(const AABB &p_aabb, OrblitCollisionObject3D **p_results, int p_max_results, int *p_result_indices = nullptr) override;

	virtual void set_pair_callback(PairCallback p_pair_callback, void *p_userdata) override;
	virtual void set_unpair_callback(UnpairCallback p_unpair_callback, void *p_userdata) override;

	virtual void update() override;

	static OrblitBroadPhase3D *_create();
	OrblitBroadPhase3DBVH();
};
