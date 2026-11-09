/**************************************************************************/
/*  dr_bridge.h                                                           */

#include "core/io/file_access.h"
#include "core/os/memory.h"
#include "core/typedefs.h"

typedef struct {
	void *user_data;
	void *(*malloc_func)(size_t size, void *user_data);
	void *(*realloc_func)(void *ptr, size_t size, void *user_data);
	void (*free_func)(void *ptr, void *user_data);
} dr_allocation_callbacks;

static void *dr_memalloc(size_t size, void *user_data) {
	return memalloc(size);
}

static void *dr_memrealloc(void *ptr, size_t size, void *user_data) {
	return memrealloc(ptr, size);
}

static void dr_memfree(void *ptr, void *user_data) {
	if (ptr) {
		memfree(ptr);
	}
}

const dr_allocation_callbacks dr_alloc_calls = {
	nullptr,
	dr_memalloc,
	dr_memrealloc,
	dr_memfree
};
