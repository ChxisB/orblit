/**************************************************************************/
/*  margin_container_editor_plugin.h                                      */

#pragma once

#include "editor/plugins/editor_plugin.h"
#include "scene/ui/margin_container.h"

class CanvasItemEditor;

class MarginContainerEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(MarginContainerEditorPlugin, EditorPlugin);

	MarginContainer *margin_container = nullptr;

public:
	void forward_canvas_draw_over_viewport(Control *p_viewport_control) override;

	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
};
