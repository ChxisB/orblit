/**************************************************************************/
/*  class_registry_api_json.cpp                                           */

#include "class_registry_api_json.h"

#ifdef DEBUG_ENABLED

#include "core/config/project_settings.h"
#include "core/io/file_access.h"
#include "core/io/json.h"

void class_registry_api_to_json(const String &p_output_file, ClassRegistry::APIType p_api) {
	Dictionary classes_dict;

	LocalVector<StringName> class_list;
	ClassRegistry::get_class_list(class_list);

	for (const StringName &class_name : class_list) {
		ClassRegistry::ClassInfo *t = ClassRegistry::classes.getptr(class_name);
		ERR_FAIL_NULL(t);
		if (t->api != p_api || !t->exposed) {
			continue;
		}

		Dictionary class_dict;
		classes_dict[t->class_descriptor->get_name()] = class_dict;

		class_dict["inherits"] = t->class_descriptor->get_super_type_name();

		{ //methods

			List<StringName> snames;

			for (const KeyValue<StringName, ClassDescriptor::Member> &F : t->class_descriptor->members(true)) {
				if (F.value.type != ClassDescriptor::Member::Type::METHOD) {
					continue;
				}
				String name = F.key.string();

				ERR_CONTINUE(name.is_empty());

				if (name[0] == '_') {
					continue; // Ignore non-virtual methods that start with an underscore
				}

				snames.push_back(F.key);
			}

			snames.sort_custom<StringName::AlphCompare>();

			Array methods;

			for (const StringName &F : snames) {
				Dictionary method_dict;
				methods.push_back(method_dict);

				const MethodBind *mb = t->class_descriptor->members(true)[F].payload.method;
				method_dict["name"] = mb->get_name();
				method_dict["argument_count"] = mb->get_argument_count();
				method_dict["return_type"] = mb->get_argument_type(-1);

				Array arguments;
				method_dict["arguments"] = arguments;

				for (int i = 0; i < mb->get_argument_count(); i++) {
					Dictionary argument_dict;
					arguments.push_back(argument_dict);
					const PropertyInfo info = mb->get_argument_info(i);
					argument_dict["type"] = info.type;
					argument_dict["name"] = info.name;
					argument_dict["hint"] = info.hint;
					argument_dict["hint_string"] = info.hint_string;
				}

				method_dict["default_argument_count"] = mb->get_default_argument_count();

				Array default_arguments;
				method_dict["default_arguments"] = default_arguments;

				for (int i = 0; i < mb->get_default_argument_count(); i++) {
					Dictionary default_argument_dict;
					default_arguments.push_back(default_argument_dict);
					//hash should not change, i hope for tis
					Variant da = mb->get_default_argument(i);
					default_argument_dict["value"] = da;
				}

				method_dict["hint_flags"] = mb->get_hint_flags();
			}

			if (!methods.is_empty()) {
				class_dict["methods"] = methods;
			}
		}

		{ //constants

			List<StringName> snames;

			for (const KeyValue<StringName, ClassDescriptor::Member> &F : t->class_descriptor->members(true)) {
				if (F.value.type != ClassDescriptor::Member::Type::INTEGER_CONSTANT) {
					continue;
				}
				snames.push_back(F.key);
			}

			snames.sort_custom<StringName::AlphCompare>();

			Array constants;

			for (const StringName &F : snames) {
				Dictionary constant_dict;
				constants.push_back(constant_dict);

				constant_dict["name"] = F;
				constant_dict["value"] = t->class_descriptor->members(true)[F].payload.integer_constant.value;
			}

			if (!constants.is_empty()) {
				class_dict["constants"] = constants;
			}
		}

		{ //signals

			List<StringName> snames;

			for (const KeyValue<StringName, ClassDescriptor::Member> &F : t->class_descriptor->members(true)) {
				if (F.value.type != ClassDescriptor::Member::Type::SIGNAL) {
					continue;
				}
				snames.push_back(F.key);
			}

			snames.sort_custom<StringName::AlphCompare>();

			Array signals;

			for (const StringName &F : snames) {
				Dictionary signal_dict;
				signals.push_back(signal_dict);

				const MethodInfo &mi = *t->class_descriptor->members(true)[F].payload.signal;
				signal_dict["name"] = F;

				Array arguments;
				signal_dict["arguments"] = arguments;
				for (const PropertyInfo &pi : mi.arguments) {
					Dictionary argument_dict;
					arguments.push_back(argument_dict);
					argument_dict["type"] = pi.type;
				}
			}

			if (!signals.is_empty()) {
				class_dict["signals"] = signals;
			}
		}

		{ //properties

			List<StringName> snames;

			for (const KeyValue<StringName, ClassDescriptor::Member> &kv : t->class_descriptor->members(true)) {
				if (kv.value.type == ClassDescriptor::Member::Type::PROPERTY) {
					snames.push_back(kv.key);
				}
			}
			snames.sort_custom<StringName::AlphCompare>();

			Array properties;

			for (const StringName &F : snames) {
				Dictionary property_dict;
				properties.push_back(property_dict);

				const ClassDescriptor::Member &property = t->class_descriptor->members(true)[F];
				const ClassDescriptor::Member::Property &psg = property.payload.property;

				property_dict["name"] = F;
				property_dict["setter"] = psg.setter ? psg.setter->get_name() : StringName();
				property_dict["getter"] = psg.getter ? psg.getter->get_name() : StringName();
			}

			if (!properties.is_empty()) {
				class_dict["property_setget"] = properties;
			}
		}

		Array property_list;

		//property list
		for (const PropertyInfo *F : t->class_descriptor->get_ordered_self_properties()) {
			Dictionary property_dict;
			property_list.push_back(property_dict);

			property_dict["name"] = F->name;
			property_dict["type"] = F->type;
			property_dict["hint"] = F->hint;
			property_dict["hint_string"] = F->hint_string;
			property_dict["usage"] = F->usage;
		}

		if (!property_list.is_empty()) {
			class_dict["property_list"] = property_list;
		}
	}

	Ref<FileAccess> f = FileAccess::open(p_output_file, FileAccess::WRITE);
	ERR_FAIL_COND_MSG(f.is_null(), "Cannot open file '" + p_output_file + "'.");
	f->store_string(JSON::stringify(classes_dict, "\t"));

	print_line(String() + "ClassRegistry API JSON written to: " + ProjectSettings::get_singleton()->globalize_path(p_output_file));
}

#endif // DEBUG_ENABLED
