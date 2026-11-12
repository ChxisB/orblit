/**************************************************************************/
/*  graph_script.h                                                        */

#pragma once

#include "graph_function.h"

#include "core/object/script_language.h"

struct GraphVariable {
	StringName name;
	Variant::Type type = Variant::NIL;
	Variant default_value;
	bool exported = false;
};

// A script made of functions, each a graph of steps. Every change goes through a method
// here and returns an Error. A failed change leaves the script as it was.
class GraphScript : public Script {
	ORBLIT_CLASS(GraphScript, Script);
	OBJ_SAVE_TYPE(GraphScript);

	Vector<GraphFunction> functions;
	Vector<GraphVariable> variables;
	Vector<StringName> signals;

	int _find_function_index(const String &p_name) const;
	int _find_variable_index(const StringName &p_name) const;
	int _find_signal_index(const StringName &p_name) const;
	Error _commit(Error p_error);

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

	Error add_function(const String &p_name);
	Error remove_function(const String &p_name);
	Error add_step(const String &p_function, int p_id, const String &p_kind);
	Error remove_step(const String &p_function, int p_id);
	Error move_step(const String &p_function, int p_id, float p_x, float p_y);
	// The value is Variant text, because the editor bridge marshals strings and not variants.
	Error set_step_parameter(const String &p_function, int p_id, const String &p_parameter, const String &p_value_text);
	// Five parameters, because the editor bridge cannot marshal a struct.
	Error connect_steps(const String &p_function, int p_from_step, int p_from_port, int p_to_step, int p_to_port);
	// Five parameters, because the editor bridge cannot marshal a struct.
	Error disconnect_steps(const String &p_function, int p_from_step, int p_from_port, int p_to_step, int p_to_port);

	Error add_variable(const String &p_name, int p_type);
	Error remove_variable(const String &p_name);
	Error set_variable_default(const String &p_name, const String &p_value_text);
	Error set_variable_exported(const String &p_name, bool p_exported);
	Error add_signal(const String &p_name);
	Error remove_signal(const String &p_name);

	// The three lists below are what a file stores.
	void set_functions(const Array &p_functions);
	Array get_functions() const;
	void set_variables(const Array &p_variables);
	Array get_variables() const;
	void set_signals(const Array &p_signals);
	Array get_signals() const;

	virtual void reload_from_file() override;
	virtual bool can_instantiate() const override;
	virtual Ref<Script> get_base_script() const override;
	virtual StringName get_global_name() const override;
	virtual bool inherits_script(const Ref<Script> &p_script) const override;
	virtual StringName get_instance_base_type() const override;
	virtual ScriptInstance *instance_create(Object *p_this) override;
	virtual bool has_source_code() const override;
	virtual String get_source_code() const override;
	virtual void set_source_code(const String &p_code) override;
	virtual Error reload(bool p_keep_state = false) override;

#ifdef TOOLS_ENABLED
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
