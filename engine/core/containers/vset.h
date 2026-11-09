/**************************************************************************/
/*  vset.h                                                                */

#pragma once

#include "core/containers/vector.h"
#include "core/typedefs.h"

/**
 * Ordered set container with copy-on-write semantics.
 */
template <typename T>
class _WARN_UNUSED_ VSet {
	Vector<T> _data;

protected:
	_FORCE_INLINE_ int _find(const T &p_val, bool &r_exact) const {
		r_exact = false;
		if (_data.is_empty()) {
			return 0;
		}

		int64_t pos = _data.span().bisect(p_val, true);

		if (pos < _data.size() && !(p_val < _data[pos]) && !(_data[pos] < p_val)) {
			r_exact = true;
		}
		return pos;
	}

	_FORCE_INLINE_ int _find_exact(const T &p_val) const {
		if (_data.is_empty()) {
			return -1;
		}

		int64_t pos = _data.span().bisect(p_val, true);

		if (pos < _data.size() && !(p_val < _data[pos]) && !(_data[pos] < p_val)) {
			return pos;
		}
		return -1;
	}

public:
	void insert(const T &p_val) {
		bool exact;
		int pos = _find(p_val, exact);
		if (exact) {
			return;
		}
		_data.insert(pos, p_val);
	}

	bool has(const T &p_val) const {
		return _find_exact(p_val) != -1;
	}

	void erase(const T &p_val) {
		int pos = _find_exact(p_val);
		if (pos < 0) {
			return;
		}
		_data.remove_at(pos);
	}

	int find(const T &p_val) const {
		return _find_exact(p_val);
	}

	_FORCE_INLINE_ bool is_empty() const { return _data.is_empty(); }

	_FORCE_INLINE_ int size() const { return _data.size(); }

	inline T &operator[](int p_index) {
		return _data.write[p_index];
	}

	inline const T &operator[](int p_index) const {
		return _data[p_index];
	}

	_FORCE_INLINE_ VSet() {}
	_FORCE_INLINE_ VSet(std::initializer_list<T> p_init) :
			_data(p_init) {}
};
