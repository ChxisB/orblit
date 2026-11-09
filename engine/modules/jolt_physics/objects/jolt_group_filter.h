/**************************************************************************/
/*  jolt_group_filter.h                                                   */

#pragma once

#include <Jolt/Jolt.h>

#include <Jolt/Physics/Collision/CollisionGroup.h>
#include <Jolt/Physics/Collision/GroupFilter.h>

class JoltObject3D;

class JoltGroupFilter final : public JPH::GroupFilter {
	virtual bool CanCollide(const JPH::CollisionGroup &p_group1, const JPH::CollisionGroup &p_group2) const override;

public:
	inline static JoltGroupFilter *instance = nullptr;

	static void encode_object(const JoltObject3D *p_object, JPH::CollisionGroup::GroupID &r_group_id, JPH::CollisionGroup::SubGroupID &r_sub_group_id);
	static const JoltObject3D *decode_object(JPH::CollisionGroup::GroupID p_group_id, JPH::CollisionGroup::SubGroupID p_sub_group_id);
};
