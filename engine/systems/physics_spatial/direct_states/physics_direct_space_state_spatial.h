/**************************************************************************/
/*  physics_direct_space_state_spatial.h                                       */

#pragma once

#include "core/variant/type_info.h"
#include "systems/physics_spatial/physics_system_spatial_types.h"
#include "systems/physics_spatial/queries/physics_cast_motion_result_spatial.h"
#include "systems/physics_spatial/queries/physics_collide_shape_result_spatial.h"
#include "systems/physics_spatial/queries/physics_get_rest_info_result_spatial.h"
#include "systems/physics_spatial/queries/physics_intersect_point_result_spatial.h"
#include "systems/physics_spatial/queries/physics_intersect_ray_result_spatial.h"
#include "systems/physics_spatial/queries/physics_intersect_shape_result_spatial.h"
#include "systems/physics_spatial/queries/physics_point_query_parameters_spatial.h"
#include "systems/physics_spatial/queries/physics_ray_query_parameters_spatial.h"
#include "systems/physics_spatial/queries/physics_shape_query_parameters_spatial.h"

class PhysicsDirectSpaceState3D : public Object {
	ORBLIT_CLASS(PhysicsDirectSpaceState3D, Object);

private:
	Dictionary _intersect_ray(RequiredParam<PhysicsRayQueryParameters3D> p_ray_query);
	TypedArray<Dictionary> _intersect_point(RequiredParam<PhysicsPointQueryParameters3D> p_point_query, int p_max_results = 32);
	TypedArray<Dictionary> _intersect_shape(RequiredParam<PhysicsShapeQueryParameters3D> p_shape_query, int p_max_results = 32);
	Vector<real_t> _cast_motion(RequiredParam<PhysicsShapeQueryParameters3D> p_shape_query);
	TypedArray<Vector3> _collide_shape(RequiredParam<PhysicsShapeQueryParameters3D> p_shape_query, int p_max_results = 32);
	Dictionary _get_rest_info(RequiredParam<PhysicsShapeQueryParameters3D> p_shape_query);

	bool _intersect_ray_into(RequiredParam<PhysicsRayQueryParameters3D> p_ray_query, RequiredParam<PhysicsIntersectRayResult3D> p_result);
	bool _intersect_point_into(RequiredParam<PhysicsPointQueryParameters3D> p_point_query, RequiredParam<PhysicsIntersectPointResult3D> p_result);
	bool _intersect_shape_into(RequiredParam<PhysicsShapeQueryParameters3D> p_shape_query, RequiredParam<PhysicsIntersectShapeResult3D> p_result);
	bool _cast_motion_into(RequiredParam<PhysicsShapeQueryParameters3D> p_shape_query, RequiredParam<PhysicsCastMotionResult3D> p_result);
	bool _collide_shape_into(RequiredParam<PhysicsShapeQueryParameters3D> p_shape_query, RequiredParam<PhysicsCollideShapeResult3D> p_result);
	bool _get_rest_info_into(RequiredParam<PhysicsShapeQueryParameters3D> p_shape_query, RequiredParam<PhysicsGetRestInfoResult3D> p_result);

protected:
	static void register_members();

public:
	virtual bool intersect_ray(const PS3DT::RayParameters &p_parameters, PS3DT::RayResult &r_result) = 0;
	virtual int intersect_point(const PS3DT::PointParameters &p_parameters, PS3DT::ShapeResult *r_results, int p_result_max) = 0;
	virtual int intersect_shape(const PS3DT::ShapeParameters &p_parameters, PS3DT::ShapeResult *r_results, int p_result_max) = 0;
	virtual bool cast_motion(const PS3DT::ShapeParameters &p_parameters, real_t &p_closest_safe, real_t &p_closest_unsafe, PS3DT::ShapeRestInfo *r_info = nullptr) = 0;
	virtual bool collide_shape(const PS3DT::ShapeParameters &p_parameters, Vector3 *r_results, int p_result_max, int &r_result_count) = 0;
	virtual bool rest_info(const PS3DT::ShapeParameters &p_parameters, PS3DT::ShapeRestInfo *r_info) = 0;

	virtual Vector3 get_closest_point_to_object_volume(RID p_object, const Vector3 p_point) const = 0;

	PhysicsDirectSpaceState3D();
};
