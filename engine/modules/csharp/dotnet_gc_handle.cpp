/**************************************************************************/
/*  dotnet_gc_handle.cpp                                                  */

#include "dotnet_gc_handle.h"

#include "host/dotnet_host.h"
#include "host/dotnet_host_cache.h"

void DotNetGCHandleData::release() {
#ifdef DEBUG_ENABLED
	CRASH_COND(handle.value && DotNetHost::get_singleton() == nullptr);
#endif

	if (handle.value && DotNetHostCache::orblit_api_cache_updated &&
			DotNetHost::get_singleton()->is_runtime_initialized()) {
		free_gchandle(handle);
		handle.value = nullptr;
	}
}
void DotNetGCHandleData::free_gchandle(GCHandleIntPtr p_gchandle) {
	CRASH_COND(!DotNetHostCache::orblit_api_cache_updated);
	DotNetHostCache::managed_callbacks.GCHandleBridge_FreeGCHandle(p_gchandle);
}
