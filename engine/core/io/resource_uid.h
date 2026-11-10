/**************************************************************************/
/*  resource_uid.h                                                        */

#pragma once

#include "core/containers/hash_map.h"
#include "core/object/object.h"
#include "core/string/ustring.h"

class FileAccess;

typedef void (*ResourceUIDScanForUIDOnStartup)();

class ResourceUID : public Object {
	ORBLIT_CLASS(ResourceUID, Object)
public:
	typedef int64_t ID;
	constexpr const static ID INVALID_ID = -1;

	static String get_cache_file();

private:
	Mutex mutex;
	struct Cache {
		CharString cs;
		bool saved_to_cache = false;
	};

	HashMap<ID, Cache> unique_ids; // Unique IDs and utf8 paths (less memory used).
#ifdef TOOLS_ENABLED
	HashMap<ID, Cache> unique_ids_copy; // Copy of the cache during filesystem scan.
#endif
	bool use_reverse_cache = false;
	HashMap<CharString, ID> reverse_cache; // Used at runtime.
	static ResourceUID *singleton;

	uint32_t cache_entries = 0;
	bool changed = false;
	bool cache_initialized = false;

protected:
	static void register_members();

public:
	inline static ResourceUIDScanForUIDOnStartup scan_for_uid_on_startup = nullptr;

	String id_to_text(ID p_id) const;
	ID text_to_id(const String &p_text) const;

	ID create_id();
	ID create_id_for_path(const String &p_path);
	bool has_id(ID p_id) const;
	void add_id(ID p_id, const String &p_path);
	void set_id(ID p_id, const String &p_path);
	String get_id_path(ID p_id) const;
	ID get_path_id(const String &p_path) const;
	void remove_id(ID p_id);

	static String uid_to_path(const String &p_uid);
	static String path_to_uid(const String &p_path);
	static String ensure_path(const String &p_uid_or_path);
	// Doesn't print errors, returns empty path on invalid UID.
	static String ensure_path_nocheck(const String &p_uid_or_path);

	Error load_from_cache(bool p_reset);
	Error save_to_cache();
	Error update_cache();
	static String get_path_from_cache(Ref<FileAccess> &p_cache_file, const String &p_uid_string);
	static Vector<uint8_t> encode_binary_cache(const Vector<Pair<ID, String>> &p_entries);

	void enable_reverse_cache() { use_reverse_cache = true; }
	void clear();
#ifdef TOOLS_ENABLED
	void copy_and_clear_cache();
	void clear_copy();
#endif

	static ResourceUID *get_singleton() { return singleton; }

	ResourceUID();
};
