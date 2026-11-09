/**************************************************************************/
/*  vehicle_body_spatial_gizmo_plugin.cpp                                      */

#include "vehicle_body_spatial_gizmo_plugin.h"

#include "scene/spatial/physics/vehicle_body_spatial.h"
#include "scene/stage/stage.h"

VehicleWheel3DGizmoPlugin::VehicleWheel3DGizmoPlugin() {
	Color gizmo_color = Stage::get_singleton()->get_debug_collisions_color();
	create_material("shape_material", gizmo_color);
}

bool VehicleWheel3DGizmoPlugin::has_gizmo(Entity3D *p_spatial) {
	return Object::cast_to<VehicleWheel3D>(p_spatial) != nullptr;
}

String VehicleWheel3DGizmoPlugin::get_gizmo_name() const {
	return "VehicleWheel3D";
}

int VehicleWheel3DGizmoPlugin::get_priority() const {
	return -1;
}

void VehicleWheel3DGizmoPlugin::redraw(EditorEntity3DGizmo *p_gizmo) {
	VehicleWheel3D *car_wheel = Object::cast_to<VehicleWheel3D>(p_gizmo->get_entity_3d());

	p_gizmo->clear();

	Vector<Vector3> points;

	float r = car_wheel->get_radius();
	const int skip = 10;
	for (int i = 0; i <= 360; i += skip) {
		float ra = Math::deg_to_rad((float)i);
		float rb = Math::deg_to_rad((float)i + skip);
		Point2 a = Vector2(Math::sin(ra), Math::cos(ra)) * r;
		Point2 b = Vector2(Math::sin(rb), Math::cos(rb)) * r;

		points.push_back(Vector3(0, a.x, a.y));
		points.push_back(Vector3(0, b.x, b.y));

		const int springsec = 4;

		for (int j = 0; j < springsec; j++) {
			float t = car_wheel->get_suspension_rest_length() * 5;
			points.push_back(Vector3(a.x, i / 360.0 * t / springsec + j * (t / springsec), a.y) * 0.2);
			points.push_back(Vector3(b.x, (i + skip) / 360.0 * t / springsec + j * (t / springsec), b.y) * 0.2);
		}
	}

	//travel
	points.push_back(Vector3(0, 0, 0));
	points.push_back(Vector3(0, car_wheel->get_suspension_rest_length(), 0));

	//axis
	points.push_back(Vector3(r * 0.2, car_wheel->get_suspension_rest_length(), 0));
	points.push_back(Vector3(-r * 0.2, car_wheel->get_suspension_rest_length(), 0));
	//axis
	points.push_back(Vector3(r * 0.2, 0, 0));
	points.push_back(Vector3(-r * 0.2, 0, 0));

	//forward line
	points.push_back(Vector3(0, -r, 0));
	points.push_back(Vector3(0, -r, r * 2));
	points.push_back(Vector3(0, -r, r * 2));
	points.push_back(Vector3(r * 2 * 0.2, -r, r * 2 * 0.8));
	points.push_back(Vector3(0, -r, r * 2));
	points.push_back(Vector3(-r * 2 * 0.2, -r, r * 2 * 0.8));

	Ref<Material> material = get_material("shape_material", p_gizmo);

	p_gizmo->add_lines(points, material);
	p_gizmo->add_collision_segments(points);
}
