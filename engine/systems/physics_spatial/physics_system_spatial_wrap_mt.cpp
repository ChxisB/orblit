/**************************************************************************/
/*  physics_system_spatial_wrap_mt.cpp                                         */

#include "physics_system_spatial_wrap_mt.h"

#include "core/object/callable_mp.h"

void PhysicsSystem3DWrapMT::_assign_mt_ids(WorkerThreadPool::TaskID p_pump_task_id) {
	server_thread = Thread::get_caller_id();
	server_task_id = p_pump_task_id;
}

void PhysicsSystem3DWrapMT::_thread_exit() {
	exit = true;
}

void PhysicsSystem3DWrapMT::_thread_loop() {
	while (!exit) {
		WorkerThreadPool::get_singleton()->yield();

		if (!doing_sync.is_set()) {
			command_queue.flush_all();
		}
	}
}

void PhysicsSystem3DWrapMT::_thread_sync() {
	doing_sync.set();
}

/* EVENT QUEUING */

void PhysicsSystem3DWrapMT::step(real_t p_step) {
	if (create_thread) {
		command_queue.push(physics_system_3d, &PhysicsSystem3D::step, p_step);
	} else {
		physics_system_3d->step(p_step);
	}
}

void PhysicsSystem3DWrapMT::sync() {
	if (create_thread) {
		command_queue.push_and_sync(this, &PhysicsSystem3DWrapMT::_thread_sync);
	} else {
		command_queue.flush_all(); // Flush all pending from other threads.
	}
	physics_system_3d->sync();
}

void PhysicsSystem3DWrapMT::flush_queries() {
	physics_system_3d->flush_queries();
}

void PhysicsSystem3DWrapMT::end_sync() {
	physics_system_3d->end_sync();

	if (create_thread) {
		doing_sync.clear();
	}
}

void PhysicsSystem3DWrapMT::init() {
	if (create_thread) {
		WorkerThreadPool::TaskID tid = WorkerThreadPool::get_singleton()->add_task(callable_mp(this, &PhysicsSystem3DWrapMT::_thread_loop), true, "Physics server 3D pump task", true);
		command_queue.set_pump_task_id(tid);
		command_queue.push(this, &PhysicsSystem3DWrapMT::_assign_mt_ids, tid);
		command_queue.push_and_sync(physics_system_3d, &PhysicsSystem3D::init);
		DEV_ASSERT(server_task_id == tid);
	} else {
		server_thread = Thread::MAIN_ID;
		physics_system_3d->init();
	}
}

void PhysicsSystem3DWrapMT::finish() {
	if (create_thread) {
		command_queue.push(physics_system_3d, &PhysicsSystem3D::finish);
		command_queue.push(this, &PhysicsSystem3DWrapMT::_thread_exit);
		if (server_task_id != WorkerThreadPool::INVALID_TASK_ID) {
			WorkerThreadPool::get_singleton()->wait_for_task_completion(server_task_id);
			server_task_id = WorkerThreadPool::INVALID_TASK_ID;
		}
		server_thread = Thread::MAIN_ID;
	} else {
		physics_system_3d->finish();
	}
}

PhysicsSystem3DWrapMT::PhysicsSystem3DWrapMT(PhysicsSystem3D *p_contained, bool p_create_thread) {
	physics_system_3d = p_contained;
	create_thread = p_create_thread;
}

PhysicsSystem3DWrapMT::~PhysicsSystem3DWrapMT() {
	memdelete(physics_system_3d);
}
