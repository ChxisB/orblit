/**************************************************************************/
/*  gpu_particles_spatial_gizmo_plugin.cpp                                     */

#include "gpu_particles_spatial_gizmo_plugin.h"

#include "editor/editor_node.h"
#include "editor/editor_string_names.h"
#include "editor/settings/editor_settings.h"
#include "scene/spatial/gpu_particles_spatial.h"

GPUParticles3DGizmoPlugin::GPUParticles3DGizmoPlugin() {
	Color gizmo_color = EDITOR_GET("editors/spatial_gizmos/gizmo_colors/particles");
	create_material("particles_material", gizmo_color);
	gizmo_color.a = MAX((gizmo_color.a - 0.2) * 0.02, 0.0);
	create_icon_material("particles_icon", EditorNode::get_singleton()->get_editor_theme()->get_icon(SNAME("GizmoGPUParticles3D"), EditorStringName(EditorIcons)));
}

bool GPUParticles3DGizmoPlugin::has_gizmo(Entity3D *p_spatial) {
	return Object::cast_to<GPUParticles3D>(p_spatial) != nullptr;
}

String GPUParticles3DGizmoPlugin::get_gizmo_name() const {
	return "GPUParticles3D";
}

int GPUParticles3DGizmoPlugin::get_priority() const {
	return -1;
}

bool GPUParticles3DGizmoPlugin::is_selectable_when_hidden() const {
	return true;
}

void GPUParticles3DGizmoPlugin::redraw(EditorEntity3DGizmo *p_gizmo) {
	p_gizmo->clear();

	if (p_gizmo->is_selected()) {
		GPUParticles3D *particles = Object::cast_to<GPUParticles3D>(p_gizmo->get_entity_3d());

		Vector<Vector3> lines;
		AABB aabb = particles->get_visibility_aabb();

		for (int i = 0; i < 12; i++) {
			Vector3 a, b;
			aabb.get_edge(i, a, b);
			lines.push_back(a);
			lines.push_back(b);
		}

		Ref<Material> material = get_material("particles_material", p_gizmo);

		p_gizmo->add_lines(lines, material);
	}

	Ref<Material> icon = get_material("particles_icon", p_gizmo);
	p_gizmo->add_unscaled_billboard(icon, 0.05);
}
