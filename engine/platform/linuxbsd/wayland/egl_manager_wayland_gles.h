/**************************************************************************/
/*  egl_manager_wayland_gles.h                                            */

#pragma once

#ifdef WAYLAND_ENABLED
#ifdef EGL_ENABLED
#ifdef GLES3_ENABLED

#include "backends/egl/egl_manager.h"

class EGLManagerWaylandGLES : public EGLManager {
public:
	virtual const char *_get_platform_extension_name() const override;
	virtual EGLenum _get_platform_extension_enum() const override;
	virtual EGLenum _get_platform_api_enum() const override;
	virtual Vector<EGLAttrib> _get_platform_display_attributes() const override;
	virtual Vector<EGLint> _get_platform_context_attribs() const override;
};

#endif // GLES3_ENABLED
#endif // EGL_ENABLED
#endif // WAYLAND_ENABLED
