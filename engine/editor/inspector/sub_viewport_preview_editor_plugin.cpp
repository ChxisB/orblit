/**************************************************************************/
/*  sub_viewport_preview_editor_plugin.cpp                                */

#include "sub_viewport_preview_editor_plugin.h"

#include "core/object/callable_mp.h"
#include "scene/stage/viewport.h"

bool EditorInspectorPluginSubViewportPreview::can_handle(Object *p_object) {
	return Object::cast_to<SubViewport>(p_object) != nullptr;
}

void EditorInspectorPluginSubViewportPreview::parse_begin(Object *p_object) {
	SubViewport *sub_viewport = Object::cast_to<SubViewport>(p_object);

	TexturePreview *sub_viewport_preview = memnew(TexturePreview(sub_viewport->get_texture(), false));
	// Otherwise `sub_viewport_preview`'s `texture_display` doesn't update properly when `sub_viewport`'s size changes.
	sub_viewport->connect("size_changed", callable_mp((CanvasItem *)sub_viewport_preview->get_texture_display(), &CanvasItem::queue_redraw));
	add_custom_control(sub_viewport_preview);
}

SubViewportPreviewEditorPlugin::SubViewportPreviewEditorPlugin() {
	Ref<EditorInspectorPluginSubViewportPreview> plugin;
	plugin.instantiate();
	add_inspector_plugin(plugin);
}
