/**************************************************************************/
/*  sub_viewport_preview_editor_plugin.h                                  */

#pragma once

#include "editor/plugins/editor_plugin.h"
#include "editor/scene/texture/texture_editor_plugin.h"

class EditorInspectorPluginSubViewportPreview : public EditorInspectorPluginTexture {
	ORBLIT_CLASS(EditorInspectorPluginSubViewportPreview, EditorInspectorPluginTexture);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual void parse_begin(Object *p_object) override;
};

class SubViewportPreviewEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(SubViewportPreviewEditorPlugin, EditorPlugin);

public:
	virtual String get_plugin_name() const override { return "SubViewportPreview"; }

	SubViewportPreviewEditorPlugin();
};
