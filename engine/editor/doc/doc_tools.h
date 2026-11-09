/**************************************************************************/
/*  doc_tools.h                                                           */

#pragma once

#include "core/doc_data.h"
#include "core/containers/rb_set.h"

class XMLParser;

class DocTools {
public:
	String version;
	HashMap<String, DocData::ClassDoc> class_list;
	HashMap<String, RBSet<String, NaturalNoCaseComparator>> inheriting;

	static Error erase_classes(const String &p_dir);

	void merge_from(const DocTools &p_data);
	void add_doc(const DocData::ClassDoc &p_class_doc);
	void remove_doc(const String &p_class_name);
	void remove_script_doc_by_path(const String &p_path);
	bool has_doc(const String &p_class_name);
	enum GenerateFlags {
		GENERATE_FLAG_SKIP_BASIC_TYPES = (1 << 0),
		GENERATE_FLAG_EXTENSION_CLASSES_ONLY = (1 << 1),
	};
	void generate(BitField<GenerateFlags> p_flags = {});
	Error load_classes(const String &p_dir);
	Error save_classes(const String &p_default_path, const HashMap<String, String> &p_class_path, bool p_use_relative_schema = true);

	Error _load(Ref<XMLParser> parser);
	Error load_compressed(const uint8_t *p_data, int64_t p_compressed_size, int64_t p_uncompressed_size);
	Error load_xml(const uint8_t *p_data, int64_t p_size);
};
