/**************************************************************************/
/*  openxr_render_model.h                                                 */

#pragma once

#include "modules/modules_enabled.gen.h"

#ifdef MODULE_GLTF_ENABLED
#include "scene/spatial/entity_spatial.h"

#include <openxr/openxr.h>

class OpenXRRenderModel : public Entity3D {
	ORBLIT_CLASS(OpenXRRenderModel, Entity3D);

private:
	RID render_model;
	Entity3D *scene = nullptr;
	HashMap<String, Entity3D *> animatable_nodes;

	void _load_render_model_scene();
	void _on_render_model_top_level_path_changed(RID p_render_model);

protected:
	static void register_members();

	void _notification(int p_what);

public:
	virtual PackedStringArray get_configuration_warnings() const override;

	RID get_render_model() const;
	void set_render_model(RID p_render_model);

	String get_top_level_path() const;
};
#endif // MODULE_GLTF_ENABLED
