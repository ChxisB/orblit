/**************************************************************************/
/*  mutex.cpp                                                             */

#include "mutex.h"

static Mutex _global_mutex;

void _global_lock() {
	_global_mutex.lock();
}

void _global_unlock() {
	_global_mutex.unlock();
}

#ifdef THREADS_ENABLED

template class MutexImpl<THREADING_NAMESPACE::recursive_mutex>;
template class MutexImpl<THREADING_NAMESPACE::mutex>;
template class MutexLock<MutexImpl<THREADING_NAMESPACE::recursive_mutex>>;
template class MutexLock<MutexImpl<THREADING_NAMESPACE::mutex>>;

#endif
