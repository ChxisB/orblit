/**************************************************************************/
/*  main_loop.cpp                                                         */

#include "main_loop.h"

#include "core/object/class_registry.h"

void MainLoop::register_members() {
	BIND_CONSTANT(NOTIFICATION_OS_MEMORY_WARNING);
	BIND_CONSTANT(NOTIFICATION_TRANSLATION_CHANGED);
	BIND_CONSTANT(NOTIFICATION_WM_ABOUT);
	BIND_CONSTANT(NOTIFICATION_CRASH);
	BIND_CONSTANT(NOTIFICATION_OS_IME_UPDATE);
	BIND_CONSTANT(NOTIFICATION_APPLICATION_RESUMED);
	BIND_CONSTANT(NOTIFICATION_APPLICATION_PAUSED);
	BIND_CONSTANT(NOTIFICATION_APPLICATION_FOCUS_IN);
	BIND_CONSTANT(NOTIFICATION_APPLICATION_FOCUS_OUT);
	BIND_CONSTANT(NOTIFICATION_TEXT_SYSTEM_CHANGED);
	BIND_CONSTANT(NOTIFICATION_APPLICATION_PIP_MODE_ENTERED);
	BIND_CONSTANT(NOTIFICATION_APPLICATION_PIP_MODE_EXITED);

	ADD_SIGNAL(MethodInfo("on_request_permissions_result", PropertyInfo(Variant::STRING, "permission"), PropertyInfo(Variant::BOOL, "granted")));

	ORBLIT_VIRTUAL_BIND(_initialize);
	ORBLIT_VIRTUAL_BIND(_step, "delta");
	ORBLIT_VIRTUAL_BIND(_frame, "delta");
	ORBLIT_VIRTUAL_BIND(_finalize);
}

void MainLoop::initialize() {
	ORBLIT_VIRTUAL_CALL(_initialize);
}

bool MainLoop::physics_process(double p_time) {
	bool quit = false;
	ORBLIT_VIRTUAL_CALL(_step, p_time, quit);
	return quit;
}

bool MainLoop::process(double p_time) {
	bool quit = false;
	ORBLIT_VIRTUAL_CALL(_frame, p_time, quit);
	return quit;
}

void MainLoop::finalize() {
	ORBLIT_VIRTUAL_CALL(_finalize);

	if (get_script_instance()) {
		set_script(Variant()); //clear script
	}
}
