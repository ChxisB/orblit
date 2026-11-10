/**************************************************************************/
/*  physics_system_spatial_rendering_system_handler.cpp                        */

#include "physics_system_spatial_rendering_system_handler.h"

#include "core/math/aabb.h"
#include "core/math/vector3.h"
#include "core/object/class_registry.h"

void PhysicsSystem3DRenderingSystemHandler::set_vertex(int p_vertex_id, const Vector3 &p_vertex) {
	ORBLIT_VIRTUAL_CALL(_set_vertex, p_vertex_id, p_vertex);
}
void PhysicsSystem3DRenderingSystemHandler::set_normal(int p_vertex_id, const Vector3 &p_normal) {
	ORBLIT_VIRTUAL_CALL(_set_normal, p_vertex_id, p_normal);
}
void PhysicsSystem3DRenderingSystemHandler::set_aabb(const AABB &p_aabb) {
	ORBLIT_VIRTUAL_CALL(_set_aabb, p_aabb);
}

void PhysicsSystem3DRenderingSystemHandler::register_members() {
	ORBLIT_VIRTUAL_BIND(_set_vertex, "vertex_id", "vertex");
	ORBLIT_VIRTUAL_BIND(_set_normal, "vertex_id", "normal");
	ORBLIT_VIRTUAL_BIND(_set_aabb, "aabb");

	ClassRegistry::bind_method(D_METHOD("set_vertex", "vertex_id", "vertex"), &PhysicsSystem3DRenderingSystemHandler::set_vertex);
	ClassRegistry::bind_method(D_METHOD("set_normal", "vertex_id", "normal"), &PhysicsSystem3DRenderingSystemHandler::set_normal);
	ClassRegistry::bind_method(D_METHOD("set_aabb", "aabb"), &PhysicsSystem3DRenderingSystemHandler::set_aabb);
}
