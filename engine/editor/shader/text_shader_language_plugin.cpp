/**************************************************************************/
/*  text_shader_language_plugin.cpp                                       */

#include "text_shader_language_plugin.h"

#include "core/string/string_builder.h"
#include "editor/shader/shader_text_editor.h"
#include "systems/rendering/shader_types.h"

Ref<Shader> TextShaderLanguagePlugin::create_new_shader(int p_variation_index, Shader::Mode p_shader_mode, int p_template_index) {
	Ref<Shader> shader;
	shader.instantiate();

	StringBuilder code;
	const String &shader_type = ShaderTypes::get_singleton()->get_types_list().get(p_shader_mode);
	code += vformat("shader_type %s;\n", shader_type);

	if (p_template_index == 0) { // Default template.
		switch (p_shader_mode) {
			case Shader::MODE_SPATIAL: {
				code += R"(
void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	// Called for every pixel the material is visible on.
}

//void light() {
//	// Called for every pixel for every light affecting the material.
//	// Uncomment to replace the default light processing function with this one.
//}
)";
			} break;
			case Shader::MODE_CANVAS_ITEM: {
				code += R"(
void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	// Called for every pixel the material is visible on.
}

//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
)";
			} break;
			case Shader::MODE_PARTICLES: {
				code += R"(
void start() {
	// Called when a particle is spawned.
}

void process() {
	// Called every frame on existing particles (according to the Fixed FPS property).
}
)";
			} break;
			case Shader::MODE_SKY: {
				code += R"(
void sky() {
	// Called for every visible pixel in the sky background, as well as all pixels
	// in the radiance cubemap.
}
)";
			} break;
			case Shader::MODE_FOG: {
				code += R"(
void fog() {
	// Called once for every froxel that is touched by an axis-aligned bounding box
	// of the associated FogVolume. This means that froxels that just barely touch
	// a given FogVolume will still be used.
}
)";
			} break;
			case Shader::MODE_TEXTURE_BLIT: {
				code += R"(
void blit() {
	// Called for each pixel inside the given rect on the DrawableTexture.
}
)";
			} break;
			case Shader::MODE_MAX: {
				ERR_FAIL_V_MSG(Ref<Shader>(), "Invalid shader mode for text shader editor.");
			} break;
		}
	}
	shader->set_code(code.as_string());
	return shader;
}

Ref<ShaderInclude> TextShaderLanguagePlugin::create_new_shader_include() {
	Ref<ShaderInclude> shader_inc;
	shader_inc.instantiate();
	return shader_inc;
}

String TextShaderLanguagePlugin::get_file_extension(int p_variation_index) const {
	if (p_variation_index == 0) {
		return "oshader";
	} else if (p_variation_index == 1) {
		return "oshaderinc";
	}
	return "ores";
}
