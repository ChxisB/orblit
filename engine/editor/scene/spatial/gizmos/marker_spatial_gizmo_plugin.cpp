/**************************************************************************/
/*  marker_spatial_gizmo_plugin.cpp                                            */

#include "marker_spatial_gizmo_plugin.h"

#include "editor/editor_node.h"
#include "editor/editor_string_names.h"
#include "scene/spatial/marker_spatial.h"

Marker3DGizmoPlugin::Marker3DGizmoPlugin() {
	pos3d_mesh.instantiate();

	Vector<Vector3> cursor_points;
	Vector<Color> cursor_colors;
	const float cs = 1.0;
	// Add more points to create a "hard stop" in the color gradient.
	cursor_points.push_back(Vector3(+cs, 0, 0));
	cursor_points.push_back(Vector3());
	cursor_points.push_back(Vector3());
	cursor_points.push_back(Vector3(-cs, 0, 0));

	cursor_points.push_back(Vector3(0, +cs, 0));
	cursor_points.push_back(Vector3());
	cursor_points.push_back(Vector3());
	cursor_points.push_back(Vector3(0, -cs, 0));

	cursor_points.push_back(Vector3(0, 0, +cs));
	cursor_points.push_back(Vector3());
	cursor_points.push_back(Vector3());
	cursor_points.push_back(Vector3(0, 0, -cs));

	// Use the axis color which is brighter for the positive axis.
	// Use a darkened axis color for the negative axis.
	// This makes it possible to see in which direction the Marker3D node is rotated
	// (which can be important depending on how it's used).
	const Color color_x = EditorNode::get_singleton()->get_editor_theme()->get_color(SNAME("axis_x_color"), EditorStringName(Editor));
	cursor_colors.push_back(color_x);
	cursor_colors.push_back(color_x);
	cursor_colors.push_back(color_x.darkened(0.5));
	cursor_colors.push_back(color_x.darkened(0.5));

	const Color color_y = EditorNode::get_singleton()->get_editor_theme()->get_color(SNAME("axis_y_color"), EditorStringName(Editor));
	cursor_colors.push_back(color_y);
	cursor_colors.push_back(color_y);
	cursor_colors.push_back(color_y.darkened(0.5));
	cursor_colors.push_back(color_y.darkened(0.5));

	const Color color_z = EditorNode::get_singleton()->get_editor_theme()->get_color(SNAME("axis_z_color"), EditorStringName(Editor));
	cursor_colors.push_back(color_z);
	cursor_colors.push_back(color_z);
	cursor_colors.push_back(color_z.darkened(0.5));
	cursor_colors.push_back(color_z.darkened(0.5));

	Ref<StandardMaterial3D> mat = memnew(StandardMaterial3D);
	mat->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	mat->set_flag(StandardMaterial3D::FLAG_ALBEDO_FROM_VERTEX_COLOR, true);
	mat->set_flag(StandardMaterial3D::FLAG_SRGB_VERTEX_COLOR, true);
	mat->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	mat->set_transparency(StandardMaterial3D::TRANSPARENCY_ALPHA);

	Array d;
	d.resize(RSE::ARRAY_MAX);
	d[Mesh::ARRAY_VERTEX] = cursor_points;
	d[Mesh::ARRAY_COLOR] = cursor_colors;
	pos3d_mesh->add_surface_from_arrays(Mesh::PRIMITIVE_LINES, d);
	pos3d_mesh->surface_set_material(0, mat);
}

bool Marker3DGizmoPlugin::has_gizmo(Entity3D *p_spatial) {
	return Object::cast_to<Marker3D>(p_spatial) != nullptr;
}

String Marker3DGizmoPlugin::get_gizmo_name() const {
	return "Marker3D";
}

int Marker3DGizmoPlugin::get_priority() const {
	return -1;
}

void Marker3DGizmoPlugin::redraw(EditorEntity3DGizmo *p_gizmo) {
	const Marker3D *marker = Object::cast_to<Marker3D>(p_gizmo->get_entity_3d());
	const real_t extents = marker->get_gizmo_extents();
	const Transform3D xform(Basis::from_scale(Vector3(extents, extents, extents)));

	p_gizmo->clear();
	p_gizmo->add_mesh(pos3d_mesh, Ref<Material>(), xform);

	const Vector<Vector3> points = {
		Vector3(-extents, 0, 0),
		Vector3(+extents, 0, 0),
		Vector3(0, -extents, 0),
		Vector3(0, +extents, 0),
		Vector3(0, 0, -extents),
		Vector3(0, 0, +extents),
	};
	p_gizmo->add_collision_segments(points);
}
