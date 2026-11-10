/**************************************************************************/
/*  rasterizer_util_gles3.h                                               */

#pragma once

#include <cstdint>

// This class is meant to hold static utility methods with minimal dependencies.

class RasterizerUtilGLES3 {
private:
	static bool gles_over_gl;

public:
	static void set_gles_over_gl(bool p_gles_over_gl) {
		gles_over_gl = p_gles_over_gl;
	}
	static bool is_gles_over_gl() {
		return gles_over_gl;
	}

	static void clear_depth(float p_depth);
	static void clear_stencil(int32_t p_stencil);
};
