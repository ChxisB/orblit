/**************************************************************************/
/*  world_planar.h                                                            */

#pragma once

#include "core/io/resource.h"

#ifndef PHYSICS_PLANAR_DISABLED
class PhysicsDirectSpaceState2D;
#endif // PHYSICS_PLANAR_DISABLED

class VisibleOnScreenNotifier2D;
class Viewport;
struct SpatialIndexer2D;

// World2D is needed for Viewport for CanvasItem rendering even when 2D is disabled.
class World2D : public Resource {
	ORBLIT_CLASS(World2D, Resource);

	RID canvas;
#ifndef NAVIGATION_PLANAR_DISABLED
	mutable RID navigation_map;
#endif // NAVIGATION_PLANAR_DISABLED
#ifndef PHYSICS_PLANAR_DISABLED
	mutable RID space;
#endif // PHYSICS_PLANAR_DISABLED

	HashSet<Viewport *> viewports;

protected:
	static void register_members();
	friend class Viewport;

public:
	RID get_canvas() const;
#ifndef NAVIGATION_PLANAR_DISABLED
	RID get_navigation_map() const;
#endif // NAVIGATION_PLANAR_DISABLED

#ifndef PHYSICS_PLANAR_DISABLED
	RID get_space() const;
	PhysicsDirectSpaceState2D *get_direct_space_state();
#endif // PHYSICS_PLANAR_DISABLED

	void register_viewport(Viewport *p_viewport);
	void remove_viewport(Viewport *p_viewport);

	_FORCE_INLINE_ const HashSet<Viewport *> &get_viewports() { return viewports; }

	World2D();
	~World2D();
};
