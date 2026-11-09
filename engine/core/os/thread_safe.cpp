/**************************************************************************/
/*  thread_safe.cpp                                                       */

#ifndef THREAD_SAFE_CPP
#define THREAD_SAFE_CPP

#include "thread_safe.h"

static thread_local bool current_thread_safe_for_nodes = false;

bool is_current_thread_safe_for_nodes() {
	return current_thread_safe_for_nodes;
}

void set_current_thread_safe_for_nodes(bool p_safe) {
	current_thread_safe_for_nodes = p_safe;
}

#endif // THREAD_SAFE_CPP
