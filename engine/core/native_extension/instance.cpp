/**************************************************************************/
/*  instance.cpp                                                          */

#include "instance.h"

#include "core/native_extension/native_extension_manager.h"
#include "core/object/class_registry.h"
#include "core/os/main_loop.h"
#include "core/os/os.h"
#include "startup/main.h"
#include "systems/display/display_system.h"

void OrblitInstance::register_members() {
	ClassRegistry::bind_method(D_METHOD("start"), &OrblitInstance::start);
	ClassRegistry::bind_method(D_METHOD("is_started"), &OrblitInstance::is_started);
	ClassRegistry::bind_method(D_METHOD("iteration"), &OrblitInstance::iteration);
	ClassRegistry::bind_method(D_METHOD("focus_in"), &OrblitInstance::focus_in);
	ClassRegistry::bind_method(D_METHOD("focus_out"), &OrblitInstance::focus_out);
	ClassRegistry::bind_method(D_METHOD("pause"), &OrblitInstance::pause);
	ClassRegistry::bind_method(D_METHOD("resume"), &OrblitInstance::resume);
}

OrblitInstance::OrblitInstance() {
}

OrblitInstance::~OrblitInstance() {
}

bool OrblitInstance::initialize(NativeExtensionInitializationFunction p_init_func) {
	print_verbose("Orblit Instance initialization");
	NativeExtensionManager *native_extension_manager = NativeExtensionManager::get_singleton();
	NativeExtensionPtr<const NativeExtensionInitializationFunction> ptr((const NativeExtensionInitializationFunction *)&p_init_func);
	NativeExtensionManager::LoadStatus status = native_extension_manager->load_extension_from_function("liborblit://main", ptr);
	return status == NativeExtensionManager::LoadStatus::LOAD_STATUS_OK;
}

bool OrblitInstance::start() {
	print_verbose("OrblitInstance::start()");
	Error err = Main::setup2();
	if (err != OK) {
		return false;
	}
	started = Main::start() == EXIT_SUCCESS;
	if (started) {
		OS::get_singleton()->get_main_loop()->initialize();
	}
	return started;
}

bool OrblitInstance::is_started() {
	return started;
}

bool OrblitInstance::iteration() {
	DisplaySystem::get_singleton()->process_events();
	return Main::iteration();
}

void OrblitInstance::stop() {
	print_verbose("OrblitInstance::stop()");
	if (started) {
		OS::get_singleton()->get_main_loop()->finalize();
	}
	started = false;
}

void OrblitInstance::focus_out() {
	print_verbose("OrblitInstance::focus_out()");
	if (started) {
		if (OS::get_singleton()->get_main_loop()) {
			OS::get_singleton()->get_main_loop()->notification(MainLoop::NOTIFICATION_APPLICATION_FOCUS_OUT);
		}
	}
}

void OrblitInstance::focus_in() {
	print_verbose("OrblitInstance::focus_in()");
	if (started) {
		if (OS::get_singleton()->get_main_loop()) {
			OS::get_singleton()->get_main_loop()->notification(MainLoop::NOTIFICATION_APPLICATION_FOCUS_IN);
		}
	}
}

void OrblitInstance::pause() {
	print_verbose("OrblitInstance::pause()");
	if (started) {
		if (OS::get_singleton()->get_main_loop()) {
			OS::get_singleton()->get_main_loop()->notification(MainLoop::NOTIFICATION_APPLICATION_PAUSED);
		}
	}
}

void OrblitInstance::resume() {
	print_verbose("OrblitInstance::resume()");
	if (started) {
		if (OS::get_singleton()->get_main_loop()) {
			OS::get_singleton()->get_main_loop()->notification(MainLoop::NOTIFICATION_APPLICATION_RESUMED);
		}
	}
}
