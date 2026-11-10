/**************************************************************************/
/*  liborblit_linuxbsd.cpp                                                */

#include "os_linuxbsd.h"

#include "core/native_extension/instance.h"
#include "core/native_extension/liborblit.h"
#include "startup/main.h"

static OS_LinuxBSD *os = nullptr;

static OrblitInstance *instance = nullptr;

NativeExtensionObjectPtr liborblit_create_instance(int p_argc, char *p_argv[], NativeExtensionInitializationFunction p_init_func) {
	ERR_FAIL_COND_V_MSG(instance != nullptr, nullptr, "Only one Orblit Instance may be created.");

	os = new OS_LinuxBSD();

	Error err = Main::setup(p_argv[0], p_argc - 1, &p_argv[1], false);
	if (err != OK) {
		return nullptr;
	}

	instance = memnew(OrblitInstance);
	if (!instance->initialize(p_init_func)) {
		memdelete(instance);
		// Note: When Orblit Engine supports reinitialization, clear the instance pointer here.
		//instance = nullptr;
		return nullptr;
	}

	return (NativeExtensionObjectPtr)instance;
}

void liborblit_destroy_instance(NativeExtensionObjectPtr p_instance) {
	OrblitInstance *orblit_instance = (OrblitInstance *)p_instance;
	if (instance == orblit_instance) {
		orblit_instance->stop();
		memdelete(orblit_instance);
		instance = nullptr;
		Main::cleanup();
	}
}
