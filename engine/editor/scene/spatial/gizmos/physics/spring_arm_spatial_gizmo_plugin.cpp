/**************************************************************************/
/*  spring_arm_spatial_gizmo_plugin.cpp                                        */

#include "spring_arm_spatial_gizmo_plugin.h"

#include "scene/spatial/physics/spring_arm_spatial.h"
#include "scene/stage/stage.h"

void SpringArm3DGizmoPlugin::redraw(EditorEntity3DGizmo *p_gizmo) {
	SpringArm3D *spring_arm = Object::cast_to<SpringArm3D>(p_gizmo->get_entity_3d());

	p_gizmo->clear();

	Vector<Vector3> lines = {
		Vector3(),
		Vector3(0, 0, 1.0) * spring_arm->get_length()
	};

	Ref<StandardMaterial3D> material = get_material("shape_material", p_gizmo);

	p_gizmo->add_lines(lines, material);
	p_gizmo->add_collision_segments(lines);
}

SpringArm3DGizmoPlugin::SpringArm3DGizmoPlugin() {
	Color gizmo_color = Stage::get_singleton()->get_debug_collisions_color();
	create_material("shape_material", gizmo_color);
}

bool SpringArm3DGizmoPlugin::has_gizmo(Entity3D *p_spatial) {
	return Object::cast_to<SpringArm3D>(p_spatial) != nullptr;
}

String SpringArm3DGizmoPlugin::get_gizmo_name() const {
	return "SpringArm3D";
}

int SpringArm3DGizmoPlugin::get_priority() const {
	return -1;
}
