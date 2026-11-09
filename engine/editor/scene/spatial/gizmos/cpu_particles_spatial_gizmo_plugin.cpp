/**************************************************************************/
/*  cpu_particles_spatial_gizmo_plugin.cpp                                     */

#include "cpu_particles_spatial_gizmo_plugin.h"

#include "editor/editor_node.h"
#include "editor/editor_string_names.h"
#include "editor/settings/editor_settings.h"
#include "scene/spatial/cpu_particles_spatial.h"

CPUParticles3DGizmoPlugin::CPUParticles3DGizmoPlugin() {
	Color gizmo_color = EDITOR_GET("editors/spatial_gizmos/gizmo_colors/particles");
	create_material("particles_material", gizmo_color);
	gizmo_color.a = MAX((gizmo_color.a - 0.2) * 0.02, 0.0);
	create_material("particles_solid_material", gizmo_color);
	create_icon_material("particles_icon", EditorNode::get_singleton()->get_editor_theme()->get_icon(SNAME("GizmoCPUParticles3D"), EditorStringName(EditorIcons)));
}

bool CPUParticles3DGizmoPlugin::has_gizmo(Entity3D *p_spatial) {
	return Object::cast_to<CPUParticles3D>(p_spatial) != nullptr;
}

String CPUParticles3DGizmoPlugin::get_gizmo_name() const {
	return "CPUParticles3D";
}

int CPUParticles3DGizmoPlugin::get_priority() const {
	return -1;
}

bool CPUParticles3DGizmoPlugin::is_selectable_when_hidden() const {
	return true;
}

void CPUParticles3DGizmoPlugin::redraw(EditorEntity3DGizmo *p_gizmo) {
	CPUParticles3D *particles = Object::cast_to<CPUParticles3D>(p_gizmo->get_entity_3d());

	p_gizmo->clear();

	Vector<Vector3> lines;
	AABB aabb = particles->get_visibility_aabb();

	for (int i = 0; i < 12; i++) {
		Vector3 a, b;
		aabb.get_edge(i, a, b);
		lines.push_back(a);
		lines.push_back(b);
	}

	Vector<Vector3> handles;

	for (int i = 0; i < 3; i++) {
		Vector3 ax;
		ax[i] = aabb.position[i] + aabb.size[i];
		ax[(i + 1) % 3] = aabb.position[(i + 1) % 3] + aabb.size[(i + 1) % 3] * 0.5;
		ax[(i + 2) % 3] = aabb.position[(i + 2) % 3] + aabb.size[(i + 2) % 3] * 0.5;
		handles.push_back(ax);
	}

	Vector3 center = aabb.get_center();
	for (int i = 0; i < 3; i++) {
		Vector3 ax;
		ax[i] = 1.0;
		handles.push_back(center + ax);
		lines.push_back(center);
		lines.push_back(center + ax);
	}

	Ref<Material> material = get_material("particles_material", p_gizmo);

	p_gizmo->add_lines(lines, material);

	if (p_gizmo->is_selected()) {
		Ref<Material> solid_material = get_material("particles_solid_material", p_gizmo);
		p_gizmo->add_solid_box(solid_material, aabb.get_size(), aabb.get_center());
	}

	Ref<Material> icon = get_material("particles_icon", p_gizmo);
	p_gizmo->add_unscaled_billboard(icon, 0.05);
}
