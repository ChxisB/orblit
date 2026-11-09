/**************************************************************************/
/*  main_ios.mm                                                           */

#import "os_ios.h"

#import "backends/apple_embedded/app_delegate_apple_embedded.h"
#import "backends/apple_embedded/main_utilities.h"
#include "core/profiling/profiling.h"
#include "startup/main.h"

#import <UIKit/UIKit.h>

#include <cstdio>

static OS_IOS *os = nullptr;

int apple_embedded_main(int argc, char **argv) {
#if defined(VULKAN_ENABLED)
	//MoltenVK - enable full component swizzling support
	setenv("MVK_CONFIG_FULL_IMAGE_VIEW_SWIZZLE", "1", 1);
#endif

	change_to_launch_dir(argv);

	os = new OS_IOS();

	// We must override main when testing is enabled
	TEST_MAIN_OVERRIDE

	char *fargv[64];
	argc = process_args(argc, argv, fargv);

	init_profiler();

	Error err = Main::setup(fargv[0], argc - 1, &fargv[1], false);

	if (err != OK) {
		if (err == ERR_HELP) { // Returned by --help and --version, so success.
			return EXIT_SUCCESS;
		}
		return EXIT_FAILURE;
	}

	os->initialize_modules();

	return os->get_exit_code();
}

void apple_embedded_finish() {
	Main::cleanup();
	cleanup_profiler();
	delete os;
}
