/**************************************************************************/
/*  shape_cast_spatial_gizmo_plugin.cpp                                        */

#include "shape_cast_spatial_gizmo_plugin.h"

#include "scene/spatial/physics/shape_cast_spatial.h"
#include "scene/stage/stage.h"

ShapeCast3DGizmoPlugin::ShapeCast3DGizmoPlugin() {
	const Color gizmo_color = Stage::get_singleton()->get_debug_collisions_color();
	create_material("shape_material", gizmo_color);
	const float gizmo_value = gizmo_color.get_v();
	const Color gizmo_color_disabled = Color(gizmo_value, gizmo_value, gizmo_value, 0.65);
	create_material("shape_material_disabled", gizmo_color_disabled);
}

bool ShapeCast3DGizmoPlugin::has_gizmo(Entity3D *p_spatial) {
	return Object::cast_to<ShapeCast3D>(p_spatial) != nullptr;
}

String ShapeCast3DGizmoPlugin::get_gizmo_name() const {
	return "ShapeCast3D";
}

int ShapeCast3DGizmoPlugin::get_priority() const {
	return -1;
}

void ShapeCast3DGizmoPlugin::redraw(EditorEntity3DGizmo *p_gizmo) {
	ShapeCast3D *shapecast = Object::cast_to<ShapeCast3D>(p_gizmo->get_entity_3d());

	p_gizmo->clear();

	const Ref<StandardMaterial3D> material = shapecast->is_enabled() ? shapecast->get_debug_material() : get_material("shape_material_disabled");

	p_gizmo->add_lines(shapecast->get_debug_line_vertices(), material);

	if (shapecast->get_shape().is_valid()) {
		p_gizmo->add_lines(shapecast->get_debug_shape_vertices(), material);
	}

	p_gizmo->add_collision_segments(shapecast->get_debug_line_vertices());
}
