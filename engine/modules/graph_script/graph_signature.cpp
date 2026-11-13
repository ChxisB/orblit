/**************************************************************************/
/*  graph_signature.cpp                                                   */

#include "graph_signature.h"

namespace {

bool is_text(const Variant &p_value) {
	return p_value.get_type() == Variant::STRING || p_value.get_type() == Variant::STRING_NAME;
}

bool is_type_number(const Variant &p_value) {
	return p_value.get_type() == Variant::INT && int(p_value) >= Variant::NIL && int(p_value) < Variant::VARIANT_MAX;
}

String read_entry(const Variant &p_value, int p_index, const Vector<GraphTyped> &p_before, GraphTyped &r_entry) {
	if (p_value.get_type() != Variant::DICTIONARY) {
		return vformat("entry %d is not a dictionary.", p_index);
	}
	const Dictionary entry = p_value;
	const Variant name = entry.get("name", Variant());
	const Variant type = entry.get("type", Variant());
	if (!is_text(name) || !String(name).is_valid_unicode_identifier() || !is_type_number(type)) {
		return vformat("entry %d needs a 'name' that is an identifier and a 'type' number.", p_index);
	}
	for (const GraphTyped &other : p_before) {
		if (other.name == StringName(String(name))) {
			return vformat("'%s' is declared twice.", String(name));
		}
	}
	r_entry = { String(name), Variant::Type(int(type)) };
	return String();
}

String read_type(const Dictionary &p_parameters, const char *p_key, Variant::Type &r_type) {
	const Variant value = p_parameters.get(p_key, Variant());
	if (value.get_type() == Variant::NIL) {
		return String();
	}
	if (!is_type_number(value)) {
		return vformat("'%s' needs a type number.", p_key);
	}
	r_type = Variant::Type(int(value));
	return String();
}

String read_names(const Variant &p_value, Vector<StringName> &r_names) {
	if (p_value.get_type() == Variant::NIL) {
		return String();
	}
	if (p_value.get_type() != Variant::ARRAY) {
		return "needs a list of names.";
	}
	const Array list = p_value;
	for (const Variant &name : list) {
		if (!is_text(name)) {
			return "needs a list of names.";
		}
		r_names.push_back(String(name));
	}
	return String();
}

String read_listener(const Dictionary &p_parameters, GraphSignature &r_signature) {
	const Variant signal = p_parameters.get("signal", Variant());
	const Variant target = p_parameters.get("target", Variant());
	if (!is_text(signal) || String(signal).is_empty()) {
		return "the step needs a 'signal' parameter.";
	}
	if (target.get_type() != Variant::NIL && !is_text(target)) {
		return "'target' needs a path as text.";
	}
	r_signature.signal = String(signal);
	r_signature.target = target.get_type() == Variant::NIL ? String() : String(target);
	return String();
}

struct RpcKey {
	const char *name;
	Variant::Type type;
	int default_value;
	int max;
};

// The values are the multiplayer's: rpc_mode 2 is authority, transfer_mode 2 is reliable.
constexpr RpcKey RPC_KEYS[] = {
	{ "rpc_mode", Variant::INT, 2, 2 },
	{ "transfer_mode", Variant::INT, 2, 2 },
	{ "call_local", Variant::BOOL, 0, 1 },
	{ "channel", Variant::INT, 0, INT32_MAX },
};

String read_rpc(const Variant &p_value, Dictionary &r_rpc) {
	if (p_value.get_type() == Variant::NIL) {
		return String();
	}
	if (p_value.get_type() != Variant::DICTIONARY) {
		return "'rpc' needs a dictionary.";
	}
	const Dictionary settings = p_value;
	for (const Variant &key : settings.keys()) {
		bool known = false;
		for (const RpcKey &row : RPC_KEYS) {
			known = known || key == Variant(row.name);
		}
		if (!known) {
			return vformat("'rpc' has no setting '%s'.", String(key));
		}
	}
	Dictionary rpc;
	for (const RpcKey &row : RPC_KEYS) {
		const Variant fallback = row.type == Variant::BOOL ? Variant(row.default_value != 0) : Variant(row.default_value);
		const Variant value = settings.get(row.name, fallback);
		const bool fits = value.get_type() == row.type && int(value) >= 0 && int(value) <= row.max;
		if (!fits) {
			return vformat("'rpc' has a bad value for '%s'.", row.name);
		}
		rpc[row.name] = value;
	}
	r_rpc = rpc;
	return String();
}

} // namespace

const GraphTyped *GraphSignature::find_local(const StringName &p_name) const {
	for (const GraphTyped &local : locals) {
		if (local.name == p_name) {
			return &local;
		}
	}
	return nullptr;
}

String GraphSignatures::parse_typed_list(const Variant &p_value, Vector<GraphTyped> &r_list) {
	r_list.clear();
	if (p_value.get_type() == Variant::NIL) {
		return String();
	}
	if (p_value.get_type() != Variant::ARRAY) {
		return "needs a list of entries with a name and a type.";
	}
	const Array list = p_value;
	for (int i = 0; i < list.size(); i++) {
		GraphTyped entry;
		const String problem = read_entry(list[i], i, r_list, entry);
		if (!problem.is_empty()) {
			return problem;
		}
		r_list.push_back(entry);
	}
	return String();
}

Array GraphSignatures::typed_list_to_array(const Vector<GraphTyped> &p_list) {
	Array result;
	for (const GraphTyped &entry : p_list) {
		Dictionary item;
		item["name"] = String(entry.name);
		item["type"] = int(entry.type);
		result.push_back(item);
	}
	return result;
}

String GraphSignatures::read(const GraphStepKind &p_kind, const Dictionary &p_parameters, GraphSignature &r_signature) {
	GraphSignature signature;
	String problem = parse_typed_list(p_parameters.get("arguments", Variant()), signature.arguments);
	if (!problem.is_empty()) {
		return "arguments: " + problem;
	}
	problem = parse_typed_list(p_parameters.get("locals", Variant()), signature.locals);
	if (!problem.is_empty()) {
		return "locals: " + problem;
	}
	problem = read_type(p_parameters, "returns", signature.returns);
	if (problem.is_empty()) {
		problem = read_names(p_parameters.get("ignored_warnings", Variant()), signature.ignored_warnings);
	}
	if (problem.is_empty()) {
		problem = read_rpc(p_parameters.get("rpc", Variant()), signature.rpc);
	}
	if (!problem.is_empty()) {
		return problem;
	}
	signature.description = is_text(p_parameters.get("description", Variant())) ? String(p_parameters["description"]) : String();
	if (StringName("signal_received") == p_kind.name) {
		problem = read_listener(p_parameters, signature);
	}
	if (problem.is_empty()) {
		r_signature = signature;
	}
	return problem;
}

String GraphSignatures::derive_start(const Dictionary &p_parameters, GraphDerivedPorts &r_ports) {
	Vector<GraphTyped> arguments;
	const String problem = parse_typed_list(p_parameters.get("arguments", Variant()), arguments);
	if (!problem.is_empty()) {
		return "arguments: " + problem;
	}
	for (const GraphTyped &argument : arguments) {
		r_ports.outputs.push_back({ String(argument.name), GraphPortSort::DATA, argument.type });
	}
	return String();
}

bool GraphSignatures::convert(Variant::Type p_type, const Variant &p_value, Variant &r_converted) {
	if (p_type == Variant::NIL || p_value.get_type() == p_type) {
		r_converted = p_value;
		return true;
	}
	if (!Variant::can_convert_strict(p_value.get_type(), p_type)) {
		return false;
	}
	const Variant *args[1] = { &p_value };
	Callable::CallError error;
	Variant::construct(p_type, r_converted, args, 1, error);
	return error.error == Callable::CallError::CALL_OK;
}

Variant GraphSignatures::make_default(Variant::Type p_type) {
	Variant value;
	Callable::CallError error;
	Variant::construct(p_type, value, nullptr, 0, error);
	return value;
}
