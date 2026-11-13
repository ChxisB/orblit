/**************************************************************************/
/*  graph_script.h                                                        */

#pragma once

#include "graph_compiled.h"
#include "graph_function.h"

#include "core/containers/hash_set.h"
#include "core/object/script_language.h"

struct GraphVariable {
	StringName name;
	Variant::Type type = Variant::NIL;
	Variant default_value;
	bool exported = false;
	PropertyHint hint = PROPERTY_HINT_NONE;
	String hint_string;
	// The inspector groups exported variables of one category under a heading.
	String category;
	String description;
};

class GraphScriptInstance;

// A script made of functions, each a graph of steps. Every change goes through a method
// here and returns an Error. A failed change leaves the script as it was.
class GraphScript : public Script {
	ORBLIT_CLASS(GraphScript, Script);
	OBJ_SAVE_TYPE(GraphScript);

	Vector<GraphFunction> functions;
	Vector<GraphVariable> variables;
	Vector<GraphSignal> signals;
	// One per function, rebuilt by every change.
	Vector<GraphCompiledFunction> compiled;
	// The first compile error, or empty.
	String error;
	// Empty means the script goes on any object.
	StringName base_type;
	// Every live instance, so a change to the variables reaches them.
	HashSet<GraphScriptInstance *> instances;
#ifdef TOOLS_ENABLED
	HashSet<PlaceHolderScriptInstance *> placeholders;
#endif

	int _find_function_index(const String &p_name) const;
	int _find_variable_index(const StringName &p_name) const;
	int _find_signal_index(const StringName &p_name) const;
	// shortcut: every change recompiles every function. Compile only the changed one when a
	// script grows large enough for editing to lag.
	void _compile();
	Error _commit(Error p_error);
	void _update_instances();
#ifdef TOOLS_ENABLED
	void _update_placeholder(PlaceHolderScriptInstance *p_placeholder) const;
#endif

	template <typename Edit>
	Error _edit_function(const String &p_function, Edit p_edit) {
		const int index = _find_function_index(p_function);
		if (index < 0) {
			return ERR_DOES_NOT_EXIST;
		}
		return _commit(p_edit(functions.write[index]));
	}

protected:
	static void register_members();

public:
	const GraphFunction *get_function(const String &p_name) const;
	const GraphVariable *get_variable(const StringName &p_name) const;
	int get_valid_step_id(const String &p_function) const;
	// Null when the script has no function of that name.
	const GraphCompiledFunction *get_compiled(const StringName &p_function) const;
	const Vector<GraphCompiledFunction> &get_compiled_functions() const { return compiled; }
	const Vector<GraphVariable> &get_declared_variables() const { return variables; }
	const Vector<GraphSignal> &get_declared_signals() const { return signals; }
	const Vector<GraphFunction> &get_declared_functions() const { return functions; }
	// The instance list is kept by the instances themselves.
	void add_instance(GraphScriptInstance *p_instance) { instances.insert(p_instance); }
	void remove_instance(GraphScriptInstance *p_instance) { instances.erase(p_instance); }
	String get_error() const { return error; }

	Error add_function(const String &p_name);
	Error remove_function(const String &p_name);
	Error add_step(const String &p_function, int p_id, const String &p_kind);
	Error remove_step(const String &p_function, int p_id);
	Error move_step(const String &p_function, int p_id, float p_x, float p_y);
	// The value is Variant text, because the editor bridge marshals strings and not variants.
	Error set_step_parameter(const String &p_function, int p_id, const String &p_parameter, const String &p_value_text);
	Error set_step_pins(const String &p_function, int p_id, int p_count);
	// Five parameters, because the editor bridge cannot marshal a struct.
	Error connect_steps(const String &p_function, int p_from_step, int p_from_port, int p_to_step, int p_to_port);
	// Five parameters, because the editor bridge cannot marshal a struct.
	Error disconnect_steps(const String &p_function, int p_from_step, int p_from_port, int p_to_step, int p_to_port);

	// The frame is the text of a dictionary, which JSON also is, because the editor bridge marshals strings.
	// A frame with the same id is replaced.
	Error set_frame(const String &p_function, const String &p_frame_text);
	Error remove_frame(const String &p_function, int p_id);

	// A rename writes the new name into every step that names the old one, in one change. A
	// function, variable or signal that has the new name already is an error.
	Error rename_function(const String &p_name, const String &p_new_name);
	Error rename_variable(const String &p_name, const String &p_new_name);
	Error rename_signal(const String &p_name, const String &p_new_name);

	Error add_variable(const String &p_name, int p_type);
	Error remove_variable(const String &p_name);
	// A default that is not of the new type goes back to the default of that type.
	Error set_variable_type(const String &p_name, int p_type);
	Error set_variable_default(const String &p_name, const String &p_value_text);
	Error set_variable_exported(const String &p_name, bool p_exported);
	// The hint is a PropertyHint number, because the editor bridge marshals numbers and strings.
	Error set_variable_hint(const String &p_name, int p_hint, const String &p_hint_string);
	Error set_variable_info(const String &p_name, const String &p_category, const String &p_description);
	Error add_signal(const String &p_name);
	Error remove_signal(const String &p_name);
	// The text is a list of {name, type} entries, as Variant text.
	Error set_signal_arguments(const String &p_name, const String &p_arguments_text);

	// The three lists below are what a file stores.
	void set_functions(const Array &p_functions);
	Array get_functions() const;
	void set_variables(const Array &p_variables);
	Array get_variables() const;
	void set_signals(const Array &p_signals);
	Array get_signals() const;
	// An empty name lets the script go on any object. A name that is not a class is refused.
	void set_base_type(const StringName &p_base_type);
	StringName get_base_type() const { return base_type; }

	// One entry per warning: kind, function, step, name and text. Answers only, changes nothing.
	Array get_warnings() const;

	// The whole script as JSON text for an editor to draw. See graph_document.h.
	String get_document() const;
	// The steps a palette offers, as JSON text. The side is "", "input" or "output", the side of
	// the port a wire is dragged from, and the sort is "flow" or "data". The type is the type a
	// dragged data wire carries. This asks about the engine and not about this script, and it sits
	// here because the editor bridge calls methods on an object.
	String find_steps(const String &p_text, const String &p_dragged_side, int p_type, const String &p_sort) const;

	virtual void reload_from_file() override;
	virtual bool can_instantiate() const override;
	virtual Ref<Script> get_base_script() const override;
	virtual StringName get_global_name() const override;
	virtual bool inherits_script(const Ref<Script> &p_script) const override;
	virtual StringName get_instance_base_type() const override;
	virtual ScriptInstance *instance_create(Object *p_this) override;
#ifdef TOOLS_ENABLED
	virtual PlaceHolderScriptInstance *placeholder_instance_create(Object *p_this) override;
	virtual void update_exports() override;
#endif
	virtual bool has_source_code() const override;
	virtual String get_source_code() const override;
	virtual void set_source_code(const String &p_code) override;
	virtual Error reload(bool p_keep_state = false) override;

#ifdef TOOLS_ENABLED
	virtual void _placeholder_erased(PlaceHolderScriptInstance *p_placeholder) override;
	virtual StringName get_doc_class_name() const override;
	virtual Vector<DocData::ClassDoc> get_documentation() const override;
	virtual String get_class_icon_path() const override;
#endif // TOOLS_ENABLED

	virtual bool has_method(const StringName &p_method) const override;
	virtual MethodInfo get_method_info(const StringName &p_method) const override;
	virtual bool is_tool() const override;
	virtual bool is_script_valid() const override;
	virtual bool is_abstract() const override;
	virtual ScriptLanguage *get_language() const override;
	virtual bool has_script_signal(const StringName &p_signal) const override;
	virtual void get_script_signal_list(List<MethodInfo> *r_signals) const override;
	virtual bool get_property_default_value(const StringName &p_property, Variant &r_value) const override;
	virtual void get_script_method_list(List<MethodInfo> *r_list) const override;
	virtual void get_script_property_list(List<PropertyInfo> *r_list) const override;
	virtual const Variant get_rpc_config() const override;
};
