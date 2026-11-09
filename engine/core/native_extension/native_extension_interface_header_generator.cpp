/**************************************************************************/
/*  native_extension_interface_header_generator.cpp                       */

#ifdef TOOLS_ENABLED

#include "native_extension_interface_header_generator.h"

#include "core/native_extension/native_extension_interface_dump.gen.h"
#include "core/io/json.h"

static const char *FILE_HEADER =
		"/**************************************************************************/\n"
		"/*  native_extension_interface.h                                               */\n";

static const char *INTRO =
		"\n"
		"#pragma once\n"
		"\n"
		"/* This is a C class header, you can copy it and use it directly in your own binders.\n"
		" * Together with the `extension_api.json` file, you should be able to generate any binder.\n"
		" */\n"
		"\n"
		"#ifndef __cplusplus\n"
		"#include <stddef.h>\n"
		"#include <stdint.h>\n"
		"\n"
		"typedef uint32_t char32_t;\n"
		"typedef uint16_t char16_t;\n"
		"#else\n"
		"#include <cstddef>\n"
		"#include <cstdint>\n"
		"\n"
		"extern \"C\" {\n"
		"#endif\n"
		"\n";

static const char *OUTRO =
		"#ifdef __cplusplus\n"
		"}\n"
		"#endif\n";

void NativeExtensionInterfaceHeaderGenerator::generate_native_extension_interface_header(const String &p_path) {
	Ref<FileAccess> fa = FileAccess::open(p_path, FileAccess::WRITE);
	ERR_FAIL_COND_MSG(fa.is_null(), vformat("Cannot open file '%s' for writing.", p_path));

	Vector<uint8_t> bytes = NativeExtensionInterfaceDump::load_native_extension_interface_file();
	String json_string = String::utf8(Span<char>((char *)bytes.ptr(), bytes.size()));

	Ref<JSON> json;
	json.instantiate();
	Error err = json->parse(json_string);
	ERR_FAIL_COND(err);

	Dictionary data = json->get_data();
	ERR_FAIL_COND(data.is_empty());

	fa->store_string(FILE_HEADER);

	Array copyright = data["_copyright"];
	for (const Variant &line : copyright) {
		fa->store_line(line);
	}

	fa->store_string(INTRO);

	Array types = data["types"];
	for (Dictionary type_dict : types) {
		if (type_dict.has("description")) {
			write_doc(fa, type_dict["description"]);
		}
		String kind = type_dict["kind"];
		if (kind == "handle") {
			type_dict["type"] = type_dict.get("is_const", false) ? "const void*" : "void*";
			write_simple_type(fa, type_dict);
		} else if (kind == "alias") {
			write_simple_type(fa, type_dict);
		} else if (kind == "enum") {
			write_enum_type(fa, type_dict);
		} else if (kind == "function") {
			write_function_type(fa, type_dict);
		} else if (kind == "struct") {
			write_struct_type(fa, type_dict);
		}
	}

	Array interfaces = data["interface"];
	for (const Variant &interface : interfaces) {
		write_interface(fa, interface);
	}

	fa->store_string(OUTRO);
}

void NativeExtensionInterfaceHeaderGenerator::write_doc(const Ref<FileAccess> &p_fa, const Array &p_doc, const String &p_indent) {
	if (p_doc.size() == 1) {
		p_fa->store_string(vformat("%s/* %s */\n", p_indent, p_doc[0]));
		return;
	}

	bool first = true;
	for (const Variant &line : p_doc) {
		if (first) {
			p_fa->store_string(p_indent + "/*");
			first = false;
		} else {
			p_fa->store_string(p_indent + " *");
		}

		if (line == "") {
			p_fa->store_string("\n");
		} else {
			p_fa->store_line(String(" ") + (String)line);
		}
	}

	p_fa->store_string(p_indent + " */\n");
}

void NativeExtensionInterfaceHeaderGenerator::write_simple_type(const Ref<FileAccess> &p_fa, const Dictionary &p_type) {
	String type_and_name = format_type_and_name(p_type["type"], p_type["name"]);
	p_fa->store_string(vformat("typedef %s;\n", type_and_name));
}

void NativeExtensionInterfaceHeaderGenerator::write_enum_type(const Ref<FileAccess> &p_fa, const Dictionary &p_enum) {
	p_fa->store_string("typedef enum {\n");
	Array values = p_enum["values"];
	for (Dictionary value_dict : values) {
		if (value_dict.has("description")) {
			write_doc(p_fa, value_dict["description"], "\t");
		}
		p_fa->store_string(vformat("\t%s = %s,\n", value_dict["name"], (int)value_dict["value"]));
	}
	p_fa->store_string(vformat("} %s;\n\n", p_enum["name"]));
}

void NativeExtensionInterfaceHeaderGenerator::write_function_type(const Ref<FileAccess> &p_fa, const Dictionary &p_func) {
	String args_text = p_func.has("arguments") ? make_args_text(p_func["arguments"]) : "";
	String name_and_args = vformat("(*%s)(%s)", p_func["name"], args_text);
	String return_type;
	if (p_func.has("return_value")) {
		Dictionary ret = p_func["return_value"];
		return_type = ret["type"];
	} else {
		return_type = "void";
	}
	p_fa->store_string(vformat("typedef %s;\n", format_type_and_name(return_type, name_and_args)));
}

void NativeExtensionInterfaceHeaderGenerator::write_struct_type(const Ref<FileAccess> &p_fa, const Dictionary &p_struct) {
	p_fa->store_string("typedef struct {\n");
	Array members = p_struct["members"];
	for (Dictionary member_dict : members) {
		if (member_dict.has("description")) {
			write_doc(p_fa, member_dict["description"], "\t");
		}
		p_fa->store_string(vformat("\t%s;\n", format_type_and_name(member_dict["type"], member_dict["name"])));
	}
	p_fa->store_string(vformat("} %s;\n\n", p_struct["name"]));
}

String NativeExtensionInterfaceHeaderGenerator::format_type_and_name(const String &p_type, const String &p_name) {
	String ret = p_type;
	bool is_pointer = false;
	if (ret.ends_with("*")) {
		ret = ret.substr(0, ret.size() - 2) + " *";
		is_pointer = true;
	}
	if (!p_name.is_empty()) {
		if (is_pointer) {
			ret = ret + p_name;
		} else {
			ret = ret + " " + p_name;
		}
	}
	return ret;
}

String NativeExtensionInterfaceHeaderGenerator::make_args_text(const Array &p_args) {
	Vector<String> combined;
	for (Dictionary arg_dict : p_args) {
		combined.push_back(format_type_and_name(arg_dict["type"], arg_dict.get("name", String())));
	}
	return String(", ").join(combined);
}

void NativeExtensionInterfaceHeaderGenerator::write_interface(const Ref<FileAccess> &p_fa, const Dictionary &p_interface) {
	Vector<String> doc;

	doc.push_back(String("@name ") + (String)p_interface["name"]);
	doc.push_back(String("@since ") + (String)p_interface["since"]);

	Array orig_doc = p_interface["description"];
	for (int i = 0; i < orig_doc.size(); i++) {
		// Put an empty line before the 1st and 2nd lines.
		if (i <= 1) {
			doc.push_back("");
		}
		doc.push_back(orig_doc[i]);
	}

	if (p_interface.has("arguments")) {
		Array args = p_interface["arguments"];
		if (args.size() > 0) {
			doc.push_back("");
			for (Dictionary arg_dict : args) {
				String arg_string = String("@param ") + (String)arg_dict["name"];
				if (arg_dict.has("description")) {
					Array arg_doc = arg_dict["description"];
					for (const Variant &d : arg_doc) {
						arg_string += String(" ") + (String)d;
					}
				}
				doc.push_back(arg_string);
			}
		}
	}

	if (p_interface.has("return_value")) {
		Dictionary ret = p_interface["return_value"];
		String ret_string = String("@return");
		if (ret.has("description")) {
			Array arg_doc = ret["description"];
			for (const Variant &d : arg_doc) {
				ret_string += String(" ") + (String)d;
			}
		}
		doc.push_back("");
		doc.push_back(ret_string);
	}

	if (p_interface.has("see")) {
		Array see_array = p_interface["see"];
		if (see_array.size() > 0) {
			doc.push_back("");
			for (const Variant &see : see_array) {
				doc.push_back(String("@see ") + (String)see);
			}
		}
	}

	p_fa->store_string("/**\n");
	for (const String &d : doc) {
		if (d == "") {
			p_fa->store_string(" *\n");
		} else {
			p_fa->store_string(vformat(" * %s\n", d));
		}
	}
	p_fa->store_string(" */\n");

	Dictionary func = p_interface.duplicate();
	if (p_interface.has("legacy_type_name")) {
		// @todo When we can break compat, remove this! This maintains legacy type-o's in some type names.
		func["name"] = p_interface["legacy_type_name"];
	} else {
		// Cannot use `to_pascal_case()` because it'll capitalize after numbers.
		Vector<String> words = ((String)p_interface["name"]).split("_");
		for (String &word : words) {
			// Cannot use `capitalize()` on the whole string, because it'll separate numbers with a space.
			word[0] = String::char_uppercase(word[0]);
		}
		func["name"] = String("NativeExtensionInterface") + String().join(words);
	}
	write_function_type(p_fa, func);

	p_fa->store_string("\n");
}

#endif // TOOLS_ENABLED
