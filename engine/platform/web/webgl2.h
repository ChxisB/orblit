/**************************************************************************/
/*  webgl2.h                                                              */

#pragma once

#include <GLES3/gl3.h>
#include <webgl/webgl2.h>

#define GL_FRAMEBUFFER_ATTACHMENT_TEXTURE_NUM_VIEWS_OVR 0x9630
#define GL_FRAMEBUFFER_ATTACHMENT_TEXTURE_BASE_VIEW_INDEX_OVR 0x9632
#define GL_MAX_VIEWS_OVR 0x9631
#define GL_FRAMEBUFFER_INCOMPLETE_VIEW_TARGETS_OVR 0x9633

#ifdef __cplusplus
extern "C" {
#endif

void orblit_webgl2_glFramebufferTextureMultiviewOVR(GLenum target, GLenum attachment, GLuint texture, GLint level, GLint baseViewIndex, GLsizei numViews);
void orblit_webgl2_glFramebufferTextureMultisampleMultiviewOVR(GLenum target, GLenum attachment, GLuint texture, GLint level, GLsizei samples, GLint baseViewIndex, GLsizei numViews);
void orblit_webgl2_glGetBufferSubData(GLenum target, GLintptr offset, GLsizeiptr size, GLvoid *data);

#define glFramebufferTextureMultiviewOVR orblit_webgl2_glFramebufferTextureMultiviewOVR
#define glFramebufferTextureMultisampleMultiviewOVR orblit_webgl2_glFramebufferTextureMultisampleMultiviewOVR

#ifdef __cplusplus
}
#endif
