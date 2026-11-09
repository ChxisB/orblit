/**************************************************************************/
/*  jolt_group_filter.cpp                                                 */

#include "jolt_group_filter.h"

#include "jolt_object_spatial.h"

bool JoltGroupFilter::CanCollide(const JPH::CollisionGroup &p_group1, const JPH::CollisionGroup &p_group2) const {
	const JoltObject3D *object1 = decode_object(p_group1.GetGroupID(), p_group1.GetSubGroupID());
	const JoltObject3D *object2 = decode_object(p_group2.GetGroupID(), p_group2.GetSubGroupID());
	return object1->can_interact_with(*object2);
}

void JoltGroupFilter::encode_object(const JoltObject3D *p_object, JPH::CollisionGroup::GroupID &r_group_id, JPH::CollisionGroup::SubGroupID &r_sub_group_id) {
	// Since group filters don't grant us access to the bodies or their user data we are instead forced use the
	// collision group to carry the upper and lower bits of our pointer, which we can access and decode in `CanCollide`.
	const uint64_t address = reinterpret_cast<uint64_t>(p_object);
	r_group_id = JPH::CollisionGroup::GroupID(address >> 32U);
	r_sub_group_id = JPH::CollisionGroup::SubGroupID(address & 0xFFFFFFFFULL);
}

const JoltObject3D *JoltGroupFilter::decode_object(JPH::CollisionGroup::GroupID p_group_id, JPH::CollisionGroup::SubGroupID p_sub_group_id) {
	const uint64_t upper_bits = (uint64_t)p_group_id << 32U;
	const uint64_t lower_bits = (uint64_t)p_sub_group_id;
	const uint64_t address = uint64_t(upper_bits | lower_bits);
	return reinterpret_cast<const JoltObject3D *>(address);
}

static_assert(sizeof(JoltObject3D *) <= 8, "Pointer size greater than expected.");
static_assert(sizeof(JPH::CollisionGroup::GroupID) == 4, "Size of Jolt's collision group ID has changed.");
static_assert(sizeof(JPH::CollisionGroup::SubGroupID) == 4, "Size of Jolt's collision sub-group ID has changed.");
