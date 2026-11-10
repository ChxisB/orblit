/**************************************************************************/
/*  stack_trace_macos.h                                                   */

#pragma once

#include "core/os/os.h"

#include <dlfcn.h>
#include <mach-o/dyld.h>

namespace StackTraceMacOS {

static void *find_executable_load_address() {
	char full_path[1024];
	uint32_t size = sizeof(full_path);
	if (!_NSGetExecutablePath(full_path, &size)) {
		void *handle = dlopen(full_path, RTLD_LAZY | RTLD_NOLOAD);
		void *addr = dlsym(handle, "main");
		Dl_info info;
		if (dladdr(addr, &info)) {
			return info.dli_fbase;
		}
	}
	return nullptr;
}

static Vector<String> symbolize_with_atos(const String &p_exec_path, const void *p_load_addr, const void *const *p_backtrace_addresses, size_t p_backtrace_size) {
	List<String> args;
	args.push_back("-o");
	args.push_back(p_exec_path);
#if defined(__x86_64) || defined(__x86_64__) || defined(__amd64__)
	args.push_back("-arch");
	args.push_back("x86_64");
#elif defined(__aarch64__) || defined(__arm64__)
	args.push_back("-arch");
	args.push_back("arm64");
#endif
	args.push_back("--fullPath");
	args.push_back("-l");
	// Add load address first, then the backtrace addresses.
	char str[1024];
	snprintf(str, sizeof(str), "%p", p_load_addr);
	args.push_back(String(str));
	for (size_t i = 0; i < p_backtrace_size; i++) {
		snprintf(str, sizeof(str), "%p", p_backtrace_addresses[i]);
		args.push_back(String(str));
	}
	// Execute atos with the arguments and capture the output.
	String atos_output;
	int ret = 0;
	const Error err = OS::get_singleton()->execute("atos", args, &atos_output, &ret);
	if (err == OK && ret == 0 && !atos_output.is_empty()) {
		return atos_output.split("\n", false);
	}
	return Vector<String>();
}

} // namespace StackTraceMacOS
