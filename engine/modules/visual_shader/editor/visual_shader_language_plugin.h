/**************************************************************************/
/*  visual_shader_language_plugin.h                                       */

#pragma once

#include "editor/shader/editor_shader_language_plugin.h"

class VisualShaderLanguagePlugin : public EditorShaderLanguagePlugin {
	ORBLIT_CLASS(VisualShaderLanguagePlugin, EditorShaderLanguagePlugin);

public:
	virtual Ref<Shader> create_new_shader(int p_variation_index, Shader::Mode p_shader_mode, int p_template_index) override;
	virtual PackedStringArray get_language_variations() const override { return { "VisualShader" }; }
};
