/**************************************************************************/
/*  collision_shape_spatial_gizmo_plugin.h                                     */

#pragma once

#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"

class Gizmo3DHelper;

class CollisionShape3DGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(CollisionShape3DGizmoPlugin, EditorEntity3DGizmoPlugin);

	void create_collision_material(const String &p_name, float p_alpha);

	Ref<Gizmo3DHelper> helper;

	inline static bool show_only_when_selected = false;

public:
	bool has_gizmo(Entity3D *p_spatial) override;
	String get_gizmo_name() const override;
	int get_priority() const override;
	void redraw(EditorEntity3DGizmo *p_gizmo) override;
	static void set_show_only_when_selected(bool p_enabled);

	String get_handle_name(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const override;
	Variant get_handle_value(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const override;
	void begin_handle_action(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) override;
	void set_handle(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary, Camera3D *p_camera, const Point2 &p_point) override;
	void commit_handle(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary, const Variant &p_restore, bool p_cancel = false) override;

	CollisionShape3DGizmoPlugin();
};
