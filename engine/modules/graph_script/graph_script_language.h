/**************************************************************************/
/*  graph_script_language.h                                               */

#pragma once

#include "core/object/script_language.h"
#ifdef TOOLS_ENABLED
#include "core/object/editor_language.h"
#endif

// The language of graph scripts. It reads no source text, so most of the editor and debugger
// hooks answer with nothing.
class GraphScriptLanguage final : public ScriptLanguage {
	static GraphScriptLanguage *singleton;

	String last_error;
#ifdef TOOLS_ENABLED
	EditorLanguage editor_language;
#endif

public:
	static GraphScriptLanguage *get_singleton() { return singleton; }

	GraphScriptLanguage();
	~GraphScriptLanguage();

	// Keeps the text for debug_get_error and prints it.
	void report_error(const String &p_text);
	// Keeps the text for debug_get_error without printing it. A stop at a breakpoint is no error.
	void set_break_reason(const String &p_reason) { last_error = p_reason; }

	virtual String get_name() const override { return "GraphScript"; }
	virtual void init() override {}
	virtual String get_type() const override { return "GraphScript"; }
	virtual String get_extension() const override { return "ograph"; }
	virtual void finish() override {}

#ifdef TOOLS_ENABLED
	virtual EditorLanguage *get_editor_language() override { return &editor_language; }
#endif

	virtual Vector<String> get_reserved_words() const override { return Vector<String>(); }
	virtual bool is_control_flow_keyword(const String &p_string) const override { return false; }
	virtual Vector<String> get_comment_delimiters() const override { return Vector<String>(); }
	virtual Vector<String> get_doc_comment_delimiters() const override { return Vector<String>(); }
	virtual Vector<String> get_string_delimiters() const override { return Vector<String>(); }
	virtual bool supports_builtin_mode() const override { return true; }
	virtual bool supports_documentation() const override { return true; }
	virtual bool is_using_templates() override { return true; }
	virtual Vector<ScriptTemplate> get_built_in_templates(const StringName &p_object) override;
	virtual Ref<Script> make_template(const String &p_template, const String &p_class_name, const String &p_base_class_name) const override;
	virtual bool can_make_function() const override { return false; }
	virtual String make_function(const String &p_class, const String &p_name, const PackedStringArray &p_args) const override { return String(); }
	virtual void add_global_constant(const StringName &p_variable, const Variant &p_value) override {}

	virtual String debug_get_error() const override { return last_error; }
	virtual int debug_get_stack_level_count() const override;
	virtual int debug_get_stack_level_line(int p_level) const override;
	virtual String debug_get_stack_level_function(int p_level) const override;
	virtual String debug_get_stack_level_source(int p_level) const override;
	virtual void debug_get_stack_level_locals(int p_level, List<String> *r_locals, List<Variant> *r_values, int p_max_subitems = -1, int p_max_depth = -1) override;
	virtual void debug_get_stack_level_members(int p_level, List<String> *r_members, List<Variant> *r_values, int p_max_subitems = -1, int p_max_depth = -1) override;
	virtual ScriptInstance *debug_get_stack_level_instance(int p_level) override;
	virtual void debug_get_globals(List<String> *r_globals, List<Variant> *r_values, int p_max_subitems = -1, int p_max_depth = -1) override {}
	virtual String debug_parse_stack_level_expression(int p_level, const String &p_expression, int p_max_subitems = -1, int p_max_depth = -1) override { return String(); }

	virtual void reload_all_scripts() override {}
	virtual void reload_scripts(const Array &p_scripts) override;
	virtual void reload_tool_script(const Ref<Script> &p_script) override;

	virtual void get_public_functions(List<MethodInfo> *r_functions) const override {}
	virtual void get_public_constants(List<Pair<String, Variant>> *r_constants) const override {}
	virtual void get_public_annotations(List<MethodInfo> *r_annotations) const override {}

	virtual void profiling_start() override {}
	virtual void profiling_stop() override {}
	virtual void profiling_set_save_native_calls(bool p_enable) override {}
	virtual int profiling_get_accumulated_data(ProfilingInfo *r_info_arr, int p_info_max) override { return 0; }
	virtual int profiling_get_frame_data(ProfilingInfo *r_info_arr, int p_info_max) override { return 0; }
};
