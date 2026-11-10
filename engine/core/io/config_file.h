/**************************************************************************/
/*  config_file.h                                                         */

#pragma once

#include "core/containers/hash_map.h"
#include "core/io/file_access.h"
#include "core/object/ref_counted.h"
#include "core/variant/variant_parser.h"

class ConfigFile : public RefCounted {
	ORBLIT_CLASS(ConfigFile, RefCounted);

	HashMap<String, HashMap<String, Variant>> values;

	Error _internal_load(const String &p_path, Ref<FileAccess> p_file);
	Error _internal_save(Ref<FileAccess> p_file);

	Error _parse(const String &p_path, VariantParser::Stream *p_stream);

protected:
	static void register_members();

public:
	void set_value(const String &p_section, const String &p_key, const Variant &p_value);
	Variant get_value(const String &p_section, const String &p_key, const Variant &p_default = Variant()) const;

	bool has_section(const String &p_section) const;
	bool has_section_key(const String &p_section, const String &p_key) const;

	Vector<String> get_sections() const;
	Vector<String> get_section_keys(const String &p_section) const;

	void erase_section(const String &p_section);
	void erase_section_key(const String &p_section, const String &p_key);

	Error save(const String &p_path);
	Error load(const String &p_path);
	Error parse(const String &p_data);

	String encode_to_text() const; // used by exporter

	void clear();

	Error load_encrypted(const String &p_path, const Vector<uint8_t> &p_key);
	Error load_encrypted_pass(const String &p_path, const String &p_pass);

	Error save_encrypted(const String &p_path, const Vector<uint8_t> &p_key);
	Error save_encrypted_pass(const String &p_path, const String &p_pass);
};
