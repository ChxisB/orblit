/**************************************************************************/
/*  script_language_extension.cpp                                         */

#include "script_language_extension.h"

#include "core/object/class_registry.h"

ScriptLanguageExtension::ScriptLanguageExtension() {
#ifdef TOOLS_ENABLED
	editor_adapter = memnew(EditorAdapter(this));
#endif // TOOLS_ENABLED
}

ScriptLanguageExtension::~ScriptLanguageExtension() {
#ifdef TOOLS_ENABLED
	memdelete(editor_adapter);
#endif // TOOLS_ENABLED
}

void ScriptExtension::register_members() {
	ORBLIT_VIRTUAL_BIND(_editor_can_reload_from_file);
	ORBLIT_VIRTUAL_BIND(_placeholder_erased, "placeholder");

	ORBLIT_VIRTUAL_BIND(_can_instantiate);
	ORBLIT_VIRTUAL_BIND(_get_base_script);
	ORBLIT_VIRTUAL_BIND(_get_global_name);
	ORBLIT_VIRTUAL_BIND(_inherits_script, "script");

	ORBLIT_VIRTUAL_BIND(_get_instance_base_type);
	ORBLIT_VIRTUAL_BIND(_instance_create, "for_object");
	ORBLIT_VIRTUAL_BIND(_placeholder_instance_create, "for_object");

	ORBLIT_VIRTUAL_BIND(_has_source_code);
	ORBLIT_VIRTUAL_BIND(_get_source_code);

	ORBLIT_VIRTUAL_BIND(_set_source_code, "code");
	ORBLIT_VIRTUAL_BIND(_reload, "keep_state");

	ORBLIT_VIRTUAL_BIND(_get_doc_class_name);
	ORBLIT_VIRTUAL_BIND(_get_documentation);
	ORBLIT_VIRTUAL_BIND(_get_class_icon_path);

	ORBLIT_VIRTUAL_BIND(_has_method, "method");
	ORBLIT_VIRTUAL_BIND(_has_static_method, "method");

	ORBLIT_VIRTUAL_BIND(_get_script_method_argument_count, "method");

	ORBLIT_VIRTUAL_BIND(_get_method_info, "method");

	ORBLIT_VIRTUAL_BIND(_is_tool);
	ORBLIT_VIRTUAL_BIND(_is_valid);
	ORBLIT_VIRTUAL_BIND(_is_abstract);
	ORBLIT_VIRTUAL_BIND(_get_language);

	ORBLIT_VIRTUAL_BIND(_has_script_signal, "signal");
	ORBLIT_VIRTUAL_BIND(_get_script_signal_list);

	ORBLIT_VIRTUAL_BIND(_has_property_default_value, "property");
	ORBLIT_VIRTUAL_BIND(_get_property_default_value, "property");

	ORBLIT_VIRTUAL_BIND(_update_exports);
	ORBLIT_VIRTUAL_BIND(_get_script_method_list);
	ORBLIT_VIRTUAL_BIND(_get_script_property_list);

	ORBLIT_VIRTUAL_BIND(_get_member_line, "member");

	ORBLIT_VIRTUAL_BIND(_get_constants);
	ORBLIT_VIRTUAL_BIND(_get_members);
	ORBLIT_VIRTUAL_BIND(_is_placeholder_fallback_enabled);

	ORBLIT_VIRTUAL_BIND(_get_rpc_config);
}

void ScriptLanguageExtension::register_members() {
	ORBLIT_VIRTUAL_BIND(_get_name);
	ORBLIT_VIRTUAL_BIND(_init);
	ORBLIT_VIRTUAL_BIND(_get_type);
	ORBLIT_VIRTUAL_BIND(_get_extension);
	ORBLIT_VIRTUAL_BIND(_finish);

	ORBLIT_VIRTUAL_BIND(_get_reserved_words);
	ORBLIT_VIRTUAL_BIND(_is_control_flow_keyword, "keyword");
	ORBLIT_VIRTUAL_BIND(_get_comment_delimiters);
	ORBLIT_VIRTUAL_BIND(_get_doc_comment_delimiters);
	ORBLIT_VIRTUAL_BIND(_get_string_delimiters);
	ORBLIT_VIRTUAL_BIND(_make_template, "template", "class_name", "base_class_name");
	ORBLIT_VIRTUAL_BIND(_get_built_in_templates, "object");
	ORBLIT_VIRTUAL_BIND(_is_using_templates);
	ORBLIT_VIRTUAL_BIND(_validate, "script", "path", "validate_functions", "validate_errors", "validate_warnings", "validate_safe_lines");

	ORBLIT_VIRTUAL_BIND(_validate_path, "path");
	ORBLIT_VIRTUAL_BIND(_supports_builtin_mode);
	ORBLIT_VIRTUAL_BIND(_supports_documentation);
	ORBLIT_VIRTUAL_BIND(_can_inherit_from_file);
	ORBLIT_VIRTUAL_BIND(_find_function, "function", "code");
	ORBLIT_VIRTUAL_BIND(_make_function, "class_name", "function_name", "function_args");
	ORBLIT_VIRTUAL_BIND(_can_make_function);
	ORBLIT_VIRTUAL_BIND(_open_in_external_editor, "script", "line", "column");
	ORBLIT_VIRTUAL_BIND(_overrides_external_editor);
	ORBLIT_VIRTUAL_BIND(_preferred_file_name_casing);

	ORBLIT_VIRTUAL_BIND(_complete_code, "code", "path", "owner");
	ORBLIT_VIRTUAL_BIND(_lookup_code, "code", "symbol", "path", "owner");
	ORBLIT_VIRTUAL_BIND(_auto_indent_code, "code", "from_line", "to_line");

	ORBLIT_VIRTUAL_BIND(_add_global_constant, "name", "value");
	ORBLIT_VIRTUAL_BIND(_add_named_global_constant, "name", "value");
	ORBLIT_VIRTUAL_BIND(_remove_named_global_constant, "name");

	ORBLIT_VIRTUAL_BIND(_thread_enter);
	ORBLIT_VIRTUAL_BIND(_thread_exit);
	ORBLIT_VIRTUAL_BIND(_debug_get_error);
	ORBLIT_VIRTUAL_BIND(_debug_get_stack_level_count);

	ORBLIT_VIRTUAL_BIND(_debug_get_stack_level_line, "level");
	ORBLIT_VIRTUAL_BIND(_debug_get_stack_level_function, "level");
	ORBLIT_VIRTUAL_BIND(_debug_get_stack_level_source, "level");
	ORBLIT_VIRTUAL_BIND(_debug_get_stack_level_locals, "level", "max_subitems", "max_depth");
	ORBLIT_VIRTUAL_BIND(_debug_get_stack_level_members, "level", "max_subitems", "max_depth");
	ORBLIT_VIRTUAL_BIND(_debug_get_stack_level_instance, "level");
	ORBLIT_VIRTUAL_BIND(_debug_get_globals, "max_subitems", "max_depth");
	ORBLIT_VIRTUAL_BIND(_debug_parse_stack_level_expression, "level", "expression", "max_subitems", "max_depth");

	ORBLIT_VIRTUAL_BIND(_debug_get_current_stack_info);

	ORBLIT_VIRTUAL_BIND(_reload_all_scripts);
	ORBLIT_VIRTUAL_BIND(_reload_scripts, "scripts", "soft_reload");
	ORBLIT_VIRTUAL_BIND(_reload_tool_script, "script", "soft_reload");

	ORBLIT_VIRTUAL_BIND(_get_public_functions);
	ORBLIT_VIRTUAL_BIND(_get_public_constants);
	ORBLIT_VIRTUAL_BIND(_get_public_annotations);

	ORBLIT_VIRTUAL_BIND(_profiling_start);
	ORBLIT_VIRTUAL_BIND(_profiling_stop);
	ORBLIT_VIRTUAL_BIND(_profiling_set_save_native_calls, "enable");

	ORBLIT_VIRTUAL_BIND(_profiling_get_accumulated_data, "info_array", "info_max");
	ORBLIT_VIRTUAL_BIND(_profiling_get_frame_data, "info_array", "info_max");

	ORBLIT_VIRTUAL_BIND(_frame);

	ORBLIT_VIRTUAL_BIND(_handles_global_class_type, "type");
	ORBLIT_VIRTUAL_BIND(_get_global_class_name, "path");

	BIND_ENUM_CONSTANT(LOOKUP_RESULT_SCRIPT_LOCATION);
	BIND_ENUM_CONSTANT(LOOKUP_RESULT_CLASS);
	BIND_ENUM_CONSTANT(LOOKUP_RESULT_CLASS_CONSTANT);
	BIND_ENUM_CONSTANT(LOOKUP_RESULT_CLASS_PROPERTY);
	BIND_ENUM_CONSTANT(LOOKUP_RESULT_CLASS_METHOD);
	BIND_ENUM_CONSTANT(LOOKUP_RESULT_CLASS_SIGNAL);
	BIND_ENUM_CONSTANT(LOOKUP_RESULT_CLASS_ENUM);
	BIND_ENUM_CONSTANT(LOOKUP_RESULT_CLASS_ANNOTATION);
	BIND_ENUM_CONSTANT(LOOKUP_RESULT_LOCAL_CONSTANT);
	BIND_ENUM_CONSTANT(LOOKUP_RESULT_LOCAL_VARIABLE);
	BIND_ENUM_CONSTANT(LOOKUP_RESULT_MAX);

	BIND_ENUM_CONSTANT(LOCATION_LOCAL);
	BIND_ENUM_CONSTANT(LOCATION_PARENT_MASK);
	BIND_ENUM_CONSTANT(LOCATION_OTHER_USER_CODE);
	BIND_ENUM_CONSTANT(LOCATION_OTHER);

	BIND_ENUM_CONSTANT(CODE_COMPLETION_KIND_CLASS);
	BIND_ENUM_CONSTANT(CODE_COMPLETION_KIND_FUNCTION);
	BIND_ENUM_CONSTANT(CODE_COMPLETION_KIND_SIGNAL);
	BIND_ENUM_CONSTANT(CODE_COMPLETION_KIND_VARIABLE);
	BIND_ENUM_CONSTANT(CODE_COMPLETION_KIND_MEMBER);
	BIND_ENUM_CONSTANT(CODE_COMPLETION_KIND_ENUM);
	BIND_ENUM_CONSTANT(CODE_COMPLETION_KIND_CONSTANT);
	BIND_ENUM_CONSTANT(CODE_COMPLETION_KIND_ENTITY_PATH);
	BIND_ENUM_CONSTANT(CODE_COMPLETION_KIND_FILE_PATH);
	BIND_ENUM_CONSTANT(CODE_COMPLETION_KIND_PLAIN_TEXT);
	BIND_ENUM_CONSTANT(CODE_COMPLETION_KIND_KEYWORD);
	BIND_ENUM_CONSTANT(CODE_COMPLETION_KIND_MAX);
}
