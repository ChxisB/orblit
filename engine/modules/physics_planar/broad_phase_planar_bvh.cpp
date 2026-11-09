/**************************************************************************/
/*  broad_phase_planar_bvh.cpp                                                */

#include "broad_phase_planar_bvh.h"

#include "collision_object_planar.h"

OrblitBroadPhase2D::ID OrblitBroadPhase2DBVH::create(OrblitCollisionObject2D *p_object, int p_subindex, const Rect2 &p_aabb, bool p_static) {
	uint32_t tree_id = p_static ? TREE_STATIC : TREE_DYNAMIC;
	uint32_t tree_collision_mask = p_static ? TREE_FLAG_DYNAMIC : (TREE_FLAG_STATIC | TREE_FLAG_DYNAMIC);
	ID oid = bvh.create(p_object, true, tree_id, tree_collision_mask, p_aabb, p_subindex); // Pair everything, don't care?
	return oid + 1;
}

void OrblitBroadPhase2DBVH::move(ID p_id, const Rect2 &p_aabb) {
	ERR_FAIL_COND(!p_id);
	bvh.move(p_id - 1, p_aabb);
}

void OrblitBroadPhase2DBVH::set_static(ID p_id, bool p_static) {
	ERR_FAIL_COND(!p_id);
	uint32_t tree_id = p_static ? TREE_STATIC : TREE_DYNAMIC;
	uint32_t tree_collision_mask = p_static ? TREE_FLAG_DYNAMIC : (TREE_FLAG_STATIC | TREE_FLAG_DYNAMIC);
	bvh.set_tree(p_id - 1, tree_id, tree_collision_mask, false);
}

void OrblitBroadPhase2DBVH::remove(ID p_id) {
	ERR_FAIL_COND(!p_id);
	bvh.erase(p_id - 1);
}

OrblitCollisionObject2D *OrblitBroadPhase2DBVH::get_object(ID p_id) const {
	ERR_FAIL_COND_V(!p_id, nullptr);
	OrblitCollisionObject2D *it = bvh.get(p_id - 1);
	ERR_FAIL_NULL_V(it, nullptr);
	return it;
}

bool OrblitBroadPhase2DBVH::is_static(ID p_id) const {
	ERR_FAIL_COND_V(!p_id, false);
	uint32_t tree_id = bvh.get_tree_id(p_id - 1);
	return tree_id == 0;
}

int OrblitBroadPhase2DBVH::get_subindex(ID p_id) const {
	ERR_FAIL_COND_V(!p_id, 0);
	return bvh.get_subindex(p_id - 1);
}

int OrblitBroadPhase2DBVH::cull_segment(const Vector2 &p_from, const Vector2 &p_to, OrblitCollisionObject2D **p_results, int p_max_results, int *p_result_indices) {
	return bvh.cull_segment(p_from, p_to, p_results, p_max_results, nullptr, 0xFFFFFFFF, p_result_indices);
}

int OrblitBroadPhase2DBVH::cull_aabb(const Rect2 &p_aabb, OrblitCollisionObject2D **p_results, int p_max_results, int *p_result_indices) {
	return bvh.cull_aabb(p_aabb, p_results, p_max_results, nullptr, 0xFFFFFFFF, p_result_indices);
}

void *OrblitBroadPhase2DBVH::_pair_callback(void *self, uint32_t p_A, OrblitCollisionObject2D *p_object_A, int subindex_A, uint32_t p_B, OrblitCollisionObject2D *p_object_B, int subindex_B) {
	OrblitBroadPhase2DBVH *bpo = static_cast<OrblitBroadPhase2DBVH *>(self);
	if (!bpo->pair_callback) {
		return nullptr;
	}

	return bpo->pair_callback(p_object_A, subindex_A, p_object_B, subindex_B, bpo->pair_userdata);
}

void OrblitBroadPhase2DBVH::_unpair_callback(void *self, uint32_t p_A, OrblitCollisionObject2D *p_object_A, int subindex_A, uint32_t p_B, OrblitCollisionObject2D *p_object_B, int subindex_B, void *pairdata) {
	OrblitBroadPhase2DBVH *bpo = static_cast<OrblitBroadPhase2DBVH *>(self);
	if (!bpo->unpair_callback) {
		return;
	}

	bpo->unpair_callback(p_object_A, subindex_A, p_object_B, subindex_B, pairdata, bpo->unpair_userdata);
}

void OrblitBroadPhase2DBVH::set_pair_callback(PairCallback p_pair_callback, void *p_userdata) {
	pair_callback = p_pair_callback;
	pair_userdata = p_userdata;
}

void OrblitBroadPhase2DBVH::set_unpair_callback(UnpairCallback p_unpair_callback, void *p_userdata) {
	unpair_callback = p_unpair_callback;
	unpair_userdata = p_userdata;
}

void OrblitBroadPhase2DBVH::update() {
	bvh.update();
}

OrblitBroadPhase2D *OrblitBroadPhase2DBVH::_create() {
	return memnew(OrblitBroadPhase2DBVH);
}

OrblitBroadPhase2DBVH::OrblitBroadPhase2DBVH() {
	bvh.set_pair_callback(_pair_callback, this);
	bvh.set_unpair_callback(_unpair_callback, this);
}
