/**************************************************************************/
/*  condition_variable.h                                                  */

#pragma once

#include "core/os/mutex.h"
#include "core/os/safe_binary_mutex.h"

#ifdef THREADS_ENABLED

#ifdef MINGW_ENABLED
#define MINGW_STDTHREAD_REDUNDANCY_WARNING
#include <thirdparty/mingw-std-threads/mingw.condition_variable.h>
#define THREADING_NAMESPACE mingw_stdthread
#else
#include <condition_variable>
#define THREADING_NAMESPACE std
#endif

// An object one or multiple threads can wait on a be notified by some other.
// Normally, you want to use a semaphore for such scenarios, but when the
// condition is something different than a count being greater than zero
// (which is the built-in logic in a semaphore) or you want to provide your
// own mutex to tie the wait-notify to some other behavior, you need to use this.

class ConditionVariable {
	mutable THREADING_NAMESPACE::condition_variable condition;

public:
	template <typename BinaryMutexT>
	_ALWAYS_INLINE_ void wait(const MutexLock<BinaryMutexT> &p_lock) const {
		condition.wait(p_lock._get_lock());
	}

	template <int Tag>
	_ALWAYS_INLINE_ void wait(const MutexLock<SafeBinaryMutex<Tag>> &p_lock) const {
		condition.wait(p_lock.mutex._get_lock());
	}

	_ALWAYS_INLINE_ void notify_one() const {
		condition.notify_one();
	}

	_ALWAYS_INLINE_ void notify_all() const {
		condition.notify_all();
	}
};

#else // No threads.

class ConditionVariable {
public:
	template <typename BinaryMutexT>
	void wait(const MutexLock<BinaryMutexT> &p_lock) const {}
	void notify_one() const {}
	void notify_all() const {}
};

#endif // THREADS_ENABLED
