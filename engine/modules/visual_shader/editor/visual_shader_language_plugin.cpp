/**************************************************************************/
/*  visual_shader_language_plugin.cpp                                     */

#include "visual_shader_language_plugin.h"

#include "../visual_shader.h"

Ref<Shader> VisualShaderLanguagePlugin::create_new_shader(int p_variation_index, Shader::Mode p_shader_mode, int p_template_index) {
	Ref<VisualShader> shader;
	shader.instantiate();
	shader->set_mode(p_shader_mode);
	return shader;
}
