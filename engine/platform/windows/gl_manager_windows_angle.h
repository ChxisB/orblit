/**************************************************************************/
/*  gl_manager_windows_angle.h                                            */

#pragma once

#if defined(WINDOWS_ENABLED) && defined(GLES3_ENABLED)

#include "backends/egl/egl_manager.h"
#include "systems/display/display_system_enums.h"

#include <windows.h>

class GLManagerANGLE_Windows : public EGLManager {
private:
	virtual const char *_get_platform_extension_name() const override;
	virtual EGLenum _get_platform_extension_enum() const override;
	virtual EGLenum _get_platform_api_enum() const override;
	virtual Vector<EGLAttrib> _get_platform_display_attributes() const override;
	virtual Vector<EGLint> _get_platform_context_attribs() const override;

public:
	void window_resize(DisplaySystemEnums::WindowID p_window_id, int p_width, int p_height);

	GLManagerANGLE_Windows() {}
	~GLManagerANGLE_Windows() {}
};

#endif // WINDOWS_ENABLED && GLES3_ENABLED
