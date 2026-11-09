/**************************************************************************/
/*  script_backtrace.h                                                    */

#pragma once

#include "core/object/ref_counted.h"

class ScriptLanguage;

class ScriptBacktrace : public RefCounted {
	ORBLIT_CLASS(ScriptBacktrace, RefCounted);

	struct StackVariable {
		String name;
		Variant value;
	};

	struct StackFrame {
		LocalVector<StackVariable> local_variables;
		LocalVector<StackVariable> member_variables;
		String function;
		String file;
		int line = 0;
	};

	LocalVector<StackFrame> stack_frames;
	LocalVector<StackVariable> global_variables;
	String language_name;

	static void _store_variables(const List<String> &p_names, const List<Variant> &p_values, LocalVector<StackVariable> &r_variables);

protected:
	static void register_members();

public:
	ScriptBacktrace() = default;
	ScriptBacktrace(ScriptLanguage *p_language, bool p_include_variables = false);

	String get_language_name() const { return language_name; }

	bool is_empty() const { return stack_frames.is_empty(); }
	int get_frame_count() const { return stack_frames.size(); }
	String get_frame_function(int p_index) const;
	String get_frame_file(int p_index) const;
	int get_frame_line(int p_index) const;

	int get_global_variable_count() const { return global_variables.size(); }
	String get_global_variable_name(int p_variable_index) const;
	Variant get_global_variable_value(int p_variable_index) const;

	int get_local_variable_count(int p_frame_index) const;
	String get_local_variable_name(int p_frame_index, int p_variable_index) const;
	Variant get_local_variable_value(int p_frame_index, int p_variable_index) const;

	int get_member_variable_count(int p_frame_index) const;
	String get_member_variable_name(int p_frame_index, int p_variable_index) const;
	Variant get_member_variable_value(int p_frame_index, int p_variable_index) const;

	String format(int p_indent_all = 0, int p_indent_frames = 4) const;
	virtual String _to_string() override { return format(); }
};
