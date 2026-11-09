/**************************************************************************/
/*  syslog_logger.cpp                                                     */

#ifdef UNIX_ENABLED

#include "syslog_logger.h"

#include <syslog.h>

void SyslogLogger::logv(const char *p_format, va_list p_list, bool p_err) {
	if (!should_log(p_err)) {
		return;
	}

	vsyslog(p_err ? LOG_ERR : LOG_INFO, p_format, p_list);
}

void SyslogLogger::print_error(const char *p_function, const char *p_file, int p_line, const char *p_code, const char *p_rationale, ErrorType p_type) {
	if (!should_log(true)) {
		return;
	}

	const char *err_type = error_type_string(p_type);

	const char *err_details;
	if (p_rationale && *p_rationale) {
		err_details = p_rationale;
	} else {
		err_details = p_code;
	}

	syslog(p_type == ERR_WARNING ? LOG_WARNING : LOG_ERR, "**%s**: %s\n   At: %s:%i:%s() - %s", err_type, err_details, p_file, p_line, p_function, p_code);
}

SyslogLogger::~SyslogLogger() {
}

#endif
