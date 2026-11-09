/**************************************************************************/
/*  soft_body_spatial_gizmo_plugin.h                                           */

#pragma once

#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"

class SoftBody3DGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(SoftBody3DGizmoPlugin, EditorEntity3DGizmoPlugin);

public:
	bool has_gizmo(Entity3D *p_spatial) override;
	String get_gizmo_name() const override;
	int get_priority() const override;
	bool is_selectable_when_hidden() const override;
	bool can_commit_handle_on_click() const override;
	void redraw(EditorEntity3DGizmo *p_gizmo) override;

	String get_handle_name(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const override;
	Variant get_handle_value(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const override;
	void commit_handle(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary, const Variant &p_restore, bool p_cancel = false) override;
	bool is_handle_highlighted(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const override;

	SoftBody3DGizmoPlugin();
};
