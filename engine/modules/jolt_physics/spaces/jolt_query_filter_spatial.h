/**************************************************************************/
/*  jolt_query_filter_spatial.h                                                */

#pragma once

#include "core/containers/hash_set.h"
#include "core/containers/rid.h"

#include <Jolt/Jolt.h>

#include <Jolt/Physics/Body/Body.h>
#include <Jolt/Physics/Body/BodyFilter.h>
#include <Jolt/Physics/Collision/BroadPhase/BroadPhaseLayer.h>
#include <Jolt/Physics/Collision/ObjectLayer.h>

class JoltPhysicsDirectSpaceState3D;
class JoltSpace3D;

class JoltQueryFilter3D final
		: public JPH::BroadPhaseLayerFilter,
		  public JPH::ObjectLayerFilter,
		  public JPH::BodyFilter {
	const JoltSpace3D &space;
	const HashSet<RID> &excluded;
	uint32_t collision_mask = 0;
	bool collide_with_bodies = false;
	bool collide_with_areas = false;
	bool picking = false;

public:
	JoltQueryFilter3D(const JoltPhysicsDirectSpaceState3D &p_space_state, uint32_t p_collision_mask, bool p_collide_with_bodies, bool p_collide_with_areas, const HashSet<RID> &p_excluded, bool p_picking = false);

	virtual bool ShouldCollide(JPH::BroadPhaseLayer p_broad_phase_layer) const override;
	virtual bool ShouldCollide(JPH::ObjectLayer p_object_layer) const override;
	virtual bool ShouldCollide(const JPH::BodyID &p_body_id) const override;
	virtual bool ShouldCollideLocked(const JPH::Body &p_body) const override;
};
