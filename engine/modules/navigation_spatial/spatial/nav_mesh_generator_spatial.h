/**************************************************************************/
/*  nav_mesh_generator_spatial.h                                               */

#pragma once

#include "core/object/object.h"
#include "core/object/worker_thread_pool.h"
#include "systems/navigation_spatial/navigation_system_spatial.h"

class Entity;
class NavigationMesh;
class NavigationMeshSourceGeometryData3D;

class NavMeshGenerator3D : public Object {
	ORBLIT_SOFTCLASS(NavMeshGenerator3D, Object);

	static NavMeshGenerator3D *singleton;

	static Mutex baking_navmesh_mutex;
	static Mutex generator_task_mutex;

	static RWLock generator_parsers_rwlock;
	static LocalVector<NavMeshGeometryParser3D *> generator_parsers;

	static bool use_threads;
	static bool baking_use_multiple_threads;
	static bool baking_use_high_priority_threads;

public:
	enum NavMeshBakeState {
		BAKE_STATE_NONE,
		BAKE_STATE_CONFIGURATION,
		BAKE_STATE_CALC_GRID_SIZE,
		BAKE_STATE_CREATE_HEIGHTFIELD,
		BAKE_STATE_MARK_WALKABLE_TRIANGLES,
		BAKE_STATE_CONSTRUCT_COMPACT_HEIGHTFIELD,
		BAKE_STATE_ERODE_WALKABLE_AREA,
		BAKE_STATE_SAMPLE_PARTITIONING,
		BAKE_STATE_CREATING_CONTOURS,
		BAKE_STATE_CREATING_POLYMESH,
		BAKE_STATE_CONVERTING_NATIVE_NAVMESH,
		BAKE_STATE_BAKE_CLEANUP,
		BAKE_STATE_BAKE_FINISHED,
		BAKE_STATE_MAX,
	};

private:
	struct NavMeshGeneratorTask3D {
		enum TaskStatus {
			BAKING_STARTED,
			BAKING_FINISHED,
			BAKING_FAILED,
			CALLBACK_DISPATCHED,
			CALLBACK_FAILED,
		};

		Ref<NavigationMesh> navigation_mesh;
		Ref<NavigationMeshSourceGeometryData3D> source_geometry_data;
		Callable callback;
		WorkerThreadPool::TaskID thread_task_id = WorkerThreadPool::INVALID_TASK_ID;
		NavMeshGeneratorTask3D::TaskStatus status = NavMeshGeneratorTask3D::TaskStatus::BAKING_STARTED;

		NavMeshBakeState bake_state = NavMeshBakeState::BAKE_STATE_NONE;
	};

	static HashMap<WorkerThreadPool::TaskID, NavMeshGeneratorTask3D *> generator_tasks;

	static void generator_thread_bake(void *p_arg);

	static HashMap<Ref<NavigationMesh>, NavMeshGeneratorTask3D *> baking_navmeshes;

	static void generator_parse_geometry_node(const Ref<NavigationMesh> &p_navigation_mesh, Ref<NavigationMeshSourceGeometryData3D> p_source_geometry_data, Entity *p_node, bool p_recurse_children);
	static void generator_parse_source_geometry_data(const Ref<NavigationMesh> &p_navigation_mesh, Ref<NavigationMeshSourceGeometryData3D> p_source_geometry_data, Entity *p_root_node);
	static void generator_bake_from_source_geometry_data(NavMeshGeneratorTask3D *p_generator_task);

	static bool generator_emit_callback(const Callable &p_callback);

public:
	static NavMeshGenerator3D *get_singleton();

	static void sync();
	static void cleanup();
	static void finish();

	static void set_generator_parsers(const LocalVector<NavMeshGeometryParser3D *> &p_parsers);

	static void parse_source_geometry_data(Ref<NavigationMesh> p_navigation_mesh, Ref<NavigationMeshSourceGeometryData3D> p_source_geometry_data, Entity *p_root_node, const Callable &p_callback = Callable());
	static void bake_from_source_geometry_data(Ref<NavigationMesh> p_navigation_mesh, Ref<NavigationMeshSourceGeometryData3D> p_source_geometry_data, const Callable &p_callback = Callable());
	static void bake_from_source_geometry_data_async(Ref<NavigationMesh> p_navigation_mesh, Ref<NavigationMeshSourceGeometryData3D> p_source_geometry_data, const Callable &p_callback = Callable());
	static bool is_baking(Ref<NavigationMesh> p_navigation_mesh);
	static String get_baking_state_msg(Ref<NavigationMesh> p_navigation_mesh);

	NavMeshGenerator3D();
	~NavMeshGenerator3D();
};
