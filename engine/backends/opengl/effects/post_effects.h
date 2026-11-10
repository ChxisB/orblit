/**************************************************************************/
/*  post_effects.h                                                        */

#pragma once

#ifdef GLES3_ENABLED

#include "backends/opengl/effects/glow.h"
#include "backends/opengl/shaders/effects/post.glsl.gen.h"

namespace GLES3 {

class PostEffects {
private:
	struct Post {
		PostShaderGLES3 shader;
		RID shader_version;
	} post;

	static PostEffects *singleton;

	// Use for full-screen effects. Slightly more efficient than screen_quad as this eliminates pixel overdraw along the diagonal.
	GLuint screen_triangle = 0;
	GLuint screen_triangle_array = 0;

	void _draw_screen_triangle();

public:
	static PostEffects *get_singleton();

	PostEffects();
	~PostEffects();

	void post_copy(GLuint p_dest_framebuffer, Size2i p_dest_size, GLuint p_source_color,
			GLuint p_source_depth, bool p_ssao_enabled, int p_ssao_quality_level, float p_ssao_strength, float p_ssao_radius,
			Size2i p_source_size, float p_luminance_multiplier, const Glow::Level *p_glow_buffers, float p_glow_intensity,
			float p_srgb_white, uint32_t p_view = 0, bool p_use_multiview = false, uint64_t p_spec_constants = 0, bool p_bilinear_filtering = true);
};

} //namespace GLES3

#endif // GLES3_ENABLED
