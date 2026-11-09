/**************************************************************************/
/*  text_shader_language_plugin.h                                         */

#pragma once

#include "editor/shader/editor_shader_language_plugin.h"

class TextShaderLanguagePlugin : public EditorShaderLanguagePlugin {
	ORBLIT_CLASS(TextShaderLanguagePlugin, EditorShaderLanguagePlugin);

public:
	virtual Ref<Shader> create_new_shader(int p_variation_index, Shader::Mode p_shader_mode, int p_template_index) override;
	virtual Ref<ShaderInclude> create_new_shader_include() override;
	virtual PackedStringArray get_language_variations() const override { return { "Shader", "ShaderInclude" }; }
	virtual String get_file_extension(int p_variation_index) const override;
};
