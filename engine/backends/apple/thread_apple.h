/**************************************************************************/
/*  thread_apple.h                                                        */

#pragma once

#include "core/containers/safe_refcount.h"
#include "core/typedefs.h"

#include <pthread.h>

#include <new> // For hardware interference size

class String;

class Thread {
public:
	typedef void (*Callback)(void *p_userdata);

	typedef uint64_t ID;

	enum : ID {
		UNASSIGNED_ID = 0,
		MAIN_ID = 1
	};

	enum Priority {
		PRIORITY_LOW,
		PRIORITY_NORMAL,
		PRIORITY_HIGH
	};

	struct Settings {
		Priority priority;
		Settings() { priority = PRIORITY_NORMAL; }
	};

#if defined(__cpp_lib_hardware_interference_size)
	ORBLIT_GCC_WARNING_PUSH_AND_IGNORE("-Winterference-size")
	static constexpr size_t CACHE_LINE_BYTES = std::hardware_destructive_interference_size;
	ORBLIT_GCC_WARNING_POP
#else
	// At a negligible memory cost, we use a conservatively high value.
	static constexpr size_t CACHE_LINE_BYTES = 128;
#endif

private:
	friend class Main;

	ID id = UNASSIGNED_ID;
	pthread_t pthread;

	static inline SafeFlag is_main_thread_assigned{ false };
	static SafeNumeric<uint64_t> id_counter;
	static thread_local ID caller_id;

	static void *thread_callback(void *p_data);

public:
	_FORCE_INLINE_ static void yield() { pthread_yield_np(); }

	_FORCE_INLINE_ ID get_id() const { return id; }
	// get the ID of the caller thread
	_FORCE_INLINE_ static ID get_caller_id() {
		return caller_id;
	}
	// get the ID of the main thread
	_FORCE_INLINE_ static ID get_main_id() { return MAIN_ID; }

	_FORCE_INLINE_ static bool is_main_thread() { return caller_id == MAIN_ID; }
	static void make_main_thread();
	static void release_main_thread();

	static Error set_name(const String &p_name);

	ID start(Thread::Callback p_callback, void *p_user, const Settings &p_settings = Settings());
	bool is_started() const { return id != UNASSIGNED_ID; }
	/// Waits until thread is finished, and deallocates it.
	void wait_to_finish();

	Thread() = default;
	~Thread();
};
