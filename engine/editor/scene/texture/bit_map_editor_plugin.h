/**************************************************************************/
/*  bit_map_editor_plugin.h                                               */

#pragma once

#include "editor/inspector/editor_inspector.h"
#include "editor/plugins/editor_plugin.h"
#include "scene/resources/bit_map.h"

class AspectRatioContainer;
class TextureRect;

class BitMapEditor : public VBoxContainer {
	ORBLIT_CLASS(BitMapEditor, VBoxContainer);

private:
	AspectRatioContainer *centering_container = nullptr;
	Control *outline_overlay = nullptr;
	TextureRect *texture_rect = nullptr;
	Label *size_label = nullptr;

	Color cached_outline_color;

	void _draw_outline();

protected:
	void _notification(int p_what);

public:
	void setup(const Ref<BitMap> &p_bitmap);

	BitMapEditor();
};

class EditorInspectorPluginBitMap : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorPluginBitMap, EditorInspectorPlugin);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual void parse_begin(Object *p_object) override;
};

class BitMapEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(BitMapEditorPlugin, EditorPlugin);

public:
	BitMapEditorPlugin();
};
