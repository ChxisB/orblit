/**************************************************************************/
/*  soft_body_spatial_gizmo_plugin.cpp                                         */

#include "soft_body_spatial_gizmo_plugin.h"

#include "editor/editor_undo_redo_manager.h"
#include "scene/spatial/physics/soft_body_spatial.h"
#include "scene/stage/stage.h"

SoftBody3DGizmoPlugin::SoftBody3DGizmoPlugin() {
	Color gizmo_color = Stage::get_singleton()->get_debug_collisions_color();
	create_material("shape_material", gizmo_color);
	create_handle_material("handles");
}

bool SoftBody3DGizmoPlugin::has_gizmo(Entity3D *p_spatial) {
	return Object::cast_to<SoftBody3D>(p_spatial) != nullptr;
}

String SoftBody3DGizmoPlugin::get_gizmo_name() const {
	return "SoftBody3D";
}

int SoftBody3DGizmoPlugin::get_priority() const {
	return -1;
}

bool SoftBody3DGizmoPlugin::is_selectable_when_hidden() const {
	return true;
}

bool SoftBody3DGizmoPlugin::can_commit_handle_on_click() const {
	return true;
}

void SoftBody3DGizmoPlugin::redraw(EditorEntity3DGizmo *p_gizmo) {
	SoftBody3D *soft_body = Object::cast_to<SoftBody3D>(p_gizmo->get_entity_3d());

	p_gizmo->clear();

	if (!soft_body || soft_body->get_mesh().is_null()) {
		return;
	}

	// find mesh

	Vector<Vector3> lines;

	soft_body->get_mesh()->generate_debug_mesh_lines(lines);

	if (!lines.size()) {
		return;
	}

	Ref<TriangleMesh> tm = soft_body->get_mesh()->generate_triangle_mesh();

	Vector<Vector3> points;
	for (int i = 0; i < soft_body->get_mesh()->get_surface_count(); i++) {
		Array arrays = soft_body->get_mesh()->surface_get_arrays(i);
		ERR_CONTINUE(arrays.is_empty());

		const Vector<Vector3> &vertices = arrays[Mesh::ARRAY_VERTEX];
		points.append_array(vertices);
	}

	Ref<Material> material = get_material("shape_material", p_gizmo);

	p_gizmo->add_lines(lines, material);
	p_gizmo->add_handles(points, get_material("handles"));
	p_gizmo->add_collision_triangles(tm);
}

String SoftBody3DGizmoPlugin::get_handle_name(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const {
	return "SoftBody3D pin point";
}

Variant SoftBody3DGizmoPlugin::get_handle_value(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const {
	SoftBody3D *soft_body = Object::cast_to<SoftBody3D>(p_gizmo->get_entity_3d());
	return Variant(soft_body->is_point_pinned(p_id));
}

void SoftBody3DGizmoPlugin::commit_handle(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary, const Variant &p_restore, bool p_cancel) {
	SoftBody3D *soft_body = Object::cast_to<SoftBody3D>(p_gizmo->get_entity_3d());
	const bool is_pinned = soft_body->is_point_pinned(p_id);

	EditorUndoRedoManager *undo_redo = EditorUndoRedoManager::get_singleton();
	undo_redo->create_action(vformat(is_pinned ? TTR("Remove SoftBody3D pinned point %d") : TTR("Add SoftBody3D pinned point %d"), p_id));
	undo_redo->add_do_method(soft_body, "set_point_pinned", p_id, !is_pinned);
	undo_redo->add_do_method(soft_body, "update_gizmos");
	undo_redo->add_undo_method(soft_body, "set_point_pinned", p_id, is_pinned);
	undo_redo->add_undo_method(soft_body, "update_gizmos");
	undo_redo->commit_action();
}

bool SoftBody3DGizmoPlugin::is_handle_highlighted(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const {
	SoftBody3D *soft_body = Object::cast_to<SoftBody3D>(p_gizmo->get_entity_3d());
	return soft_body->is_point_pinned(p_id);
}
