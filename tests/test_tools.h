/**************************************************************************/
/*  test_tools.h                                                          */

#pragma once

#include "core/error/error_macros.h"

struct ErrorDetector {
	ErrorDetector() {
		eh.errfunc = _detect_error;
		eh.userdata = this;

		add_error_handler(&eh);
	}

	~ErrorDetector() {
		remove_error_handler(&eh);
	}

	void clear() {
		has_error = false;
	}

	static void _detect_error(void *p_self, const char *p_func, const char *p_file, int p_line, const char *p_error, const char *p_errorexp, bool p_editor_notify, ErrorHandlerType p_type) {
		ErrorDetector *self = (ErrorDetector *)p_self;
		self->has_error = true;
	}

	ErrorHandlerList eh;
	bool has_error = false;
};
