/**************************************************************************/
/*  safe_binary_mutex.h                                                   */

#pragma once

#include "core/error/error_macros.h"
#include "core/os/mutex.h"
#include "core/typedefs.h"

#ifdef THREADS_ENABLED

ORBLIT_CLANG_WARNING_PUSH_AND_IGNORE("-Wundefined-var-template")

// A very special kind of mutex, used in scenarios where these
// requirements hold at the same time:
// - Must be used with a condition variable (only binary mutexes are suitable).
// - Must have recursive semnantics (or simulate, as this one does).
// The implementation keeps the lock count in TS. Therefore, only
// one object of each version of the template can exists; hence the Tag argument.
// Tags must be unique across the Orblit codebase.
// Also, don't forget to declare the thread_local variable on each use.
template <int Tag>
class SafeBinaryMutex {
	friend class MutexLock<SafeBinaryMutex<Tag>>;

	using StdMutexType = THREADING_NAMESPACE::mutex;

	mutable THREADING_NAMESPACE::mutex mutex;

	struct TLSData {
		mutable THREADING_NAMESPACE::unique_lock<THREADING_NAMESPACE::mutex> lock;
		uint32_t count = 0;

		TLSData(SafeBinaryMutex<Tag> &p_mutex) :
				lock(p_mutex.mutex, THREADING_NAMESPACE::defer_lock) {}
	};
	static thread_local TLSData tls_data;

public:
	_ALWAYS_INLINE_ void lock() const {
		if (++tls_data.count == 1) {
			tls_data.lock.lock();
		}
	}

	_ALWAYS_INLINE_ void unlock() const {
		DEV_ASSERT(tls_data.count);
		if (--tls_data.count == 0) {
			tls_data.lock.unlock();
		}
	}

	_ALWAYS_INLINE_ THREADING_NAMESPACE::unique_lock<THREADING_NAMESPACE::mutex> &_get_lock() const {
		return const_cast<THREADING_NAMESPACE::unique_lock<THREADING_NAMESPACE::mutex> &>(tls_data.lock);
	}

	_ALWAYS_INLINE_ SafeBinaryMutex() {
	}

	_ALWAYS_INLINE_ ~SafeBinaryMutex() {
		DEV_ASSERT(!tls_data.count);
	}
};

template <int Tag>
class MutexLock<SafeBinaryMutex<Tag>> {
	friend class ConditionVariable;

	const SafeBinaryMutex<Tag> &mutex;

public:
	explicit MutexLock(const SafeBinaryMutex<Tag> &p_mutex) :
			mutex(p_mutex) {
		mutex.lock();
	}

	~MutexLock() {
		mutex.unlock();
	}

	_ALWAYS_INLINE_ void temp_relock() const {
		mutex.lock();
	}

	_ALWAYS_INLINE_ void temp_unlock() const {
		mutex.unlock();
	}

	// TODO: Implement a `try_temp_relock` if needed (will also need a dummy method below).
};

ORBLIT_CLANG_WARNING_POP

#else // No threads.

template <int Tag>
class SafeBinaryMutex {
	struct TLSData {
		TLSData(SafeBinaryMutex<Tag> &p_mutex) {}
	};
	static thread_local TLSData tls_data;

public:
	void lock() const {}
	void unlock() const {}
};

template <int Tag>
class MutexLock<SafeBinaryMutex<Tag>> {
public:
	MutexLock(const SafeBinaryMutex<Tag> &p_mutex) {}

	void temp_relock() const {}
	void temp_unlock() const {}
};

#endif // THREADS_ENABLED
