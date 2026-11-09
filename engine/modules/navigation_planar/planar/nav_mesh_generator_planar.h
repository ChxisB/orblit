/**************************************************************************/
/*  nav_mesh_generator_planar.h                                               */

#pragma once

#ifdef CLIPPER2_ENABLED

#include "core/object/object.h"
#include "core/object/worker_thread_pool.h"
#include "systems/navigation_planar/navigation_system_planar.h"

class Entity;
class NavigationPolygon;
class NavigationMeshSourceGeometryData2D;

class NavMeshGenerator2D : public Object {
	ORBLIT_SOFTCLASS(NavMeshGenerator2D, Object);

	static NavMeshGenerator2D *singleton;

	static Mutex baking_navmesh_mutex;
	static Mutex generator_task_mutex;

	static RWLock generator_parsers_rwlock;
	static LocalVector<NavMeshGeometryParser2D *> generator_parsers;

	static bool use_threads;
	static bool baking_use_multiple_threads;
	static bool baking_use_high_priority_threads;

	struct NavMeshGeneratorTask2D {
		enum TaskStatus {
			BAKING_STARTED,
			BAKING_FINISHED,
			BAKING_FAILED,
			CALLBACK_DISPATCHED,
			CALLBACK_FAILED,
		};

		Ref<NavigationPolygon> navigation_mesh;
		Ref<NavigationMeshSourceGeometryData2D> source_geometry_data;
		Callable callback;
		WorkerThreadPool::TaskID thread_task_id = WorkerThreadPool::INVALID_TASK_ID;
		NavMeshGeneratorTask2D::TaskStatus status = NavMeshGeneratorTask2D::TaskStatus::BAKING_STARTED;
	};

	static HashMap<WorkerThreadPool::TaskID, NavMeshGeneratorTask2D *> generator_tasks;

	static void generator_thread_bake(void *p_arg);

	static HashSet<Ref<NavigationPolygon>> baking_navmeshes;

	static void generator_parse_geometry_node(Ref<NavigationPolygon> p_navigation_mesh, Ref<NavigationMeshSourceGeometryData2D> p_source_geometry_data, Entity *p_node, bool p_recurse_children);
	static void generator_parse_source_geometry_data(Ref<NavigationPolygon> p_navigation_mesh, Ref<NavigationMeshSourceGeometryData2D> p_source_geometry_data, Entity *p_root_node);
	static void generator_bake_from_source_geometry_data(Ref<NavigationPolygon> p_navigation_mesh, Ref<NavigationMeshSourceGeometryData2D> p_source_geometry_data);

	static bool generator_emit_callback(const Callable &p_callback);

public:
	static NavMeshGenerator2D *get_singleton();

	static void sync();
	static void cleanup();
	static void finish();

	static void set_generator_parsers(const LocalVector<NavMeshGeometryParser2D *> &p_parsers);

	static void parse_source_geometry_data(Ref<NavigationPolygon> p_navigation_mesh, Ref<NavigationMeshSourceGeometryData2D> p_source_geometry_data, Entity *p_root_node, const Callable &p_callback = Callable());
	static void bake_from_source_geometry_data(Ref<NavigationPolygon> p_navigation_mesh, Ref<NavigationMeshSourceGeometryData2D> p_source_geometry_data, const Callable &p_callback = Callable());
	static void bake_from_source_geometry_data_async(Ref<NavigationPolygon> p_navigation_mesh, Ref<NavigationMeshSourceGeometryData2D> p_source_geometry_data, const Callable &p_callback = Callable());
	static bool is_baking(Ref<NavigationPolygon> p_navigation_polygon);

	NavMeshGenerator2D();
	~NavMeshGenerator2D();
};

#endif // CLIPPER2_ENABLED
