/**************************************************************************/
/*  windows_terminal_logger.h                                             */

#pragma once

#ifdef WINDOWS_ENABLED

#include "core/io/logger.h"

class WindowsTerminalLogger : public StdLogger {
public:
	virtual void logv(const char *p_format, va_list p_list, bool p_err) override;
	virtual void log_error(const char *p_function, const char *p_file, int p_line, const char *p_code, const char *p_rationale, bool p_editor_notify = false, ErrorType p_type = ERR_ERROR, const Vector<Ref<ScriptBacktrace>> &p_script_backtraces = {}) override;
	virtual ~WindowsTerminalLogger();
};

#endif // WINDOWS_ENABLED
