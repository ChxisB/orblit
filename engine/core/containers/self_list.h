/**************************************************************************/
/*  self_list.h                                                           */

#pragma once

#include "core/error/error_macros.h"
#include "core/containers/sort_list.h"
#include "core/typedefs.h"

/**
 * A doubly linked list, implemented by storing the previous and next
 * pointer in each element rather than a separate allocation.
 *
 * This class is not trivially relocatable, which makes
 * usage like `Vector<SelfList<T>>` impossible (see GH-100509).
 */
template <typename T>
class _WARN_UNUSED_ SelfList {
public:
	class List {
		SelfList<T> *_first = nullptr;
		SelfList<T> *_last = nullptr;

	public:
		void add(SelfList<T> *p_elem) {
			ERR_FAIL_COND(p_elem->_root);

			p_elem->_root = this;
			p_elem->_next = _first;
			p_elem->_prev = nullptr;

			if (_first) {
				_first->_prev = p_elem;

			} else {
				_last = p_elem;
			}

			_first = p_elem;
		}

		void add_last(SelfList<T> *p_elem) {
			ERR_FAIL_COND(p_elem->_root);

			p_elem->_root = this;
			p_elem->_next = nullptr;
			p_elem->_prev = _last;

			if (_last) {
				_last->_next = p_elem;

			} else {
				_first = p_elem;
			}

			_last = p_elem;
		}

		void remove(SelfList<T> *p_elem) {
			ERR_FAIL_COND(p_elem->_root != this);
			if (p_elem->_next) {
				p_elem->_next->_prev = p_elem->_prev;
			}

			if (p_elem->_prev) {
				p_elem->_prev->_next = p_elem->_next;
			}

			if (_first == p_elem) {
				_first = p_elem->_next;
			}

			if (_last == p_elem) {
				_last = p_elem->_prev;
			}

			p_elem->_next = nullptr;
			p_elem->_prev = nullptr;
			p_elem->_root = nullptr;
		}

		void clear() {
			while (_first) {
				remove(_first);
			}
		}

		void sort() {
			sort_custom<Comparator<T>>();
		}

		template <typename C>
		void sort_custom() {
			if (_first == _last) {
				return;
			}

			struct PtrComparator {
				C compare;
				_FORCE_INLINE_ bool operator()(const T *p_a, const T *p_b) const { return compare(*p_a, *p_b); }
			};
			using Element = SelfList<T>;
			SortList<Element, T *, _self_ref, _prev_ref, _next_ref, PtrComparator> sorter;
			sorter.sort(_first, _last);
		}

		struct ConstIterator {
			_FORCE_INLINE_ const T &operator*() const { return *E->self(); }
			_FORCE_INLINE_ const T *operator->() const { return E->self(); }

			_FORCE_INLINE_ ConstIterator &operator++() {
				E = E->next();
				return *this;
			}

			_FORCE_INLINE_ ConstIterator &operator--() {
				E = E->prev();
				return *this;
			}

			_FORCE_INLINE_ bool operator==(const ConstIterator &p_other) const { return E == p_other.E; }
			_FORCE_INLINE_ bool operator!=(const ConstIterator &p_other) const { return E != p_other.E; }

			_FORCE_INLINE_ ConstIterator(const SelfList<T> *p_elem) { E = p_elem; }
			_FORCE_INLINE_ ConstIterator() {}
			_FORCE_INLINE_ ConstIterator(const ConstIterator &p_it) { E = p_it.E; }

		private:
			const SelfList<T> *E = nullptr;
		};

		struct Iterator {
			_FORCE_INLINE_ T &operator*() const { return *E->self(); }
			_FORCE_INLINE_ T *operator->() const { return E->self(); }

			_FORCE_INLINE_ Iterator &operator++() {
				E = E->next();
				return *this;
			}

			_FORCE_INLINE_ Iterator &operator--() {
				E = E->prev();
				return *this;
			}

			_FORCE_INLINE_ bool operator==(const Iterator &p_other) const { return E == p_other.E; }
			_FORCE_INLINE_ bool operator!=(const Iterator &p_other) const { return E != p_other.E; }

			Iterator(SelfList<T> *p_elem) { E = p_elem; }
			Iterator() {}
			Iterator(const Iterator &p_it) { E = p_it.E; }

			operator ConstIterator() const {
				return ConstIterator(E);
			}

		private:
			SelfList<T> *E = nullptr;
		};

		_FORCE_INLINE_ Iterator begin() { return Iterator(first()); }
		_FORCE_INLINE_ Iterator end() { return Iterator(nullptr); }

		_FORCE_INLINE_ ConstIterator begin() const { return ConstIterator(first()); }
		_FORCE_INLINE_ ConstIterator end() const { return ConstIterator(nullptr); }

		_FORCE_INLINE_ SelfList<T> *first() { return _first; }
		_FORCE_INLINE_ const SelfList<T> *first() const { return _first; }

		// Forbid copying, which has broken behavior.
		void operator=(const List &) = delete;

		_FORCE_INLINE_ ~List() {
			// A self list must be empty on destruction.
			DEV_ASSERT(_first == nullptr);
		}
	};

private:
	List *_root = nullptr;
	T *_self = nullptr;
	SelfList<T> *_next = nullptr;
	SelfList<T> *_prev = nullptr;
	// Specify pointers in variables to work around a VS 2022 bug (GH-121326).
	static constexpr T *SelfList<T>::*_self_ref = &SelfList<T>::_self;
	static constexpr SelfList<T> *SelfList<T>::*_prev_ref = &SelfList<T>::_prev;
	static constexpr SelfList<T> *SelfList<T>::*_next_ref = &SelfList<T>::_next;

public:
	_FORCE_INLINE_ bool in_list() const { return _root; }
	_FORCE_INLINE_ void remove_from_list() {
		if (_root) {
			_root->remove(this);
		}
	}
	_FORCE_INLINE_ SelfList<T> *next() { return _next; }
	_FORCE_INLINE_ SelfList<T> *prev() { return _prev; }
	_FORCE_INLINE_ const SelfList<T> *next() const { return _next; }
	_FORCE_INLINE_ const SelfList<T> *prev() const { return _prev; }
	_FORCE_INLINE_ T *self() const { return _self; }

	// Forbid copying, which has broken behavior.
	void operator=(const SelfList<T> &) = delete;

	_FORCE_INLINE_ SelfList(T *p_self) {
		_self = p_self;
	}

	_FORCE_INLINE_ ~SelfList() {
		if (_root) {
			_root->remove(this);
		}
	}
};
