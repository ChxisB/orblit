/**************************************************************************/
/*  line_planar_editor_plugin.h                                               */

#pragma once

#include "editor/scene/planar/abstract_polygon_planar_editor.h"
#include "scene/planar/line_planar.h"

class Line2DEditor : public AbstractPolygon2DEditor {
	ORBLIT_CLASS(Line2DEditor, AbstractPolygon2DEditor);

	Line2D *node = nullptr;

protected:
	virtual Entity2D *_get_node() const override;
	virtual void _set_node(Entity *p_line) override;

	virtual bool _is_line() const override;
	virtual Variant _get_polygon(int p_idx) const override;
	virtual void _set_polygon(int p_idx, const Variant &p_polygon) const override;
	virtual void _action_set_polygon(int p_idx, const Variant &p_previous, const Variant &p_polygon) override;
};

class Line2DEditorPlugin : public AbstractPolygon2DEditorPlugin {
	ORBLIT_CLASS(Line2DEditorPlugin, AbstractPolygon2DEditorPlugin);

public:
	Line2DEditorPlugin();
};
