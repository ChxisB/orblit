/**************************************************************************/
/*  cubemap_filter.h                                                      */

#pragma once

#ifdef GLES3_ENABLED

#include "backends/opengl/shaders/effects/cubemap_filter.glsl.gen.h"

namespace GLES3 {

class CubemapFilter {
private:
	struct CMF {
		CubemapFilterShaderGLES3 shader;
		RID shader_version;
	} cubemap_filter;

	static CubemapFilter *singleton;

	// Use for full-screen effects. Slightly more efficient than screen_quad as this eliminates pixel overdraw along the diagonal.
	GLuint screen_triangle = 0;
	GLuint screen_triangle_array = 0;

	uint32_t ggx_samples = 128;

public:
	static CubemapFilter *get_singleton() {
		return singleton;
	}

	CubemapFilter();
	~CubemapFilter();

	void filter_radiance(GLuint p_source_cubemap, GLuint p_dest_cubemap, GLuint p_dest_framebuffer, int p_source_size, int p_mipmap_count, int p_layer);
};

} //namespace GLES3

#endif // GLES3_ENABLED
