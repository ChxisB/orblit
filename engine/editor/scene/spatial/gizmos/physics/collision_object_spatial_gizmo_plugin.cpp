/**************************************************************************/
/*  collision_object_spatial_gizmo_plugin.cpp                                  */

#include "collision_object_spatial_gizmo_plugin.h"

#include "scene/spatial/physics/collision_object_spatial.h"
#include "scene/spatial/physics/collision_polygon_spatial.h"
#include "scene/spatial/physics/collision_shape_spatial.h"
#include "scene/stage/stage.h"
#include "scene/resources/surface_tool.h"

CollisionObject3DGizmoPlugin::CollisionObject3DGizmoPlugin() {
	const Color gizmo_color = Stage::get_singleton()->get_debug_collisions_color();
	create_material("shape_material", gizmo_color);
	const float gizmo_value = gizmo_color.get_v();
	const Color gizmo_color_disabled = Color(gizmo_value, gizmo_value, gizmo_value, 0.65);
	create_material("shape_material_disabled", gizmo_color_disabled);
}

bool CollisionObject3DGizmoPlugin::has_gizmo(Entity3D *p_spatial) {
	return Object::cast_to<CollisionObject3D>(p_spatial) != nullptr;
}

String CollisionObject3DGizmoPlugin::get_gizmo_name() const {
	return "CollisionObject3D";
}

int CollisionObject3DGizmoPlugin::get_priority() const {
	return -2;
}

void CollisionObject3DGizmoPlugin::redraw(EditorEntity3DGizmo *p_gizmo) {
	CollisionObject3D *co = Object::cast_to<CollisionObject3D>(p_gizmo->get_entity_3d());

	p_gizmo->clear();

	List<uint32_t> owner_ids;
	co->get_shape_owners(&owner_ids);
	for (uint32_t &owner_id : owner_ids) {
		Transform3D xform = co->shape_owner_get_transform(owner_id);
		Object *owner = co->shape_owner_get_owner(owner_id);
		// Exclude CollisionShape3D and CollisionPolygon3D as they have their gizmo.
		if (!Object::cast_to<CollisionShape3D>(owner) && !Object::cast_to<CollisionPolygon3D>(owner)) {
			Ref<Material> material = get_material(!co->is_shape_owner_disabled(owner_id) ? "shape_material" : "shape_material_disabled", p_gizmo);
			for (int shape_id = 0; shape_id < co->shape_owner_get_shape_count(owner_id); shape_id++) {
				Ref<Shape3D> s = co->shape_owner_get_shape(owner_id, shape_id);
				if (s.is_null()) {
					continue;
				}
				SurfaceTool st;
				st.append_from(s->get_debug_mesh(), 0, xform);

				p_gizmo->add_mesh(st.commit(), material);
				p_gizmo->add_collision_segments(s->get_debug_mesh_lines());
			}
		}
	}
}
