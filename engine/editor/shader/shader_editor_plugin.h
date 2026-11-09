/**************************************************************************/
/*  shader_editor_plugin.h                                                */

#pragma once

#include "editor/script/script_editor_plugin.h"

class ShaderEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(ShaderEditorPlugin, EditorPlugin);

	DocumentEditorContainer *shader_container = nullptr;

	EditorDock *shader_dock = nullptr;
	Ref<Shortcut> make_floating_shortcut;

	const String config_section = "ShaderEditor";

	static inline ShaderEditorPlugin *shader_editor_plugin = nullptr;

protected:
	virtual void shortcut_input(const Ref<InputEvent> &p_event) override;

public:
	static ShaderEditorPlugin *get_singleton() { return shader_editor_plugin; }

	DocumentEditorContainer *get_shader_container() const { return shader_container; }
	EditorDock *get_shader_dock() const { return shader_dock; }

	virtual String get_plugin_name() const override { return "Shader"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;
	virtual void set_current() override;

	virtual void set_window_layout(Ref<ConfigFile> p_layout) override;
	virtual void get_window_layout(Ref<ConfigFile> p_layout) override;

	virtual String get_unsaved_status(const String &p_for_scene) const override;
	virtual void save_external_data() override;
	virtual void apply_changes() override { shader_container->apply_scripts(); }

	ShaderEditorPlugin();
	~ShaderEditorPlugin();
};
