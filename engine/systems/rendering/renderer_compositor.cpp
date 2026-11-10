/**************************************************************************/
/*  renderer_compositor.cpp                                               */

#include "renderer_compositor.h"

#ifndef XR_DISABLED
#include "core/config/project_settings.h"
#include "systems/xr/xr_system.h"
#endif // XR_DISABLED

RendererCompositor *RendererCompositor::singleton = nullptr;

RendererCompositor *(*RendererCompositor::_create_func)() = nullptr;
bool RendererCompositor::low_end = false;

RendererCompositor *RendererCompositor::create() {
	return _create_func();
}

bool RendererCompositor::is_xr_enabled() const {
	return xr_enabled;
}

RendererCompositor::RendererCompositor() {
	ERR_FAIL_COND_MSG(singleton != nullptr, "A RendererCompositor singleton already exists.");
	singleton = this;

#ifndef XR_DISABLED
	if (XRSystem::get_xr_mode() == XRSystem::XRMODE_DEFAULT) {
		xr_enabled = GLOBAL_GET("xr/shaders/enabled");
	} else {
		xr_enabled = XRSystem::get_xr_mode() == XRSystem::XRMODE_ON;
	}
#endif // XR_DISABLED
}

RendererCompositor::~RendererCompositor() {
	singleton = nullptr;
}
