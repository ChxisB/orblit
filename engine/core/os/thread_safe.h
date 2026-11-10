/**************************************************************************/
/*  thread_safe.h                                                         */

#pragma once

#include "core/os/mutex.h" // IWYU pragma: keep // Used in macro.

#define _THREAD_SAFE_CLASS_ mutable Mutex _thread_safe_;
#define _THREAD_SAFE_METHOD_ MutexLock _thread_safe_method_(_thread_safe_);
#define _THREAD_SAFE_LOCK_ _thread_safe_.lock();
#define _THREAD_SAFE_UNLOCK_ _thread_safe_.unlock();

bool is_current_thread_safe_for_nodes();
void set_current_thread_safe_for_nodes(bool p_safe);
