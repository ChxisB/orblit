/**************************************************************************/
/*  dotnet_host.h                                                         */

#pragma once

#include "core/object/object.h"
#include "core/string/ustring.h"

#include <cstdint>

#ifndef ORBLIT_CLR_STDCALL
#ifdef WIN32
#define ORBLIT_CLR_STDCALL __stdcall
#else
#define ORBLIT_CLR_STDCALL
#endif
#endif

namespace dotnet_host {

#ifdef TOOLS_ENABLED
struct PluginCallbacks {
	using FuncLoadProjectAssemblyCallback = bool(ORBLIT_CLR_STDCALL *)(const char16_t *, String *);
	using FuncLoadToolsAssemblyCallback = Object *(ORBLIT_CLR_STDCALL *)(const char16_t *, const void **, int32_t);
	using FuncUnloadProjectPluginCallback = bool(ORBLIT_CLR_STDCALL *)();
	FuncLoadProjectAssemblyCallback LoadProjectAssemblyCallback = nullptr;
	FuncLoadToolsAssemblyCallback LoadToolsAssemblyCallback = nullptr;
	FuncUnloadProjectPluginCallback UnloadProjectPluginCallback = nullptr;
};
#endif

} // namespace dotnet_host

class DotNetRuntime;

class DotNetHost {
	bool initialized = false;
	bool runtime_initialized = false;
	bool finalizing_scripts_domain = false;

	DotNetRuntime *runtime = nullptr;

	String project_assembly_path;
	uint64_t project_assembly_modified_time = 0;
#ifdef ORBLIT_CSHARP_HOT_RELOAD
	int project_load_failure_count = 0;
#endif

#ifdef TOOLS_ENABLED
	bool _load_project_assembly();
	void _try_load_project_assembly();
#endif

#ifdef DEBUG_ENABLED
	uint64_t api_core_hash = 0;
#endif // DEBUG_ENABLED
#ifdef TOOLS_ENABLED
	uint64_t api_editor_hash = 0;
#endif
	void _init_api_hashes();

#ifdef TOOLS_ENABLED
	dotnet_host::PluginCallbacks plugin_callbacks;
#endif

protected:
	static DotNetHost *singleton;

public:
#ifdef DEBUG_ENABLED
	uint64_t get_api_core_hash();
#ifdef TOOLS_ENABLED
	uint64_t get_api_editor_hash();
#endif // TOOLS_ENABLED
#endif // DEBUG_ENABLED

	_FORCE_INLINE_ static String get_expected_api_build_config() {
#ifdef TOOLS_ENABLED
		return "Debug";
#else
#ifdef DEBUG_ENABLED
		return "Debug";
#else
		return "Release";
#endif // DEBUG_ENABLED
#endif
	}

	static DotNetHost *get_singleton() {
		return singleton;
	}

	_FORCE_INLINE_ bool is_initialized() const {
		return initialized;
	}
	_FORCE_INLINE_ bool is_runtime_initialized() const {
		return runtime_initialized;
	}
	_FORCE_INLINE_ bool is_finalizing_scripts_domain() {
		return finalizing_scripts_domain;
	}

	_FORCE_INLINE_ const String &get_project_assembly_path() const {
		return project_assembly_path;
	}
	_FORCE_INLINE_ uint64_t get_project_assembly_modified_time() const {
		return project_assembly_modified_time;
	}

#ifdef TOOLS_ENABLED
	const dotnet_host::PluginCallbacks &get_plugin_callbacks() {
		return plugin_callbacks;
	}
#endif

#ifdef ORBLIT_CSHARP_HOT_RELOAD
	void reload_failure();
	Error reload_project_assemblies();
#endif

	bool should_initialize();

	void initialize();

	// Takes ownership of p_runtime.
	explicit DotNetHost(DotNetRuntime *p_runtime);
	~DotNetHost();
};

namespace ManagedBind {

class OrblitSharp : public Object {
	ORBLIT_CLASS(OrblitSharp, Object);

protected:
	static OrblitSharp *singleton;

public:
	static OrblitSharp *get_singleton() { return singleton; }

	void reload_assemblies();

	OrblitSharp();
	~OrblitSharp();
};

} // namespace ManagedBind
