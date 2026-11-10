/**************************************************************************/
/*  space_spatial.h                                                            */

#pragma once

#include "area_spatial.h"
#include "body_spatial.h"
#include "broad_phase_spatial.h"
#include "collision_object_spatial.h"
#include "soft_body_spatial.h"

#include "core/typedefs.h"
#include "systems/physics_spatial/direct_states/physics_direct_space_state_spatial.h"

class OrblitPhysicsDirectSpaceState3D : public PhysicsDirectSpaceState3D {
	ORBLIT_CLASS(OrblitPhysicsDirectSpaceState3D, PhysicsDirectSpaceState3D);

public:
	OrblitSpace3D *space = nullptr;

	virtual int intersect_point(const PS3DT::PointParameters &p_parameters, PS3DT::ShapeResult *r_results, int p_result_max) override;
	virtual bool intersect_ray(const PS3DT::RayParameters &p_parameters, PS3DT::RayResult &r_result) override;
	virtual int intersect_shape(const PS3DT::ShapeParameters &p_parameters, PS3DT::ShapeResult *r_results, int p_result_max) override;
	virtual bool cast_motion(const PS3DT::ShapeParameters &p_parameters, real_t &p_closest_safe, real_t &p_closest_unsafe, PS3DT::ShapeRestInfo *r_info = nullptr) override;
	virtual bool collide_shape(const PS3DT::ShapeParameters &p_parameters, Vector3 *r_results, int p_result_max, int &r_result_count) override;
	virtual bool rest_info(const PS3DT::ShapeParameters &p_parameters, PS3DT::ShapeRestInfo *r_info) override;
	virtual Vector3 get_closest_point_to_object_volume(RID p_object, const Vector3 p_point) const override;

	OrblitPhysicsDirectSpaceState3D();
};

class OrblitSpace3D {
public:
	enum ElapsedTime {
		ELAPSED_TIME_INTEGRATE_FORCES,
		ELAPSED_TIME_GENERATE_ISLANDS,
		ELAPSED_TIME_SETUP_CONSTRAINTS,
		ELAPSED_TIME_SOLVE_CONSTRAINTS,
		ELAPSED_TIME_INTEGRATE_VELOCITIES,
		ELAPSED_TIME_MAX

	};

private:
	uint64_t elapsed_time[ELAPSED_TIME_MAX] = {};

	OrblitPhysicsDirectSpaceState3D *direct_access = nullptr;
	RID self;

	OrblitBroadPhase3D *broadphase = nullptr;
	SelfList<OrblitBody3D>::List active_list;
	SelfList<OrblitBody3D>::List mass_properties_update_list;
	SelfList<OrblitBody3D>::List state_query_list;
	SelfList<OrblitArea3D>::List monitor_query_list;
	SelfList<OrblitArea3D>::List area_moved_list;
	SelfList<OrblitSoftBody3D>::List active_soft_body_list;

	static void *_broadphase_pair(OrblitCollisionObject3D *A, int p_subindex_A, OrblitCollisionObject3D *B, int p_subindex_B, void *p_self);
	static void _broadphase_unpair(OrblitCollisionObject3D *A, int p_subindex_A, OrblitCollisionObject3D *B, int p_subindex_B, void *p_data, void *p_self);

	HashSet<OrblitCollisionObject3D *> objects;

	OrblitArea3D *area = nullptr;

	int solver_iterations = 0;

	real_t contact_recycle_radius = 0.0;
	real_t contact_max_separation = 0.0;
	real_t contact_max_allowed_penetration = 0.0;
	real_t contact_bias = 0.0;

	enum {
		INTERSECTION_QUERY_MAX = 2048
	};

	OrblitCollisionObject3D *intersection_query_results[INTERSECTION_QUERY_MAX];
	int intersection_query_subindex_results[INTERSECTION_QUERY_MAX];

	real_t body_linear_velocity_sleep_threshold = 0.0;
	real_t body_angular_velocity_sleep_threshold = 0.0;
	real_t body_time_to_sleep = 0.0;

	bool locked = false;

	real_t last_step = 0.001;

	int island_count = 0;
	int active_objects = 0;
	int collision_pairs = 0;

	RID static_global_body;

	Vector<Vector3> contact_debug;
	int contact_debug_count = 0;

	friend class OrblitPhysicsDirectSpaceState3D;

	int _cull_aabb_for_body(OrblitBody3D *p_body, const AABB &p_aabb);

public:
	_FORCE_INLINE_ void set_self(const RID &p_self) { self = p_self; }
	_FORCE_INLINE_ RID get_self() const { return self; }

	void set_default_area(OrblitArea3D *p_area) { area = p_area; }
	OrblitArea3D *get_default_area() const { return area; }

	const SelfList<OrblitBody3D>::List &get_active_body_list() const;
	void body_add_to_active_list(SelfList<OrblitBody3D> *p_body);
	void body_remove_from_active_list(SelfList<OrblitBody3D> *p_body);
	void body_add_to_mass_properties_update_list(SelfList<OrblitBody3D> *p_body);
	void body_remove_from_mass_properties_update_list(SelfList<OrblitBody3D> *p_body);

	void body_add_to_state_query_list(SelfList<OrblitBody3D> *p_body);
	void body_remove_from_state_query_list(SelfList<OrblitBody3D> *p_body);

	void area_add_to_monitor_query_list(SelfList<OrblitArea3D> *p_area);
	void area_remove_from_monitor_query_list(SelfList<OrblitArea3D> *p_area);
	void area_add_to_moved_list(SelfList<OrblitArea3D> *p_area);
	void area_remove_from_moved_list(SelfList<OrblitArea3D> *p_area);
	const SelfList<OrblitArea3D>::List &get_moved_area_list() const;

	const SelfList<OrblitSoftBody3D>::List &get_active_soft_body_list() const;
	void soft_body_add_to_active_list(SelfList<OrblitSoftBody3D> *p_soft_body);
	void soft_body_remove_from_active_list(SelfList<OrblitSoftBody3D> *p_soft_body);

	OrblitBroadPhase3D *get_broadphase();

	void add_object(OrblitCollisionObject3D *p_object);
	void remove_object(OrblitCollisionObject3D *p_object);
	const HashSet<OrblitCollisionObject3D *> &get_objects() const;

	_FORCE_INLINE_ int get_solver_iterations() const { return solver_iterations; }
	_FORCE_INLINE_ real_t get_contact_recycle_radius() const { return contact_recycle_radius; }
	_FORCE_INLINE_ real_t get_contact_max_separation() const { return contact_max_separation; }
	_FORCE_INLINE_ real_t get_contact_max_allowed_penetration() const { return contact_max_allowed_penetration; }
	_FORCE_INLINE_ real_t get_contact_bias() const { return contact_bias; }
	_FORCE_INLINE_ real_t get_body_linear_velocity_sleep_threshold() const { return body_linear_velocity_sleep_threshold; }
	_FORCE_INLINE_ real_t get_body_angular_velocity_sleep_threshold() const { return body_angular_velocity_sleep_threshold; }
	_FORCE_INLINE_ real_t get_body_time_to_sleep() const { return body_time_to_sleep; }

	void update();
	void setup();
	void call_queries();

	bool is_locked() const;
	void lock();
	void unlock();

	real_t get_last_step() const { return last_step; }
	void set_last_step(real_t p_step) { last_step = p_step; }

	void set_param(PS3DE::SpaceParameter p_param, real_t p_value);
	real_t get_param(PS3DE::SpaceParameter p_param) const;

	void set_island_count(int p_island_count) { island_count = p_island_count; }
	int get_island_count() const { return island_count; }

	void set_active_objects(int p_active_objects) { active_objects = p_active_objects; }
	int get_active_objects() const { return active_objects; }

	int get_collision_pairs() const { return collision_pairs; }

	OrblitPhysicsDirectSpaceState3D *get_direct_state();

	void set_debug_contacts(int p_amount) { contact_debug.resize(p_amount); }
	_FORCE_INLINE_ bool is_debugging_contacts() const { return !contact_debug.is_empty(); }
	_FORCE_INLINE_ void add_debug_contact(const Vector3 &p_contact) {
		if (contact_debug_count < contact_debug.size()) {
			contact_debug.write[contact_debug_count++] = p_contact;
		}
	}
	_FORCE_INLINE_ Vector<Vector3> get_debug_contacts() { return contact_debug; }
	_FORCE_INLINE_ int get_debug_contact_count() { return contact_debug_count; }

	void set_static_global_body(RID p_body) { static_global_body = p_body; }
	RID get_static_global_body() { return static_global_body; }

	void set_elapsed_time(ElapsedTime p_time, uint64_t p_msec) { elapsed_time[p_time] = p_msec; }
	uint64_t get_elapsed_time(ElapsedTime p_time) const { return elapsed_time[p_time]; }

	bool test_body_motion(OrblitBody3D *p_body, const PS3DT::MotionParameters &p_parameters, PS3DT::MotionResult *r_result);

	OrblitSpace3D();
	~OrblitSpace3D();
};
