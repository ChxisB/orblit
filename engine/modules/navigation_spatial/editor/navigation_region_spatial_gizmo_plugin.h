/**************************************************************************/
/*  navigation_region_spatial_gizmo_plugin.h                                   */

#pragma once

#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"

class NavigationRegion3DGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(NavigationRegion3DGizmoPlugin, EditorEntity3DGizmoPlugin);

	struct _EdgeKey {
		Vector3 from;
		Vector3 to;

		static uint32_t hash(const _EdgeKey &p_key) {
			return HashMapHasherDefault::hash(p_key.from) ^ HashMapHasherDefault::hash(p_key.to);
		}

		bool operator==(const _EdgeKey &p_with) const {
			return HashMapComparatorDefault<Vector3>::compare(from, p_with.from) && HashMapComparatorDefault<Vector3>::compare(to, p_with.to);
		}
	};

public:
	bool has_gizmo(Entity3D *p_spatial) override;
	String get_gizmo_name() const override;
	int get_priority() const override;
	void redraw(EditorEntity3DGizmo *p_gizmo) override;

	NavigationRegion3DGizmoPlugin();
};
