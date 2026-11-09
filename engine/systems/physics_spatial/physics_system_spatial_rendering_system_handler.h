/**************************************************************************/
/*  physics_system_spatial_rendering_system_handler.h                          */

#pragma once

#include "core/object/orblit_virtual.gen.h"
#include "core/object/object.h"

struct AABB;
struct Vector3;

class PhysicsSystem3DRenderingSystemHandler : public Object {
	ORBLIT_CLASS(PhysicsSystem3DRenderingSystemHandler, Object)
protected:
	ORBLIT_VIRTUAL2_REQUIRED(_set_vertex, int, const Vector3 &)
	ORBLIT_VIRTUAL2_REQUIRED(_set_normal, int, const Vector3 &)
	ORBLIT_VIRTUAL1_REQUIRED(_set_aabb, const AABB &)

	static void register_members();

public:
	virtual void set_vertex(int p_vertex_id, const Vector3 &p_vertex);
	virtual void set_normal(int p_vertex_id, const Vector3 &p_normal);
	virtual void set_aabb(const AABB &p_aabb);

	virtual ~PhysicsSystem3DRenderingSystemHandler() {}
};
