/**************************************************************************/
/*  rasterizer_util_gles3.cpp                                             */

#include "rasterizer_util_gles3.h"

#include <platform_gl.h>

bool RasterizerUtilGLES3::gles_over_gl = true;

void RasterizerUtilGLES3::clear_depth(float p_depth) {
#ifdef GL_API_ENABLED
	if (RasterizerUtilGLES3::is_gles_over_gl()) {
		glClearDepth(p_depth);
	}
#endif // GL_API_ENABLED
#ifdef GLES_API_ENABLED
	if (!RasterizerUtilGLES3::is_gles_over_gl()) {
		glClearDepthf(p_depth);
	}
#endif // GLES_API_ENABLED
}

void RasterizerUtilGLES3::clear_stencil(int32_t p_stencil) {
	glClearStencil(p_stencil);
}
