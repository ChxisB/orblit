/**************************************************************************/
/*  physics_system_planar_wrap_mt.h                                           */

#pragma once

#include "core/object/worker_thread_pool.h"
#include "core/os/thread.h"
#include "core/containers/command_queue_mt.h"
#include "systems/physics_planar/physics_system_planar.h"

#define ASYNC_COND_PUSH (Thread::get_caller_id() != server_thread && !(doing_sync.is_set() && Thread::is_main_thread()))
#define ASYNC_COND_PUSH_AND_RET (Thread::get_caller_id() != server_thread && !(doing_sync.is_set() && Thread::is_main_thread()))
#define ASYNC_COND_PUSH_AND_SYNC (Thread::get_caller_id() != server_thread && !(doing_sync.is_set() && Thread::is_main_thread()))

#ifdef DEBUG_SYNC
#define SYNC_DEBUG print_line("sync on: " + String(__FUNCTION__));
#else
#define SYNC_DEBUG
#endif

#ifdef DEBUG_ENABLED
#ifdef DEV_ENABLED
#define MAIN_THREAD_SYNC_WARN WARN_PRINT("Call to " + String(__FUNCTION__) + " causing PhysicsSystem2D synchronizations on every frame. This significantly affects performance.");
#else
#define MAIN_THREAD_SYNC_WARN
#endif
#endif

#define SystemName PhysicsSystem2D
#define SystemNameWrapMT PhysicsSystem2DWrapMT
#define system_name physics_system_2d
#define WRITE_ACTION

#include "systems/system_wrap_mt_common.h"

class PhysicsSystem2DWrapMT : public PhysicsSystem2D {
	ORBLIT_SOFTCLASS(PhysicsSystem2DWrapMT, PhysicsSystem2D);

	mutable PhysicsSystem2D *physics_system_2d = nullptr;

	mutable CommandQueueMT command_queue;

	Thread::ID server_thread = Thread::UNASSIGNED_ID;
	WorkerThreadPool::TaskID server_task_id = WorkerThreadPool::INVALID_TASK_ID;
	bool exit = false;
	bool create_thread = false;
	SafeFlag doing_sync;

	void _assign_mt_ids(WorkerThreadPool::TaskID p_pump_task_id);
	void _thread_exit();
	void _thread_loop();
	void _thread_sync();

public:
	//FUNC1RID(shape,ShapeType); todo fix
	FUNCRID(world_boundary_shape)
	FUNCRID(separation_ray_shape)
	FUNCRID(segment_shape)
	FUNCRID(circle_shape)
	FUNCRID(rectangle_shape)
	FUNCRID(capsule_shape)
	FUNCRID(convex_polygon_shape)
	FUNCRID(concave_polygon_shape)

	FUNC2(shape_set_data, RID, const Variant &);
	FUNC2(shape_set_custom_solver_bias, RID, real_t);

	FUNC1RC(PS2DE::ShapeType, shape_get_type, RID);
	FUNC1RC(Variant, shape_get_data, RID);
	FUNC1RC(real_t, shape_get_custom_solver_bias, RID);

	//these work well, but should be used from the main thread only
	bool shape_collide(RID p_shape_A, const Transform2D &p_xform_A, const Vector2 &p_motion_A, RID p_shape_B, const Transform2D &p_xform_B, const Vector2 &p_motion_B, Vector2 *r_results, int p_result_max, int &r_result_count) override {
		ERR_FAIL_COND_V(!Thread::is_main_thread(), false);
		return physics_system_2d->shape_collide(p_shape_A, p_xform_A, p_motion_A, p_shape_B, p_xform_B, p_motion_B, r_results, p_result_max, r_result_count);
	}

	/* SPACE API */

	FUNCRID(space);
	FUNC2(space_set_active, RID, bool);
	FUNC1RC(bool, space_is_active, RID);

	FUNC3(space_set_param, RID, PS2DE::SpaceParameter, real_t);
	FUNC2RC(real_t, space_get_param, RID, PS2DE::SpaceParameter);

	// this function only works on physics process, errors and returns null otherwise
	PhysicsDirectSpaceState2D *space_get_direct_state(RID p_space) override {
		ERR_FAIL_COND_V(!Thread::is_main_thread(), nullptr);
		return physics_system_2d->space_get_direct_state(p_space);
	}

	FUNC2(space_set_debug_contacts, RID, int);
	virtual Vector<Vector2> space_get_contacts(RID p_space) const override {
		ERR_FAIL_COND_V(!Thread::is_main_thread(), Vector<Vector2>());
		return physics_system_2d->space_get_contacts(p_space);
	}

	virtual int space_get_contact_count(RID p_space) const override {
		ERR_FAIL_COND_V(!Thread::is_main_thread(), 0);
		return physics_system_2d->space_get_contact_count(p_space);
	}

	/* AREA API */

	//FUNC0RID(area);
	FUNCRID(area);

	FUNC2(area_set_space, RID, RID);
	FUNC1RC(RID, area_get_space, RID);

	FUNC4(area_add_shape, RID, RID, const Transform2D &, bool);
	FUNC3(area_set_shape, RID, int, RID);
	FUNC3(area_set_shape_transform, RID, int, const Transform2D &);
	FUNC3(area_set_shape_disabled, RID, int, bool);

	FUNC1RC(int, area_get_shape_count, RID);
	FUNC2RC(RID, area_get_shape, RID, int);
	FUNC2RC(Transform2D, area_get_shape_transform, RID, int);
	FUNC2(area_remove_shape, RID, int);
	FUNC1(area_clear_shapes, RID);

	FUNC2(area_attach_object_instance_id, RID, ObjectID);
	FUNC1RC(ObjectID, area_get_object_instance_id, RID);

	FUNC2(area_attach_canvas_instance_id, RID, ObjectID);
	FUNC1RC(ObjectID, area_get_canvas_instance_id, RID);

	FUNC3(area_set_param, RID, PS2DE::AreaParameter, const Variant &);
	FUNC2(area_set_transform, RID, const Transform2D &);

	FUNC2RC(Variant, area_get_param, RID, PS2DE::AreaParameter);
	FUNC1RC(Transform2D, area_get_transform, RID);

	FUNC2(area_set_collision_layer, RID, uint32_t);
	FUNC1RC(uint32_t, area_get_collision_layer, RID);

	FUNC2(area_set_collision_mask, RID, uint32_t);
	FUNC1RC(uint32_t, area_get_collision_mask, RID);

	FUNC2(area_set_monitorable, RID, bool);
	FUNC2(area_set_pickable, RID, bool);

	FUNC2(area_set_monitor_callback, RID, const Callable &);
	FUNC2(area_set_area_monitor_callback, RID, const Callable &);

	/* BODY API */

	//FUNC2RID(body,BodyMode,bool);
	FUNCRID(body)

	FUNC2(body_set_space, RID, RID);
	FUNC1RC(RID, body_get_space, RID);

	FUNC2(body_set_mode, RID, PS2DE::BodyMode);
	FUNC1RC(PS2DE::BodyMode, body_get_mode, RID);

	FUNC4(body_add_shape, RID, RID, const Transform2D &, bool);
	FUNC3(body_set_shape, RID, int, RID);
	FUNC3(body_set_shape_transform, RID, int, const Transform2D &);

	FUNC1RC(int, body_get_shape_count, RID);
	FUNC2RC(Transform2D, body_get_shape_transform, RID, int);
	FUNC2RC(RID, body_get_shape, RID, int);

	FUNC3(body_set_shape_disabled, RID, int, bool);
	FUNC5(body_set_shape_as_one_way_collision, RID, int, bool, real_t, const Vector2 &);

	FUNC2(body_remove_shape, RID, int);
	FUNC1(body_clear_shapes, RID);

	FUNC2(body_attach_object_instance_id, RID, ObjectID);
	FUNC1RC(ObjectID, body_get_object_instance_id, RID);

	FUNC2(body_attach_canvas_instance_id, RID, ObjectID);
	FUNC1RC(ObjectID, body_get_canvas_instance_id, RID);

	FUNC2(body_set_continuous_collision_detection_mode, RID, PS2DE::CCDMode);
	FUNC1RC(PS2DE::CCDMode, body_get_continuous_collision_detection_mode, RID);

	FUNC2(body_set_collision_layer, RID, uint32_t);
	FUNC1RC(uint32_t, body_get_collision_layer, RID);

	FUNC2(body_set_collision_mask, RID, uint32_t);
	FUNC1RC(uint32_t, body_get_collision_mask, RID);

	FUNC2(body_set_collision_priority, RID, real_t);
	FUNC1RC(real_t, body_get_collision_priority, RID);

	FUNC3(body_set_param, RID, PS2DE::BodyParameter, const Variant &);
	FUNC2RC(Variant, body_get_param, RID, PS2DE::BodyParameter);

	FUNC1(body_reset_mass_properties, RID);

	FUNC3(body_set_state, RID, PS2DE::BodyState, const Variant &);
	FUNC2RC(Variant, body_get_state, RID, PS2DE::BodyState);

	FUNC2(body_apply_central_impulse, RID, const Vector2 &);
	FUNC2(body_apply_torque_impulse, RID, real_t);
	FUNC3(body_apply_impulse, RID, const Vector2 &, const Vector2 &);

	FUNC2(body_apply_central_force, RID, const Vector2 &);
	FUNC3(body_apply_force, RID, const Vector2 &, const Vector2 &);
	FUNC2(body_apply_torque, RID, real_t);

	FUNC2(body_add_constant_central_force, RID, const Vector2 &);
	FUNC3(body_add_constant_force, RID, const Vector2 &, const Vector2 &);
	FUNC2(body_add_constant_torque, RID, real_t);

	FUNC2(body_set_constant_force, RID, const Vector2 &);
	FUNC1RC(Vector2, body_get_constant_force, RID);

	FUNC2(body_set_constant_torque, RID, real_t);
	FUNC1RC(real_t, body_get_constant_torque, RID);

	FUNC2(body_set_axis_velocity, RID, const Vector2 &);

	FUNC2(body_add_collision_exception, RID, RID);
	FUNC2(body_remove_collision_exception, RID, RID);
	FUNC2S(body_get_collision_exceptions, RID, List<RID> *);

	FUNC2(body_set_max_contacts_reported, RID, int);
	FUNC1RC(int, body_get_max_contacts_reported, RID);

	FUNC2(body_set_contacts_reported_depth_threshold, RID, real_t);
	FUNC1RC(real_t, body_get_contacts_reported_depth_threshold, RID);

	FUNC2(body_set_omit_force_integration, RID, bool);
	FUNC1RC(bool, body_is_omitting_force_integration, RID);

	FUNC2(body_set_state_sync_callback, RID, const Callable &);
	FUNC3(body_set_force_integration_callback, RID, const Callable &, const Variant &);

	bool body_collide_shape(RID p_body, int p_body_shape, RID p_shape, const Transform2D &p_shape_xform, const Vector2 &p_motion, Vector2 *r_results, int p_result_max, int &r_result_count) override {
		return physics_system_2d->body_collide_shape(p_body, p_body_shape, p_shape, p_shape_xform, p_motion, r_results, p_result_max, r_result_count);
	}

	FUNC2(body_set_pickable, RID, bool);

	bool body_test_motion(RID p_body, const PS2DT::MotionParameters &p_parameters, PS2DT::MotionResult *r_result = nullptr) override {
		ERR_FAIL_COND_V(!Thread::is_main_thread(), false);
		return physics_system_2d->body_test_motion(p_body, p_parameters, r_result);
	}

	// this function only works on physics process, errors and returns null otherwise
	PhysicsDirectBodyState2D *body_get_direct_state(RID p_body) override {
		ERR_FAIL_COND_V(!Thread::is_main_thread(), nullptr);
		return physics_system_2d->body_get_direct_state(p_body);
	}

	/* JOINT API */

	FUNCRID(joint)

	FUNC1(joint_clear, RID)

	FUNC3(joint_set_param, RID, PS2DE::JointParam, real_t);
	FUNC2RC(real_t, joint_get_param, RID, PS2DE::JointParam);

	FUNC2(joint_disable_collisions_between_bodies, RID, const bool);
	FUNC1RC(bool, joint_is_disabled_collisions_between_bodies, RID);

	///FUNC3RID(pin_joint,const Vector2&,RID,RID);
	///FUNC5RID(groove_joint,const Vector2&,const Vector2&,const Vector2&,RID,RID);
	///FUNC4RID(damped_spring_joint,const Vector2&,const Vector2&,RID,RID);

	//TODO need to convert this to FUNCRID, but it's a hassle..

	FUNC4(joint_make_pin, RID, const Vector2 &, RID, RID);
	FUNC6(joint_make_groove, RID, const Vector2 &, const Vector2 &, const Vector2 &, RID, RID);
	FUNC5(joint_make_damped_spring, RID, const Vector2 &, const Vector2 &, RID, RID);

	FUNC3(pin_joint_set_param, RID, PS2DE::PinJointParam, real_t);
	FUNC2RC(real_t, pin_joint_get_param, RID, PS2DE::PinJointParam);

	FUNC3(pin_joint_set_flag, RID, PS2DE::PinJointFlag, bool);
	FUNC2RC(bool, pin_joint_get_flag, RID, PS2DE::PinJointFlag);

	FUNC3(damped_spring_joint_set_param, RID, PS2DE::DampedSpringParam, real_t);
	FUNC2RC(real_t, damped_spring_joint_get_param, RID, PS2DE::DampedSpringParam);

	FUNC1RC(PS2DE::JointType, joint_get_type, RID);

	/* MISC */

	FUNC1(free_rid, RID);
	FUNC1(set_active, bool);

	virtual void init() override;
	virtual void step(real_t p_step) override;
	virtual void sync() override;
	virtual void end_sync() override;
	virtual void flush_queries() override;
	virtual void finish() override;

	virtual bool is_flushing_queries() const override {
		return physics_system_2d->is_flushing_queries();
	}

	int get_process_info(PS2DE::ProcessInfo p_info) override {
		return physics_system_2d->get_process_info(p_info);
	}

	PhysicsSystem2DWrapMT(PhysicsSystem2D *p_contained, bool p_create_thread);
	~PhysicsSystem2DWrapMT();

#undef SystemNameWrapMT
#undef SystemName
#undef system_name
#undef WRITE_ACTION
};

#ifdef DEBUG_SYNC
#undef DEBUG_SYNC
#endif
#undef SYNC_DEBUG

#ifdef DEBUG_ENABLED
#undef MAIN_THREAD_SYNC_WARN
#endif
