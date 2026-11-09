/**************************************************************************/
/*  liborblit_macos.mm                                                    */

#include "os_macos.h"

#include "core/native_extension/instance.h"
#include "core/native_extension/liborblit.h"
#include "startup/main.h"

static OS_MacOS *os = nullptr;

static OrblitInstance *instance = nullptr;

NativeExtensionObjectPtr liborblit_create_instance(int p_argc, char *p_argv[], NativeExtensionInitializationFunction p_init_func) {
	ERR_FAIL_COND_V_MSG(instance != nullptr, nullptr, "Only one Orblit Instance may be created.");

	uint32_t remaining_args = p_argc - 1;
	os = new OS_MacOS_NSApp(p_argv[0], remaining_args, remaining_args > 0 ? &p_argv[1] : nullptr);

	@autoreleasepool {
		Error err = Main::setup(p_argv[0], remaining_args, remaining_args > 0 ? &p_argv[1] : nullptr, false);
		if (err != OK) {
			return nullptr;
		}

		instance = memnew(OrblitInstance);
		if (!instance->initialize(p_init_func)) {
			memdelete(instance);
			instance = nullptr;
			return nullptr;
		}

		return (NativeExtensionObjectPtr)instance;
	}
}

void liborblit_destroy_instance(NativeExtensionObjectPtr p_instance) {
	OrblitInstance *orblit_instance = (OrblitInstance *)p_instance;
	if (instance == orblit_instance) {
		orblit_instance->stop();
		memdelete(orblit_instance);
		// Note: When Orblit Engine supports reinitialization, clear the instance pointer here.
		//instance = nullptr;
		Main::cleanup();
	}
}
