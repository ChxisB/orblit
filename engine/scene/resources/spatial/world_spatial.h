/**************************************************************************/
/*  world_spatial.h                                                            */

#pragma once

#include "core/io/resource.h"
#include "scene/resources/environment.h"

#ifndef PHYSICS_SPATIAL_DISABLED
class PhysicsDirectSpaceState3D;
#endif // PHYSICS_SPATIAL_DISABLED

class CameraAttributes;
class Camera3D;
class Compositor;
class VisibleOnScreenNotifier3D;
struct SpatialIndexer;

class World3D : public Resource {
	ORBLIT_CLASS(World3D, Resource);

private:
	RID scenario;
	mutable RID space;
#ifndef NAVIGATION_SPATIAL_DISABLED
	mutable RID navigation_map;
#endif // NAVIGATION_SPATIAL_DISABLED

	Ref<Environment> environment;
	Ref<Environment> fallback_environment;
	Ref<CameraAttributes> camera_attributes;
	Ref<Compositor> compositor;

	HashSet<Camera3D *> cameras;

protected:
	static void register_members();

	friend class Camera3D;

	void _register_camera(Camera3D *p_camera);
	void _remove_camera(Camera3D *p_camera);

public:
	RID get_space() const;
#ifndef NAVIGATION_SPATIAL_DISABLED
	RID get_navigation_map() const;
#endif // NAVIGATION_SPATIAL_DISABLED
	RID get_scenario() const;

	void set_environment(const Ref<Environment> &p_environment);
	Ref<Environment> get_environment() const;

	void set_fallback_environment(const Ref<Environment> &p_environment);
	Ref<Environment> get_fallback_environment() const;

	void set_camera_attributes(const Ref<CameraAttributes> &p_camera_attributes);
	Ref<CameraAttributes> get_camera_attributes() const;

	void set_compositor(const Ref<Compositor> &p_compositor);
	Ref<Compositor> get_compositor() const;

	_FORCE_INLINE_ const HashSet<Camera3D *> &get_cameras() const { return cameras; }

#ifndef PHYSICS_SPATIAL_DISABLED
	PhysicsDirectSpaceState3D *get_direct_space_state();
#endif // PHYSICS_SPATIAL_DISABLED

	World3D();
	~World3D();
};
