/**************************************************************************/
/*  jolt_motion_filter_spatial.h                                               */

#pragma once

#include "core/object/object_id.h"
#include "core/containers/hash_set.h"
#include "core/containers/rid.h"

#include <Jolt/Jolt.h>

#include <Jolt/Physics/Body/Body.h>
#include <Jolt/Physics/Body/BodyFilter.h>
#include <Jolt/Physics/Collision/BroadPhase/BroadPhaseLayer.h>
#include <Jolt/Physics/Collision/ObjectLayer.h>
#include <Jolt/Physics/Collision/ShapeFilter.h>

class JoltBody3D;
class JoltPhysicsSystem3D;
class JoltSpace3D;

class JoltMotionFilter3D final
		: public JPH::BroadPhaseLayerFilter,
		  public JPH::ObjectLayerFilter,
		  public JPH::BodyFilter,
		  public JPH::ShapeFilter {
	const JoltBody3D &body_self;
	const JoltSpace3D &space;
	const HashSet<RID> &excluded_bodies;
	const HashSet<ObjectID> &excluded_objects;
	bool collide_separation_ray = false;

public:
	explicit JoltMotionFilter3D(const JoltBody3D &p_body, const HashSet<RID> &p_excluded_bodies, const HashSet<ObjectID> &p_excluded_objects, bool p_collide_separation_ray = true);

	virtual bool ShouldCollide(JPH::BroadPhaseLayer p_broad_phase_layer) const override;
	virtual bool ShouldCollide(JPH::ObjectLayer p_object_layer) const override;
	virtual bool ShouldCollide(const JPH::BodyID &p_jolt_id) const override;
	virtual bool ShouldCollideLocked(const JPH::Body &p_jolt_body) const override;
	virtual bool ShouldCollide(const JPH::Shape *p_jolt_shape, const JPH::SubShapeID &p_jolt_shape_id) const override;
	virtual bool ShouldCollide(const JPH::Shape *p_jolt_shape_self, const JPH::SubShapeID &p_jolt_shape_id_self, const JPH::Shape *p_jolt_shape_other, const JPH::SubShapeID &p_jolt_shape_id_other) const override;
};
