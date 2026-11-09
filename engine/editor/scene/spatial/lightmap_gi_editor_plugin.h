/**************************************************************************/
/*  lightmap_gi_editor_plugin.h                                           */

#pragma once

#include "editor/plugins/editor_plugin.h"

struct EditorProgress;
class EditorFileDialog;
class LightmapGI;

class LightmapGIEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(LightmapGIEditorPlugin, EditorPlugin);

	LightmapGI *lightmap = nullptr;

	Button *bake = nullptr;

	EditorFileDialog *file_dialog = nullptr;
	static EditorProgress *tmp_progress;
	static bool bake_func_step(float p_progress, const String &p_description, void *, bool p_refresh);
	static void bake_func_end(uint64_t p_time_started);

	void _bake_select_file(const String &p_file);
	void _bake();

protected:
	static void register_members();

public:
	virtual String get_plugin_name() const override { return "LightmapGI"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	LightmapGIEditorPlugin();
};
