/**************************************************************************/
/*  openxr_render_model_manager.h                                         */

#pragma once

#include "modules/modules_enabled.gen.h"

#ifdef MODULE_GLTF_ENABLED

#include "scene/spatial/entity_spatial.h"
#include "systems/xr/xr_positional_tracker.h"

#include <openxr/openxr.h>

class OpenXRRenderModelManager : public Entity3D {
	ORBLIT_CLASS(OpenXRRenderModelManager, Entity3D);

public:
	enum RenderModelTracker {
		RENDER_MODEL_TRACKER_ANY,
		RENDER_MODEL_TRACKER_NONE_SET,
		RENDER_MODEL_TRACKER_LEFT_HAND,
		RENDER_MODEL_TRACKER_RIGHT_HAND,
	};

	virtual PackedStringArray get_configuration_warnings() const override;

	void set_tracker(RenderModelTracker p_tracker);
	RenderModelTracker get_tracker() const;

	void set_make_local_to_pose(const String &p_action);
	String get_make_local_to_pose() const;

private:
	HashMap<RID, Entity3D *> render_models;
	Entity3D *container = nullptr;

	bool is_dirty = false;
	RenderModelTracker tracker = RENDER_MODEL_TRACKER_ANY;
	String make_local_to_pose;

	// cached values
	Ref<XRPositionalTracker> positional_tracker;
	XrPath xr_path = XR_NULL_PATH;

	bool _has_filters();
	void _on_render_model_added(RID p_render_model);
	void _on_render_model_removed(RID p_render_model);
	void _on_render_model_top_level_path_changed(RID p_path);
	void _update_models();

protected:
	static void register_members();

	void _notification(int p_what);
};

VARIANT_ENUM_CAST(OpenXRRenderModelManager::RenderModelTracker);

#endif // MODULE_GLTF_ENABLED
