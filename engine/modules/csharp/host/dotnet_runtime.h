/**************************************************************************/
/*  dotnet_runtime.h                                                      */

#pragma once
#pragma once

#include "dotnet_host.h"
#include "dotnet_host_cache.h"

// DotNetHost::initialize calls start() once. A runtime outside the engine
// process, such as a browser, implements this as well.
class DotNetRuntime {
public:
#ifdef TOOLS_ENABLED
	using PluginsInitialize = bool (*)(void *, bool, dotnet_host::PluginCallbacks *, DotNetHostCache::ManagedCallbacks *, const void **, int32_t);
#else
	using PluginsInitialize = bool (*)(void *, DotNetHostCache::ManagedCallbacks *, const void **, int32_t);
#endif

	// Starts .NET and returns the managed OrblitPlugins entry point, or null
	// after printing why it failed. Sets r_runtime_initialized when this call
	// started a runtime.
	virtual PluginsInitialize start(bool &r_runtime_initialized) = 0;

	virtual ~DotNetRuntime() = default;
};

// Loads hostfxr, CoreCLR or a native AOT library into the engine process.
class InProcessDotNetRuntime final : public DotNetRuntime {
	void *hostfxr_dll_handle = nullptr;
	void *coreclr_dll_handle = nullptr;

public:
	PluginsInitialize start(bool &r_runtime_initialized) override;

	~InProcessDotNetRuntime() override;
};
