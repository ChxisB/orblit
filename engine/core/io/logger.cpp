/**************************************************************************/
/*  logger.cpp                                                            */

#include "logger.h"

#include "core/containers/rb_set.h"
#include "core/core_globals.h"
#include "core/io/dir_access.h"
#include "core/io/file_access.h"
#include "core/object/script_backtrace.h"
#include "core/os/time.h"
#include "core/string/regex.h"

#include <cstdio>

#if defined(MINGW_ENABLED) || defined(_MSC_VER)
#define sprintf sprintf_s
#endif

bool Logger::should_log(bool p_err) {
	return (!p_err || CoreGlobals::print_error_enabled) && (p_err || CoreGlobals::print_line_enabled);
}

void Logger::set_flush_stdout_on_print(bool p_value) {
	_flush_stdout_on_print = p_value;
}

void Logger::log_error(const char *p_function, const char *p_file, int p_line, const char *p_code, const char *p_rationale, bool p_editor_notify, ErrorType p_type, const Vector<Ref<ScriptBacktrace>> &p_script_backtraces) {
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

	logf_error("%s: %s\n", err_type, err_details);
	logf_error("   at: %s (%s:%i)\n", p_function, p_file, p_line);

	for (const Ref<ScriptBacktrace> &backtrace : p_script_backtraces) {
		if (!backtrace->is_empty()) {
			logf_error("%s\n", backtrace->format(3).utf8().get_data());
		}
	}
}

void Logger::logf(const char *p_format, ...) {
	if (!should_log(false)) {
		return;
	}

	va_list argp;
	va_start(argp, p_format);

	logv(p_format, argp, false);

	va_end(argp);
}

void Logger::logf_error(const char *p_format, ...) {
	if (!should_log(true)) {
		return;
	}

	va_list argp;
	va_start(argp, p_format);

	logv(p_format, argp, true);

	va_end(argp);
}

// The result is a RBSet because iterating on it is done in sorted order.
// RotatedFileLogger depends on this behavior to delete the oldest log file first.
static RBSet<String> list_backups(const String &p_base_path) {
	String basename = p_base_path.get_file().get_basename();
	String extension = p_base_path.get_extension();

	RBSet<String> backups;
	Ref<DirAccess> da = DirAccess::open(p_base_path.get_base_dir());
	if (da.is_null()) {
		return backups;
	}

	da->list_dir_begin();
	String f = da->get_next();
	while (!f.is_empty()) {
		if (!da->current_is_dir() && f.begins_with(basename) && f.get_extension() == extension && f != p_base_path.get_file()) {
			backups.insert(f);
		}
		f = da->get_next();
	}
	da->list_dir_end();
	return backups;
}

void RotatedFileLogger::clear_old_backups() {
	int max_backups = max_files - 1; // -1 for the current file

	RBSet<String> backups = list_backups(base_path);
	if (backups.size() > max_backups) {
		// since backups are appended with timestamp and Set iterates them in sorted order,
		// first backups are the oldest
		int to_delete = backups.size() - max_backups;
		for (RBSet<String>::Element *E = backups.front(); E && to_delete > 0; E = E->next(), --to_delete) {
			DirAccess::remove_absolute(base_path.get_base_dir().path_join(E->get()));
		}
	}
}

void RotatedFileLogger::rotate_file() {
	file.unref();

	if (FileAccess::exists(base_path)) {
		if (max_files > 1) {
			String timestamp = Time::get_singleton()->get_datetime_string_from_system().replace_char(':', '.');
			String backup_stem = base_path.get_basename() + timestamp;
			String backup_suffix = base_path.get_extension().is_empty() ? String() : "." + base_path.get_extension();
			String backup_name = backup_stem + backup_suffix;
			// The timestamp has one second of resolution. A backup must sort after every kept one, or the next
			// clear deletes it first. Deleting the oldest backup of a second frees its name, so the counter
			// skips to the first name past the newest kept backup.
			RBSet<String> kept = list_backups(base_path);
			String newest = kept.is_empty() ? String() : kept.back()->get();
			for (int repeat = 1; repeat < 1000 && !(newest < backup_name.get_file()); repeat++) {
				backup_name = backup_stem + "_" + itos(repeat).pad_zeros(3) + backup_suffix;
			}

			Ref<DirAccess> da = DirAccess::open(base_path.get_base_dir());
			if (da.is_valid()) {
				da->copy(base_path, backup_name);
			}
			clear_old_backups();
		}
	} else {
		// The path picks the access type, so a relative path is made where the file will open.
		DirAccess::make_dir_recursive_absolute(base_path.get_base_dir());
	}

	file = FileAccess::open(base_path, FileAccess::WRITE);
	ERR_FAIL_COND_MSG(file.is_null(), "Failed to open log file for writing: " + base_path);
	file->detach_from_registry(); // Note: This FileAccess instance will exist longer than ObjectRegistry, therefore can't be registered in ObjectRegistry.
}

RotatedFileLogger::RotatedFileLogger(const String &p_base_path, int p_max_files) :
		base_path(p_base_path.simplify_path()),
		max_files(p_max_files > 0 ? p_max_files : 1) {
	rotate_file();

	strip_ansi_regex.instantiate();
	strip_ansi_regex->detach_from_registry(); // Note: This RegEx instance will exist longer than ObjectRegistry, therefore can't be registered in ObjectRegistry.
	strip_ansi_regex->compile("\u001b\\[((?:\\d|;)*)([a-zA-Z])");
}

void RotatedFileLogger::logv(const char *p_format, va_list p_list, bool p_err) {
	if (!should_log(p_err)) {
		return;
	}

	if (file.is_valid()) {
		const int static_buf_size = 512;
		char static_buf[static_buf_size];
		char *buf = static_buf;
		va_list list_copy;
		va_copy(list_copy, p_list);
		int len = vsnprintf(buf, static_buf_size, p_format, p_list);
		if (len >= static_buf_size) {
			buf = (char *)Memory::alloc_static(len + 1);
			vsnprintf(buf, len + 1, p_format, list_copy);
		}
		va_end(list_copy);

		// Strip ANSI escape codes (such as those inserted by `print_rich()`)
		// before writing to file, as text editors cannot display those
		// correctly.
		file->store_string(strip_ansi_regex->sub(String::utf8(buf), "", true));

		if (len >= static_buf_size) {
			Memory::free_static(buf);
		}

		if (p_err || _flush_stdout_on_print) {
			// Don't always flush when printing stdout to avoid performance
			// issues when `print()` is spammed in release builds.
			file->flush();
		}
	}
}

void StdLogger::logv(const char *p_format, va_list p_list, bool p_err) {
	if (!should_log(p_err)) {
		return;
	}

	if (p_err) {
		vfprintf(stderr, p_format, p_list);
	} else {
		vprintf(p_format, p_list);
		if (_flush_stdout_on_print) {
			// Don't always flush when printing stdout to avoid performance
			// issues when `print()` is spammed in release builds.
			fflush(stdout);
		}
	}
}

CompositeLogger::CompositeLogger(const Vector<Logger *> &p_loggers) :
		loggers(p_loggers) {
}

void CompositeLogger::logv(const char *p_format, va_list p_list, bool p_err) {
	if (!should_log(p_err)) {
		return;
	}

	for (int i = 0; i < loggers.size(); ++i) {
		va_list list_copy;
		va_copy(list_copy, p_list);
		loggers[i]->logv(p_format, list_copy, p_err);
		va_end(list_copy);
	}
}

void CompositeLogger::log_error(const char *p_function, const char *p_file, int p_line, const char *p_code, const char *p_rationale, bool p_editor_notify, ErrorType p_type, const Vector<Ref<ScriptBacktrace>> &p_script_backtraces) {
	if (!should_log(true)) {
		return;
	}

	for (int i = 0; i < loggers.size(); ++i) {
		loggers[i]->log_error(p_function, p_file, p_line, p_code, p_rationale, p_editor_notify, p_type, p_script_backtraces);
	}
}

void CompositeLogger::add_logger(Logger *p_logger) {
	loggers.push_back(p_logger);
}

CompositeLogger::~CompositeLogger() {
	for (int i = 0; i < loggers.size(); ++i) {
		memdelete(loggers[i]);
	}
}
