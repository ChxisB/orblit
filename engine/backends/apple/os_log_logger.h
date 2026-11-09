/**************************************************************************/
/*  os_log_logger.h                                                       */

#pragma once

#include "core/io/logger.h"

#include <os/log.h>

/**
 * @brief Apple unified logging system integration for Orblit Engine.
 */
class OsLogLogger : public Logger {
	os_log_t log;
	os_log_t error_log;
	os_log_t warning_log;
	os_log_t script_log;
	os_log_t shader_log;

public:
	void logv(const char *p_format, va_list p_list, bool p_err) override _PRINTF_FORMAT_ATTRIBUTE_2_0;
	void log_error(const char *p_function, const char *p_file, int p_line, const char *p_code, const char *p_rationale, bool p_editor_notify = false, ErrorType p_type = ERR_ERROR, const Vector<Ref<ScriptBacktrace>> &p_script_backtraces = {}) override;

	/**
	 * @brief Constructs an OsLogLogger with the specified subsystem identifier, which is normally the bundle identifier.
	 */
	OsLogLogger(const char *p_subsystem);
};
