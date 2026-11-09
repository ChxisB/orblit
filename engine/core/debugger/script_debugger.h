/**************************************************************************/
/*  script_debugger.h                                                     */

#pragma once

#include "core/object/script_language.h"
#include "core/string/string_name.h"
#include "core/containers/hash_set.h"
#include "core/containers/vector.h"

class ScriptDebugger {
	typedef ScriptLanguage::StackInfo StackInfo;

	bool skip_breakpoints = false;
	bool ignore_error_breaks = false;

	HashMap<int, HashSet<StringName>> breakpoints;

	static inline thread_local int lines_left = -1;
	static inline thread_local int depth = -1;
	static inline thread_local ScriptLanguage *break_lang = nullptr;
	static thread_local Vector<StackInfo> error_stack_info;

public:
	void set_lines_left(int p_left);
	_ALWAYS_INLINE_ int get_lines_left() const {
		return lines_left;
	}

	void set_depth(int p_depth);
	_ALWAYS_INLINE_ int get_depth() const {
		return depth;
	}

	String breakpoint_find_source(const String &p_source) const;
	void set_break_language(ScriptLanguage *p_lang) { break_lang = p_lang; }
	ScriptLanguage *get_break_language() { return break_lang; }
	void set_skip_breakpoints(bool p_skip_breakpoints);
	bool is_skipping_breakpoints();
	void set_ignore_error_breaks(bool p_ignore);
	bool is_ignoring_error_breaks();
	void insert_breakpoint(int p_line, const StringName &p_source);
	void remove_breakpoint(int p_line, const StringName &p_source);
	_ALWAYS_INLINE_ bool is_breakpoint(int p_line, const StringName &p_source) const {
		if (likely(!breakpoints.has(p_line))) {
			return false;
		}
		return breakpoints[p_line].has(p_source);
	}
	void clear_breakpoints();
	const HashMap<int, HashSet<StringName>> &get_breakpoints() const { return breakpoints; }

	void debug(ScriptLanguage *p_lang, bool p_can_continue = true, bool p_is_error_breakpoint = false);
	ScriptLanguage *get_break_language() const;

	void send_error(const String &p_func, const String &p_file, int p_line, const String &p_err, const String &p_descr, bool p_editor_notify, ErrorHandlerType p_type, const Vector<StackInfo> &p_stack_info);
	Vector<StackInfo> get_error_stack_info() const;
};
