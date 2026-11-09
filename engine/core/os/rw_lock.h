/**************************************************************************/
/*  rw_lock.h                                                             */

#pragma once

#include "core/typedefs.h"

#ifdef MINGW_ENABLED
#define MINGW_STDTHREAD_REDUNDANCY_WARNING
#include <thirdparty/mingw-std-threads/mingw.shared_mutex.h>
#define THREADING_NAMESPACE mingw_stdthread
#else
#include <shared_mutex>
#define THREADING_NAMESPACE std
#endif

class RWLock {
	mutable THREADING_NAMESPACE::shared_mutex mutex;

public:
	// Lock the RWLock, block if locked by someone else.
	_ALWAYS_INLINE_ void read_lock() const {
		mutex.lock_shared();
	}

	// Unlock the RWLock, let other threads continue.
	_ALWAYS_INLINE_ void read_unlock() const {
		mutex.unlock_shared();
	}

	// Attempt to lock the RWLock for reading. True on success, false means it can't lock.
	_ALWAYS_INLINE_ bool read_try_lock() const {
		return mutex.try_lock_shared();
	}

	// Lock the RWLock, block if locked by someone else.
	_ALWAYS_INLINE_ void write_lock() {
		mutex.lock();
	}

	// Unlock the RWLock, let other threads continue.
	_ALWAYS_INLINE_ void write_unlock() {
		mutex.unlock();
	}

	// Attempt to lock the RWLock for writing. True on success, false means it can't lock.
	_ALWAYS_INLINE_ bool write_try_lock() {
		return mutex.try_lock();
	}
};

class RWLockRead {
	const RWLock &lock;

public:
	_ALWAYS_INLINE_ RWLockRead(const RWLock &p_lock) :
			lock(p_lock) {
		lock.read_lock();
	}
	_ALWAYS_INLINE_ ~RWLockRead() {
		lock.read_unlock();
	}
};

class RWLockWrite {
	RWLock &lock;

public:
	_ALWAYS_INLINE_ RWLockWrite(RWLock &p_lock) :
			lock(p_lock) {
		lock.write_lock();
	}
	_ALWAYS_INLINE_ ~RWLockWrite() {
		lock.write_unlock();
	}
};
