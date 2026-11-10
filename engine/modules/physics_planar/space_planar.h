/**************************************************************************/
/*  space_planar.h                                                            */

#pragma once

#include "area_planar.h"
#include "body_planar.h"
#include "broad_phase_planar.h"
#include "collision_object_planar.h"

#include "core/typedefs.h"
#include "systems/physics_planar/direct_states/physics_direct_space_state_planar.h"

class OrblitPhysicsDirectSpaceState2D : public PhysicsDirectSpaceState2D {
	ORBLIT_CLASS(OrblitPhysicsDirectSpaceState2D, PhysicsDirectSpaceState2D);

public:
	OrblitSpace2D *space = nullptr;

	virtual int intersect_point(const PS2DT::PointParameters &p_parameters, PS2DT::ShapeResult *r_results, int p_result_max) override;
	virtual bool intersect_ray(const PS2DT::RayParameters &p_parameters, PS2DT::RayResult &r_result) override;
	virtual int intersect_shape(const PS2DT::ShapeParameters &p_parameters, PS2DT::ShapeResult *r_results, int p_result_max) override;
	virtual bool cast_motion(const PS2DT::ShapeParameters &p_parameters, real_t &p_closest_safe, real_t &p_closest_unsafe) override;
	virtual bool collide_shape(const PS2DT::ShapeParameters &p_parameters, Vector2 *r_results, int p_result_max, int &r_result_count) override;
	virtual bool rest_info(const PS2DT::ShapeParameters &p_parameters, PS2DT::ShapeRestInfo *r_info) override;

	OrblitPhysicsDirectSpaceState2D() {}
};

class OrblitSpace2D {
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
	struct ExcludedShapeSW {
		OrblitShape2D *local_shape = nullptr;
		const OrblitCollisionObject2D *against_object = nullptr;
		int against_shape_index = 0;
	};

	uint64_t elapsed_time[ELAPSED_TIME_MAX] = {};

	OrblitPhysicsDirectSpaceState2D *direct_access = nullptr;
	RID self;

	OrblitBroadPhase2D *broadphase = nullptr;
	SelfList<OrblitBody2D>::List active_list;
	SelfList<OrblitBody2D>::List mass_properties_update_list;
	SelfList<OrblitBody2D>::List state_query_list;
	SelfList<OrblitArea2D>::List monitor_query_list;
	SelfList<OrblitArea2D>::List area_moved_list;

	static void *_broadphase_pair(OrblitCollisionObject2D *A, int p_subindex_A, OrblitCollisionObject2D *B, int p_subindex_B, void *p_self);
	static void _broadphase_unpair(OrblitCollisionObject2D *A, int p_subindex_A, OrblitCollisionObject2D *B, int p_subindex_B, void *p_data, void *p_self);

	HashSet<OrblitCollisionObject2D *> objects;

	OrblitArea2D *area = nullptr;

	int solver_iterations = 0;

	real_t contact_recycle_radius = 0.0;
	real_t contact_max_separation = 0.0;
	real_t contact_max_allowed_penetration = 0.0;
	real_t contact_bias = 0.0;
	real_t constraint_bias = 0.0;

	enum {
		INTERSECTION_QUERY_MAX = 2048
	};

	OrblitCollisionObject2D *intersection_query_results[INTERSECTION_QUERY_MAX];
	int intersection_query_subindex_results[INTERSECTION_QUERY_MAX];

	real_t body_linear_velocity_sleep_threshold = 0.0;
	real_t body_angular_velocity_sleep_threshold = 0.0;
	real_t body_time_to_sleep = 0.0;

	bool locked = false;

	real_t last_step = 0.001;

	int island_count = 0;
	int active_objects = 0;
	int collision_pairs = 0;

	int _cull_aabb_for_body(OrblitBody2D *p_body, const Rect2 &p_aabb);

	Vector<Vector2> contact_debug;
	int contact_debug_count = 0;

	friend class OrblitPhysicsDirectSpaceState2D;

public:
	_FORCE_INLINE_ void set_self(const RID &p_self) { self = p_self; }
	_FORCE_INLINE_ RID get_self() const { return self; }

	void set_default_area(OrblitArea2D *p_area) { area = p_area; }
	OrblitArea2D *get_default_area() const { return area; }

	const SelfList<OrblitBody2D>::List &get_active_body_list() const;
	void body_add_to_active_list(SelfList<OrblitBody2D> *p_body);
	void body_remove_from_active_list(SelfList<OrblitBody2D> *p_body);
	void body_add_to_mass_properties_update_list(SelfList<OrblitBody2D> *p_body);
	void body_remove_from_mass_properties_update_list(SelfList<OrblitBody2D> *p_body);
	void area_add_to_moved_list(SelfList<OrblitArea2D> *p_area);
	void area_remove_from_moved_list(SelfList<OrblitArea2D> *p_area);
	const SelfList<OrblitArea2D>::List &get_moved_area_list() const;

	void body_add_to_state_query_list(SelfList<OrblitBody2D> *p_body);
	void body_remove_from_state_query_list(SelfList<OrblitBody2D> *p_body);

	void area_add_to_monitor_query_list(SelfList<OrblitArea2D> *p_area);
	void area_remove_from_monitor_query_list(SelfList<OrblitArea2D> *p_area);

	OrblitBroadPhase2D *get_broadphase();

	void add_object(OrblitCollisionObject2D *p_object);
	void remove_object(OrblitCollisionObject2D *p_object);
	const HashSet<OrblitCollisionObject2D *> &get_objects() const;

	_FORCE_INLINE_ int get_solver_iterations() const { return solver_iterations; }
	_FORCE_INLINE_ real_t get_contact_recycle_radius() const { return contact_recycle_radius; }
	_FORCE_INLINE_ real_t get_contact_max_separation() const { return contact_max_separation; }
	_FORCE_INLINE_ real_t get_contact_max_allowed_penetration() const { return contact_max_allowed_penetration; }
	_FORCE_INLINE_ real_t get_contact_bias() const { return contact_bias; }
	_FORCE_INLINE_ real_t get_constraint_bias() const { return constraint_bias; }
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

	void set_param(PS2DE::SpaceParameter p_param, real_t p_value);
	real_t get_param(PS2DE::SpaceParameter p_param) const;

	void set_island_count(int p_island_count) { island_count = p_island_count; }
	int get_island_count() const { return island_count; }

	void set_active_objects(int p_active_objects) { active_objects = p_active_objects; }
	int get_active_objects() const { return active_objects; }

	int get_collision_pairs() const { return collision_pairs; }

	bool test_body_motion(OrblitBody2D *p_body, const PS2DT::MotionParameters &p_parameters, PS2DT::MotionResult *r_result);

	void set_debug_contacts(int p_amount) { contact_debug.resize(p_amount); }
	_FORCE_INLINE_ bool is_debugging_contacts() const { return !contact_debug.is_empty(); }
	_FORCE_INLINE_ void add_debug_contact(const Vector2 &p_contact) {
		if (contact_debug_count < contact_debug.size()) {
			contact_debug.write[contact_debug_count++] = p_contact;
		}
	}
	_FORCE_INLINE_ Vector<Vector2> get_debug_contacts() { return contact_debug; }
	_FORCE_INLINE_ int get_debug_contact_count() { return contact_debug_count; }

	OrblitPhysicsDirectSpaceState2D *get_direct_state();

	void set_elapsed_time(ElapsedTime p_time, uint64_t p_msec) { elapsed_time[p_time] = p_msec; }
	uint64_t get_elapsed_time(ElapsedTime p_time) const { return elapsed_time[p_time]; }

	OrblitSpace2D();
	~OrblitSpace2D();
};
