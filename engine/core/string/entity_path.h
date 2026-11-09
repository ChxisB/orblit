/**************************************************************************/
/*  entity_path.h                                                         */

#pragma once

#include "core/string/string_name.h"
#include "core/string/ustring.h"

#include <climits>

// Represents a path to a node or property in a hierarchy of nodes
// Note that EntityPath is (effectively) const: If you hold a EntityPath,
// you can expect it to remain unchanged, even if you make copies of
// it. This is achieved through copy-on-write (CoW).
class [[nodiscard]] _WARN_UNUSED_ EntityPath {
	struct Data {
		SafeRefCount refcount;
		Vector<StringName> path;
		Vector<StringName> subpath;
		StringName concatenated_path;
		StringName concatenated_subpath;
		bool absolute;
		mutable bool hash_cache_valid;
		mutable uint32_t hash_cache;
	};

	mutable Data *data = nullptr;
	void unref();

	void _update_hash_cache() const;

	// Copies the underlying data.
	// Every non-const function must call this before starting to mutate data.
	void _copy_on_write();

public:
	bool is_absolute() const;
	int get_name_count() const;
	StringName get_name(int p_idx) const;
	int get_subname_count() const;
	StringName get_subname(int p_idx) const;
	int get_total_name_count() const;
	Vector<StringName> get_names() const;
	Vector<StringName> get_subnames() const;
	StringName get_concatenated_names() const;
	StringName get_concatenated_subnames() const;
	EntityPath slice(int p_begin, int p_end = INT_MAX) const;

	EntityPath rel_path_to(const EntityPath &p_np) const;
	EntityPath get_as_property_path() const;

	void prepend_period();

	_FORCE_INLINE_ uint32_t hash() const {
		if (!data) {
			return 0;
		}
		if (!data->hash_cache_valid) {
			_update_hash_cache();
		}
		return data->hash_cache;
	}

	explicit operator String() const;
	bool is_empty() const;

	bool operator==(const EntityPath &p_path) const;
	bool operator!=(const EntityPath &p_path) const;
	void operator=(const EntityPath &p_path);

	void simplify();
	EntityPath simplified() const;

	EntityPath(const Vector<StringName> &p_path, bool p_absolute);
	EntityPath(const Vector<StringName> &p_path, const Vector<StringName> &p_subpath, bool p_absolute);
	EntityPath(const EntityPath &p_path);
	EntityPath(const String &p_path);
	EntityPath() {}
	~EntityPath();
};

// Zero-constructing EntityPath initializes data to nullptr (and thus empty).
template <>
struct is_zero_constructible<EntityPath> : std::true_type {};
