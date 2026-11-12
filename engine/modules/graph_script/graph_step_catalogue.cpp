/**************************************************************************/
/*  graph_step_catalogue.cpp                                              */

#include "graph_step_catalogue.h"

#include "graph_compiled.h"
#include "graph_step_reflect.h"

#include "core/containers/local_vector.h"
#include "core/object/class_registry.h"

namespace {

// How many engine entries one search returns. A palette shows a screenful at most.
constexpr int MAX_ENGINE_ENTRIES = 300;

// Smaller comes first.
enum Rank {
	EXACT,
	PREFIX,
	CONTAINS,
};

struct BuiltinLabel {
	const char *type;
	const char *method;
	const char *label;
};

// shortcut: a few plain names for the builtin methods people look for most. Add a row when
// someone cannot find one, or make the names from the method list.
constexpr BuiltinLabel BUILTIN_LABELS[] = {
	{ "Array", "append", "Add to array" },
	{ "Array", "erase", "Remove from array" },
	{ "Array", "size", "Array size" },
	{ "Array", "clear", "Clear array" },
	{ "Dictionary", "erase", "Remove from dictionary" },
	{ "Dictionary", "has", "Dictionary has key" },
};

// "count_loop" reads "Count loop".
String title(const String &p_text) {
	const String spaced = p_text.replace("_", " ");
	return spaced.left(1).to_upper() + spaced.substr(1);
}

String builtin_label(const String &p_type, const StringName &p_method) {
	for (const BuiltinLabel &row : BUILTIN_LABELS) {
		if (p_type == row.type && p_method == row.method) {
			return row.label;
		}
	}
	return title(p_method);
}

// Says whether an entry matches what was typed and what the dragged wire needs. It only answers.
class Matcher {
	String text;
	Vector<String> words;
	GraphWireFilter filter;

	bool _port_fits(const GraphPort &p_port) const {
		if (p_port.sort != filter.sort) {
			return false;
		}
		if (p_port.sort == GraphPortSort::FLOW) {
			return true;
		}
		const bool from_output = filter.dragged_from == GraphPinSide::OUTPUT;
		const Variant::Type source = from_output ? filter.type : p_port.type;
		const Variant::Type target = from_output ? p_port.type : filter.type;
		return source == Variant::NIL || target == Variant::NIL || Variant::can_convert_strict(source, target);
	}

public:
	Matcher(const String &p_text, const GraphWireFilter &p_filter) :
			text(p_text.strip_edges().to_lower().replace("_", " ")), words(text.split(" ", false)), filter(p_filter) {}

	bool has_text() const { return !text.is_empty(); }

	// Smaller is better, and -1 means no match. Every word must be in the group or the label.
	int get_rank(const String &p_group, const String &p_label) const {
		const String label = p_label.to_lower();
		if (label == text) {
			return EXACT;
		}
		if (words.is_empty() || label.begins_with(text)) {
			return PREFIX;
		}
		const String haystack = p_group.to_lower() + " " + label;
		for (const String &word : words) {
			if (!haystack.contains(word)) {
				return -1;
			}
		}
		return CONTAINS;
	}

	// A wire dragged from an output goes into an input of the new step, and the other way round.
	bool fits(const GraphStepEntry &p_entry) const {
		if (filter.dragged_from == GraphPinSide::NONE) {
			return true;
		}
		const GraphStepKind *kind = GraphStepKinds::find(p_entry.kind);
		ERR_FAIL_NULL_V(kind, false);
		const GraphPinSide side = filter.dragged_from == GraphPinSide::OUTPUT ? GraphPinSide::INPUT : GraphPinSide::OUTPUT;
		const int count = GraphStepKinds::get_port_count(*kind, p_entry.parameters, side);
		for (int i = 0; i < count; i++) {
			if (_port_fits(GraphStepKinds::get_port(*kind, p_entry.parameters, side, i))) {
				return true;
			}
		}
		return false;
	}
};

// Only what compiles is offered. A method a query step must not call is left out.
bool is_usable(const GraphStepEntry &p_entry) {
	const GraphStepKind *kind = GraphStepKinds::find(p_entry.kind);
	ERR_FAIL_NULL_V(kind, false);
	if (!GraphStepKinds::get_ports_problem(*kind, p_entry.parameters).is_empty()) {
		return false;
	}
	GraphCompiledStep step;
	step.kind = kind;
	step.parameters = p_entry.parameters;
	return !kind->resolve || kind->resolve(step, Vector<StringName>()).is_empty();
}

struct Hit {
	int rank = 0;
	GraphStepEntry entry;
};

struct HitOrder {
	bool operator()(const Hit &p_a, const Hit &p_b) const {
		if (p_a.rank != p_b.rank) {
			return p_a.rank < p_b.rank;
		}
		if (p_a.entry.group != p_b.entry.group) {
			return p_a.entry.group < p_b.entry.group;
		}
		return p_a.entry.label < p_b.entry.label;
	}
};

const char *method_kind(const MethodInfo &p_info) {
	if (p_info.flags & METHOD_FLAG_STATIC) {
		return "call_static";
	}
	return (p_info.flags & METHOD_FLAG_CONST) && GraphStepReflect::returns_value(p_info) ? "query_method" : "call_method";
}

// Walks what the engine registers and keeps the entries that match.
class Scan {
	const Matcher &matcher;
	Vector<Hit> hits;

	// The parameters are built only for an entry whose name matches, because most do not.
	template <typename Fill>
	void _offer(const char *p_kind, const String &p_group, const String &p_label, Fill p_fill) {
		const int rank = matcher.get_rank(p_group, p_label);
		if (rank < 0) {
			return;
		}
		GraphStepEntry entry = { p_kind, p_group, p_label, Dictionary() };
		p_fill(entry.parameters);
		if (is_usable(entry) && matcher.fits(entry)) {
			hits.push_back({ rank, entry });
		}
	}

	void _offer_methods(const StringName &p_class) {
		List<MethodInfo> methods;
		ClassRegistry::get_method_list(p_class, &methods, true);
		for (const MethodInfo &info : methods) {
			if (info.name.begins_with("_") || (info.flags & METHOD_FLAG_VIRTUAL)) {
				continue;
			}
			_offer(method_kind(info), p_class, title(info.name), [&](Dictionary &r_parameters) {
				r_parameters["class"] = p_class;
				r_parameters["method"] = info.name;
			});
		}
	}

	void _offer_property(const StringName &p_class, const char *p_kind, const String &p_verb, const String &p_name) {
		_offer(p_kind, p_class, title(p_verb + " " + p_name), [&](Dictionary &r_parameters) {
			r_parameters["class"] = p_class;
			r_parameters["property"] = p_name;
		});
	}

	void _offer_properties(const StringName &p_class) {
		List<PropertyInfo> properties;
		ClassRegistry::get_property_list(p_class, &properties, true);
		for (const PropertyInfo &property : properties) {
			if (!(property.usage & PROPERTY_USAGE_EDITOR) || property.type == Variant::NIL || property.name.contains("/")) {
				continue;
			}
			_offer_property(p_class, "get_property", "get", property.name);
			if (!ClassRegistry::get_property_setter(p_class, property.name).is_empty()) {
				_offer_property(p_class, "set_property", "set", property.name);
			}
		}
	}

	void _offer_signals(const StringName &p_class) {
		List<MethodInfo> signals;
		ClassRegistry::get_signal_list(p_class, &signals, true);
		for (const MethodInfo &info : signals) {
			_offer("emit_on", p_class, title("emit " + info.name), [&](Dictionary &r_parameters) {
				r_parameters["class"] = p_class;
				r_parameters["signal"] = info.name;
			});
		}
	}

	void _offer_new(const StringName &p_class) {
		_offer("new", "Objects", title("new " + String(p_class)), [&](Dictionary &r_parameters) {
			r_parameters["class"] = p_class;
		});
	}

	void _offer_builtin(Variant::Type p_type, const StringName &p_method) {
		const String group = Variant::get_type_name(p_type);
		const MethodInfo info = Variant::get_builtin_method_info(p_type, p_method);
		const bool is_query = (info.flags & (METHOD_FLAG_CONST | METHOD_FLAG_STATIC)) && GraphStepReflect::returns_value(info);
		_offer(is_query ? "query_builtin" : "call_builtin", group, builtin_label(group, p_method), [&](Dictionary &r_parameters) {
			r_parameters["type"] = group;
			r_parameters["method"] = p_method;
		});
	}

	void _offer_builtins() {
		for (int i = Variant::NIL + 1; i < Variant::VARIANT_MAX; i++) {
			// The methods of an object come from its class.
			if (i == Variant::OBJECT) {
				continue;
			}
			List<StringName> methods;
			Variant::get_builtin_method_list(Variant::Type(i), &methods);
			for (const StringName &method : methods) {
				_offer_builtin(Variant::Type(i), method);
			}
		}
	}

	void _offer_utilities() {
		List<StringName> functions;
		Variant::get_utility_function_list(&functions);
		for (const StringName &function : functions) {
			const bool is_query = GraphStepReflect::returns_value(Variant::get_utility_function_info(function));
			_offer(is_query ? "query_utility" : "call_utility", "Utility", title(function), [&](Dictionary &r_parameters) {
				r_parameters["function"] = function;
			});
		}
	}

public:
	explicit Scan(const Matcher &p_matcher) :
			matcher(p_matcher) {}

	// Returns the entries that match, best first.
	// shortcut: every call walks every registered class. Build an index once when the palette lags.
	Vector<GraphStepEntry> run() {
		LocalVector<StringName> classes;
		ClassRegistry::get_class_list(classes);
		for (const StringName &name : classes) {
			_offer_methods(name);
			_offer_properties(name);
			_offer_signals(name);
			_offer_new(name);
		}
		_offer_builtins();
		_offer_utilities();
		hits.sort_custom<HitOrder>();
		Vector<GraphStepEntry> entries;
		for (int i = 0; i < MIN(hits.size(), MAX_ENGINE_ENTRIES); i++) {
			entries.push_back(hits[i].entry);
		}
		return entries;
	}
};

Vector<GraphStepEntry> table_entries(const Matcher &p_matcher) {
	Vector<GraphStepEntry> found;
	for (int i = 0; i < GraphStepKinds::get_kind_count(); i++) {
		const GraphStepKind &kind = GraphStepKinds::get_kind(i);
		// A start step comes with its function or signal. A derived kind comes from the engine scan.
		if (kind.is_start() || kind.derive) {
			continue;
		}
		const GraphStepEntry entry = { kind.name, kind.category, title(kind.name), Dictionary() };
		if (p_matcher.get_rank(entry.group, entry.label) >= 0 && p_matcher.fits(entry)) {
			found.push_back(entry);
		}
	}
	return found;
}

} // namespace

Vector<GraphStepEntry> GraphStepCatalogue::find(const String &p_text, const GraphWireFilter &p_filter) {
	const Matcher matcher(p_text, p_filter);
	Vector<GraphStepEntry> found = table_entries(matcher);
	if (matcher.has_text()) {
		found.append_array(Scan(matcher).run());
	}
	return found;
}
