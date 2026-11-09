/**************************************************************************/
/*  style_box_editor_plugin.h                                             */

#pragma once

#include "editor/inspector/editor_inspector.h"
#include "editor/plugins/editor_plugin.h"
#include "scene/ui/texture_rect.h"

class Button;
class StyleBox;

class StyleBoxPreview : public TextureRect {
	ORBLIT_CLASS(StyleBoxPreview, TextureRect);

	Button *grid_preview = nullptr;
	Ref<StyleBox> stylebox;

	void _sb_changed();
	void _redraw();
	static bool grid_preview_enabled;
	void _grid_preview_toggled(bool p_active);

protected:
	void _notification(int p_what);

public:
	void edit(const Ref<StyleBox> &p_stylebox);

	StyleBoxPreview();
};

class EditorInspectorPluginStyleBox : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorPluginStyleBox, EditorInspectorPlugin);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual void parse_begin(Object *p_object) override;
};

class StyleBoxEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(StyleBoxEditorPlugin, EditorPlugin);

public:
	virtual String get_plugin_name() const override { return "StyleBox"; }

	StyleBoxEditorPlugin();
};
