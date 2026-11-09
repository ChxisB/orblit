/**************************************************************************/
/*  light_occluder_planar_editor_plugin.h                                     */

#pragma once

#include "editor/scene/planar/abstract_polygon_planar_editor.h"
#include "scene/planar/light_occluder_planar.h"

class LightOccluder2DEditor : public AbstractPolygon2DEditor {
	ORBLIT_CLASS(LightOccluder2DEditor, AbstractPolygon2DEditor);

	LightOccluder2D *node = nullptr;

	Ref<OccluderPolygon2D> _ensure_occluder() const;

protected:
	virtual Entity2D *_get_node() const override;
	virtual void _set_node(Entity *p_polygon) override;

	virtual bool _is_line() const override;
	virtual int _get_polygon_count() const override;
	virtual Variant _get_polygon(int p_idx) const override;
	virtual void _set_polygon(int p_idx, const Variant &p_polygon) const override;

	virtual void _action_set_polygon(int p_idx, const Variant &p_previous, const Variant &p_polygon) override;

	virtual bool _has_resource() const override;
	virtual void _create_resource() override;
};

class LightOccluder2DEditorPlugin : public AbstractPolygon2DEditorPlugin {
	ORBLIT_CLASS(LightOccluder2DEditorPlugin, AbstractPolygon2DEditorPlugin);

public:
	LightOccluder2DEditorPlugin();
};
