/**************************************************************************/
/*  dotnet_gc_handle.h                                                    */

#pragma once

#include "core/typedefs.h"

#ifdef DEBUG_ENABLED
#include "core/error/error_macros.h"
#endif

namespace dotnet_host {

enum class GCHandleType : char {
	NIL,
	STRONG_HANDLE,
	WEAK_HANDLE
};
}

extern "C" {
struct GCHandleIntPtr {
	void *value;

	_FORCE_INLINE_ bool operator==(const GCHandleIntPtr &p_other) { return value == p_other.value; }
	_FORCE_INLINE_ bool operator!=(const GCHandleIntPtr &p_other) { return value != p_other.value; }

	GCHandleIntPtr() = delete;
};
}

static_assert(sizeof(GCHandleIntPtr) == sizeof(void *));

// Manual release of the GC handle must be done when using this struct
struct DotNetGCHandleData {
	GCHandleIntPtr handle = { nullptr };
	dotnet_host::GCHandleType type = dotnet_host::GCHandleType::NIL;

	_FORCE_INLINE_ bool is_released() const { return !handle.value; }
	_FORCE_INLINE_ bool is_weak() const { return type == dotnet_host::GCHandleType::WEAK_HANDLE; }
	_FORCE_INLINE_ GCHandleIntPtr get_intptr() const { return handle; }

	void release();

	static void free_gchandle(GCHandleIntPtr p_gchandle);

	void operator=(const DotNetGCHandleData &p_other) {
#ifdef DEBUG_ENABLED
		CRASH_COND(!is_released());
#endif
		handle = p_other.handle;
		type = p_other.type;
	}

	DotNetGCHandleData(const DotNetGCHandleData &) = default;

	DotNetGCHandleData() {}

	DotNetGCHandleData(GCHandleIntPtr p_handle, dotnet_host::GCHandleType p_type) :
			handle(p_handle),
			type(p_type) {
	}
};
