/**************************************************************************/
/*  plist.h                                                               */

#pragma once

// Property list file format (application/x-plist) parser, property list ASN-1 serialization.

#include "core/io/file_access.h"

class PListNode;

class PList : public RefCounted {
	ORBLIT_SOFTCLASS(PList, RefCounted);

	friend class PListNode;

public:
	enum PLNodeType {
		PL_NODE_TYPE_NIL,
		PL_NODE_TYPE_STRING,
		PL_NODE_TYPE_ARRAY,
		PL_NODE_TYPE_DICT,
		PL_NODE_TYPE_BOOLEAN,
		PL_NODE_TYPE_INTEGER,
		PL_NODE_TYPE_REAL,
		PL_NODE_TYPE_DATA,
		PL_NODE_TYPE_DATE,
	};

private:
	struct PListTrailer {
		uint8_t offset_size;
		uint8_t ref_size;
		uint64_t object_num;
		uint64_t root_offset_idx;
		uint64_t offset_table_start;
	};

	PListTrailer trailer;
	Ref<PListNode> root;

	uint64_t read_bplist_var_size_int(Ref<FileAccess> p_file, uint8_t p_size);
	Ref<PListNode> read_bplist_obj(Ref<FileAccess> p_file, uint64_t p_offset_idx);

public:
	PList();
	PList(const String &p_string);

	bool load_file(const String &p_filename);
	bool load_string(const String &p_string, String &r_err_out);

	PackedByteArray save_asn1() const;
	String save_text() const;

	Ref<PListNode> get_root();
};

/*************************************************************************/

class PListNode : public RefCounted {
	ORBLIT_SOFTCLASS(PListNode, RefCounted);

	static int _asn1_size_len(size_t p_size);
	static void _store_asn1_size(PackedByteArray &p_stream, size_t p_size);
	static uint8_t _get_len_octets(size_t p_size);

	size_t _get_asn1_size() const;

public:
	PList::PLNodeType data_type = PList::PLNodeType::PL_NODE_TYPE_NIL;

	CharString data_string;
	Vector<Ref<PListNode>> data_array;
	HashMap<String, Ref<PListNode>> data_dict;
	union {
		int64_t data_int;
		bool data_bool;
		double data_real;
	};

	PList::PLNodeType get_type() const;
	Variant get_value() const;

	static Ref<PListNode> new_node(const Variant &p_value);
	static Ref<PListNode> new_array();
	static Ref<PListNode> new_dict();
	static Ref<PListNode> new_string(const String &p_string);
	static Ref<PListNode> new_data(const String &p_string);
	static Ref<PListNode> new_date(const String &p_string);
	static Ref<PListNode> new_bool(bool p_bool);
	static Ref<PListNode> new_int(int64_t p_int);
	static Ref<PListNode> new_real(double p_real);

	bool push_subnode(const Ref<PListNode> &p_node, const String &p_key = "");

	bool store_asn1(PackedByteArray &p_stream) const;
	void store_text(String &p_stream, uint8_t p_indent) const;
};
