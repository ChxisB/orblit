/**************************************************************************/
/*  navigation_obstacle_spatial_editor_plugin.h                                */

#pragma once

#include "editor/plugins/editor_plugin.h"
#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"
#include "scene/ui/box_container.h"

class Button;
class ConfirmationDialog;
class NavigationObstacle3D;

class NavigationObstacle3DGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(NavigationObstacle3DGizmoPlugin, EditorEntity3DGizmoPlugin);

public:
	virtual bool has_gizmo(Entity3D *p_spatial) override;
	virtual String get_gizmo_name() const override;

	virtual void redraw(EditorEntity3DGizmo *p_gizmo) override;

	bool can_be_hidden() const override;
	int get_priority() const override;

	virtual int subgizmos_intersect_ray(const EditorEntity3DGizmo *p_gizmo, Camera3D *p_camera, const Vector2 &p_point) const override;
	virtual Vector<int> subgizmos_intersect_frustum(const EditorEntity3DGizmo *p_gizmo, const Camera3D *p_camera, const Vector<Plane> &p_frustum) const override;
	virtual Transform3D get_subgizmo_transform(const EditorEntity3DGizmo *p_gizmo, int p_id) const override;
	virtual void set_subgizmo_transform(const EditorEntity3DGizmo *p_gizmo, int p_id, Transform3D p_transform) override;
	virtual void commit_subgizmos(const EditorEntity3DGizmo *p_gizmo, const Vector<int> &p_ids, const Vector<Transform3D> &p_restore, bool p_cancel = false) override;

	NavigationObstacle3DGizmoPlugin();
};

class NavigationObstacle3DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(NavigationObstacle3DEditorPlugin, EditorPlugin);

	Ref<NavigationObstacle3DGizmoPlugin> obstacle_3d_gizmo_plugin;

	NavigationObstacle3D *obstacle_node = nullptr;

	Ref<StandardMaterial3D> line_material;
	Ref<StandardMaterial3D> handle_material;

	RID point_lines_mesh_rid;
	RID point_lines_instance_rid;
	RID point_handle_mesh_rid;
	RID point_handles_instance_rid;

public:
	enum Mode {
		MODE_CREATE = 0,
		MODE_EDIT,
		MODE_DELETE,
		ACTION_FLIP,
		ACTION_CLEAR,
	};

private:
	int mode = MODE_EDIT;

	int edited_point = 0;
	Vector3 edited_point_pos;
	Vector<Vector3> pre_move_edit;
	Vector<Vector3> wip_vertices;
	bool wip_active = false;
	bool snap_ignore = false;

	void _wip_close();
	void _wip_cancel();
	void _update_theme();

	Button *button_create = nullptr;
	Button *button_edit = nullptr;
	Button *button_delete = nullptr;
	Button *button_flip = nullptr;
	Button *button_clear = nullptr;

	ConfirmationDialog *button_clear_dialog = nullptr;

protected:
	void _notification(int p_what);
	void _node_removed(Entity *p_node);

public:
	HBoxContainer *obstacle_editor = nullptr;
	static NavigationObstacle3DEditorPlugin *singleton;

	void redraw();

	void set_mode(int p_mode);
	int get_mode() { return mode; }

	void action_flip_vertices();
	void action_clear_vertices();

	virtual EditorPlugin::AfterGUIInput forward_3d_gui_input(Camera3D *p_camera, const Ref<InputEvent> &p_event) override;

	virtual String get_plugin_name() const override { return "NavigationObstacle3DEditor"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	NavigationObstacle3DEditorPlugin();
	~NavigationObstacle3DEditorPlugin();
};
