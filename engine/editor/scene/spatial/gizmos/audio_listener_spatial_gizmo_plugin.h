/**************************************************************************/
/*  audio_listener_spatial_gizmo_plugin.h                                      */

#pragma once

#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"

class AudioListener3DGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(AudioListener3DGizmoPlugin, EditorEntity3DGizmoPlugin);

public:
	bool has_gizmo(Entity3D *p_spatial) override;
	String get_gizmo_name() const override;
	int get_priority() const override;

	void redraw(EditorEntity3DGizmo *p_gizmo) override;

	AudioListener3DGizmoPlugin();
};
