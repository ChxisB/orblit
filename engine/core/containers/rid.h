/**************************************************************************/
/*  rid.h                                                                 */

#pragma once

#include "core/containers/hashfuncs.h"
#include "core/typedefs.h"

class RID_AllocBase;

class RID {
	friend class RID_AllocBase;
	uint64_t _id = 0;

public:
	_ALWAYS_INLINE_ bool operator==(const RID &p_rid) const {
		return _id == p_rid._id;
	}
	_ALWAYS_INLINE_ bool operator<(const RID &p_rid) const {
		return _id < p_rid._id;
	}
	_ALWAYS_INLINE_ bool operator<=(const RID &p_rid) const {
		return _id <= p_rid._id;
	}
	_ALWAYS_INLINE_ bool operator>(const RID &p_rid) const {
		return _id > p_rid._id;
	}
	_ALWAYS_INLINE_ bool operator>=(const RID &p_rid) const {
		return _id >= p_rid._id;
	}
	_ALWAYS_INLINE_ bool operator!=(const RID &p_rid) const {
		return _id != p_rid._id;
	}
	_ALWAYS_INLINE_ bool is_valid() const { return _id != 0; }
	_ALWAYS_INLINE_ bool is_null() const { return _id == 0; }

	_ALWAYS_INLINE_ uint32_t get_local_index() const { return _id & 0xFFFFFFFF; }

	static _ALWAYS_INLINE_ RID from_uint64(uint64_t p_id) {
		RID _rid;
		_rid._id = p_id;
		return _rid;
	}
	_ALWAYS_INLINE_ uint64_t get_id() const { return _id; }

	uint32_t hash() const { return HashMapHasherDefault::hash(_id); }
};

template <>
struct is_zero_constructible<RID> : std::true_type {};
