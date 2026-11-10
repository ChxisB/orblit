/**************************************************************************/
/*  lru.h                                                                 */

#pragma once

#include "core/containers/hash_map.h"
#include "core/containers/list.h"

template <typename TKey, typename TData, typename Hasher = HashMapHasherDefault, typename Comparator = HashMapComparatorDefault<TKey>, void (*BeforeEvict)(TKey &, TData &) = nullptr>
class _WARN_UNUSED_ LRUCache {
public:
	struct Pair {
		TKey key;
		TData data;

		Pair() {}
		Pair(const TKey &p_key, const TData &p_data) :
				key(p_key),
				data(p_data) {
		}
	};

	typedef typename List<Pair>::Element *Element;

private:
	List<Pair> _list;
	HashMap<TKey, Element, Hasher, Comparator> _map;
	size_t capacity;

public:
	const Pair *insert(const TKey &p_key, const TData &p_value) {
		Element *e = _map.getptr(p_key);
		Element n = _list.push_front(Pair(p_key, p_value));

		if (e) {
			ORBLIT_GCC_WARNING_PUSH_AND_IGNORE("-Waddress")
			if constexpr (BeforeEvict != nullptr) {
				BeforeEvict((*e)->get().key, (*e)->get().data);
			}
			ORBLIT_GCC_WARNING_POP
			_list.erase(*e);
			_map.erase(p_key);
		}
		_map[p_key] = _list.front();

		while (_map.size() > capacity) {
			Element d = _list.back();
			ORBLIT_GCC_WARNING_PUSH_AND_IGNORE("-Waddress")
			if constexpr (BeforeEvict != nullptr) {
				BeforeEvict(d->get().key, d->get().data);
			}
			ORBLIT_GCC_WARNING_POP
			_map.erase(d->get().key);
			_list.pop_back();
		}

		return &n->get();
	}

	void clear() {
		_map.clear();
		_list.clear();
	}

	bool has(const TKey &p_key) const {
		return _map.getptr(p_key);
	}

	bool erase(const TKey &p_key) {
		Element *e = _map.getptr(p_key);
		if (!e) {
			return false;
		}
		_list.move_to_front(*e);
		_map.erase(p_key);
		_list.pop_front();
		return true;
	}

	const TData &get(const TKey &p_key) {
		Element *e = _map.getptr(p_key);
		CRASH_COND(!e);
		_list.move_to_front(*e);
		return (*e)->get().data;
	}

	const TData *getptr(const TKey &p_key) {
		Element *e = _map.getptr(p_key);
		if (!e) {
			return nullptr;
		} else {
			_list.move_to_front(*e);
			return &(*e)->get().data;
		}
	}

	_FORCE_INLINE_ size_t get_capacity() const { return capacity; }
	_FORCE_INLINE_ size_t get_size() const { return _map.size(); }

	void set_capacity(size_t p_capacity) {
		if (capacity > 0) {
			capacity = p_capacity;
			while (_map.size() > capacity) {
				Element d = _list.back();
				ORBLIT_GCC_WARNING_PUSH_AND_IGNORE("-Waddress")
				if constexpr (BeforeEvict != nullptr) {
					BeforeEvict(d->get().key, d->get().data);
				}
				ORBLIT_GCC_WARNING_POP
				_map.erase(d->get().key);
				_list.pop_back();
			}
		}
	}

	LRUCache() {
		capacity = 64;
	}

	LRUCache(int p_capacity) {
		capacity = p_capacity;
	}
};
