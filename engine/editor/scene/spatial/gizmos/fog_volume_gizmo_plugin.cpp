/**************************************************************************/
/*  fog_volume_gizmo_plugin.cpp                                           */

#include "fog_volume_gizmo_plugin.h"

#include "editor/editor_node.h"
#include "editor/editor_string_names.h"
#include "editor/scene/spatial/gizmos/gizmo_spatial_helper.h"
#include "editor/settings/editor_settings.h"
#include "scene/spatial/fog_volume.h"

FogVolumeGizmoPlugin::FogVolumeGizmoPlugin() {
	helper.instantiate();
	Color gizmo_color = EDITOR_GET("editors/spatial_gizmos/gizmo_colors/fog_volume");
	create_material("shape_material", gizmo_color);
	gizmo_color.a = 0.15;
	create_material("shape_material_internal", gizmo_color);

	create_icon_material("fog_volume_icon", EditorNode::get_singleton()->get_editor_theme()->get_icon(SNAME("GizmoFogVolume"), EditorStringName(EditorIcons)));

	create_handle_material("handles");
}

bool FogVolumeGizmoPlugin::has_gizmo(Entity3D *p_spatial) {
	return (Object::cast_to<FogVolume>(p_spatial) != nullptr);
}

String FogVolumeGizmoPlugin::get_gizmo_name() const {
	return "FogVolume";
}

int FogVolumeGizmoPlugin::get_priority() const {
	return -1;
}

String FogVolumeGizmoPlugin::get_handle_name(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const {
	return helper->box_get_handle_name(p_id);
}

Variant FogVolumeGizmoPlugin::get_handle_value(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const {
	return Vector3(p_gizmo->get_entity_3d()->call("get_size"));
}

void FogVolumeGizmoPlugin::begin_handle_action(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) {
	helper->initialize_handle_action(get_handle_value(p_gizmo, p_id, p_secondary), p_gizmo->get_entity_3d()->get_global_transform());
}

void FogVolumeGizmoPlugin::set_handle(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary, Camera3D *p_camera, const Point2 &p_point) {
	FogVolume *fog_volume = Object::cast_to<FogVolume>(p_gizmo->get_entity_3d());
	Vector3 size = fog_volume->get_size();

	Vector3 sg[2];
	helper->get_segment(p_camera, p_point, sg);

	Vector3 position;
	helper->box_set_handle(sg, p_id, size, position);
	fog_volume->set_size(size);
	fog_volume->set_global_position(position);
}

void FogVolumeGizmoPlugin::commit_handle(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary, const Variant &p_restore, bool p_cancel) {
	helper->box_commit_handle(TTR("Change FogVolume Size"), p_cancel, p_gizmo->get_entity_3d());
}

void FogVolumeGizmoPlugin::redraw(EditorEntity3DGizmo *p_gizmo) {
	FogVolume *fog_volume = Object::cast_to<FogVolume>(p_gizmo->get_entity_3d());

	p_gizmo->clear();

	if (fog_volume->get_shape() != RSE::FOG_VOLUME_SHAPE_WORLD) {
		const Ref<Material> material =
				get_material("shape_material", p_gizmo);
		const Ref<Material> material_internal =
				get_material("shape_material_internal", p_gizmo);

		Ref<Material> handles_material = get_material("handles");

		Vector<Vector3> lines;
		AABB aabb;
		aabb.size = fog_volume->get_size();
		aabb.position = aabb.size / -2;

		for (int i = 0; i < 12; i++) {
			Vector3 a, b;
			aabb.get_edge(i, a, b);
			lines.push_back(a);
			lines.push_back(b);
		}

		Vector<Vector3> handles = helper->box_get_handles(fog_volume->get_size());

		p_gizmo->add_lines(lines, material);
		p_gizmo->add_collision_segments(lines);
		const Ref<Material> icon = get_material("fog_volume_icon", p_gizmo);
		p_gizmo->add_unscaled_billboard(icon, 0.05);
		p_gizmo->add_handles(handles, handles_material);
	}
}
