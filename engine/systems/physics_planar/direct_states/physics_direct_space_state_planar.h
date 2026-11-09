/**************************************************************************/
/*  physics_direct_space_state_planar.h                                       */

#pragma once

#include "core/variant/type_info.h"
#include "systems/physics_planar/physics_system_planar_types.h"
#include "systems/physics_planar/queries/physics_cast_motion_result_planar.h"
#include "systems/physics_planar/queries/physics_collide_shape_result_planar.h"
#include "systems/physics_planar/queries/physics_get_rest_info_result_planar.h"
#include "systems/physics_planar/queries/physics_intersect_point_result_planar.h"
#include "systems/physics_planar/queries/physics_intersect_ray_result_planar.h"
#include "systems/physics_planar/queries/physics_intersect_shape_result_planar.h"
#include "systems/physics_planar/queries/physics_point_query_parameters_planar.h"
#include "systems/physics_planar/queries/physics_ray_query_parameters_planar.h"
#include "systems/physics_planar/queries/physics_shape_query_parameters_planar.h"

class PhysicsDirectSpaceState2D : public Object {
	ORBLIT_CLASS(PhysicsDirectSpaceState2D, Object);

	Dictionary _intersect_ray(RequiredParam<PhysicsRayQueryParameters2D> p_ray_query);
	TypedArray<Dictionary> _intersect_point(RequiredParam<PhysicsPointQueryParameters2D> p_point_query, int p_max_results = 32);
	TypedArray<Dictionary> _intersect_shape(RequiredParam<PhysicsShapeQueryParameters2D> p_shape_query, int p_max_results = 32);
	Vector<real_t> _cast_motion(RequiredParam<PhysicsShapeQueryParameters2D> p_shape_query);
	TypedArray<Vector2> _collide_shape(RequiredParam<PhysicsShapeQueryParameters2D> p_shape_query, int p_max_results = 32);
	Dictionary _get_rest_info(RequiredParam<PhysicsShapeQueryParameters2D> p_shape_query);

	bool _intersect_ray_into(RequiredParam<PhysicsRayQueryParameters2D> p_ray_query, RequiredParam<PhysicsIntersectRayResult2D> p_result);
	bool _intersect_point_into(RequiredParam<PhysicsPointQueryParameters2D> p_point_query, RequiredParam<PhysicsIntersectPointResult2D> p_result);
	bool _intersect_shape_into(RequiredParam<PhysicsShapeQueryParameters2D> p_shape_query, RequiredParam<PhysicsIntersectShapeResult2D> p_result);
	bool _cast_motion_into(RequiredParam<PhysicsShapeQueryParameters2D> p_shape_query, RequiredParam<PhysicsCastMotionResult2D> p_result);
	bool _collide_shape_into(RequiredParam<PhysicsShapeQueryParameters2D> p_shape_query, RequiredParam<PhysicsCollideShapeResult2D> p_result);
	bool _get_rest_info_into(RequiredParam<PhysicsShapeQueryParameters2D> p_shape_query, RequiredParam<PhysicsGetRestInfoResult2D> p_result);

protected:
	static void register_members();

public:
	virtual bool intersect_ray(const PS2DT::RayParameters &p_parameters, PS2DT::RayResult &r_result) = 0;
	virtual int intersect_point(const PS2DT::PointParameters &p_parameters, PS2DT::ShapeResult *r_results, int p_result_max) = 0;
	virtual int intersect_shape(const PS2DT::ShapeParameters &p_parameters, PS2DT::ShapeResult *r_results, int p_result_max) = 0;
	virtual bool cast_motion(const PS2DT::ShapeParameters &p_parameters, real_t &p_closest_safe, real_t &p_closest_unsafe) = 0;
	virtual bool collide_shape(const PS2DT::ShapeParameters &p_parameters, Vector2 *r_results, int p_result_max, int &r_result_count) = 0;
	virtual bool rest_info(const PS2DT::ShapeParameters &p_parameters, PS2DT::ShapeRestInfo *r_info) = 0;

	PhysicsDirectSpaceState2D();
};
