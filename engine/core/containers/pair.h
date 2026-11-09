/**************************************************************************/
/*  pair.h                                                                */

#pragma once

#include "core/typedefs.h"

template <typename F, typename S>
struct Pair {
	F first{};
	S second{};

	constexpr Pair() = default;
	constexpr Pair(const F &p_first, const S &p_second) :
			first(p_first), second(p_second) {}

	constexpr bool operator==(const Pair &p_other) const { return first == p_other.first && second == p_other.second; }
	constexpr bool operator!=(const Pair &p_other) const { return first != p_other.first || second != p_other.second; }
	constexpr bool operator<(const Pair &p_other) const { return first == p_other.first ? (second < p_other.second) : (first < p_other.first); }
	constexpr bool operator<=(const Pair &p_other) const { return first == p_other.first ? (second <= p_other.second) : (first < p_other.first); }
	constexpr bool operator>(const Pair &p_other) const { return first == p_other.first ? (second > p_other.second) : (first > p_other.first); }
	constexpr bool operator>=(const Pair &p_other) const { return first == p_other.first ? (second >= p_other.second) : (first > p_other.first); }
};

template <typename F, typename S>
struct PairSort {
	constexpr bool operator()(const Pair<F, S> &p_lhs, const Pair<F, S> &p_rhs) const {
		return p_lhs < p_rhs;
	}
};

// Pair is zero-constructible if and only if both constrained types are zero-constructible.
template <typename F, typename S>
struct is_zero_constructible<Pair<F, S>> : std::conjunction<is_zero_constructible<F>, is_zero_constructible<S>> {};

template <typename K, typename V>
struct KeyValue {
	const K key{};
	V value{};

	KeyValue &operator=(const KeyValue &p_kv) = delete;
	KeyValue &operator=(KeyValue &&p_kv) = delete;

	constexpr KeyValue(const KeyValue &p_kv) = default;
	constexpr KeyValue(KeyValue &&p_kv) = default;
	constexpr KeyValue(const K &p_key, const V &p_value) :
			key(p_key), value(p_value) {}
	constexpr KeyValue(const Pair<K, V> &p_pair) :
			key(p_pair.first), value(p_pair.second) {}

	constexpr bool operator==(const KeyValue &p_other) const { return key == p_other.key && value == p_other.value; }
	constexpr bool operator!=(const KeyValue &p_other) const { return key != p_other.key || value != p_other.value; }
	constexpr bool operator<(const KeyValue &p_other) const { return key == p_other.key ? (value < p_other.value) : (key < p_other.key); }
	constexpr bool operator<=(const KeyValue &p_other) const { return key == p_other.key ? (value <= p_other.value) : (key < p_other.key); }
	constexpr bool operator>(const KeyValue &p_other) const { return key == p_other.key ? (value > p_other.value) : (key > p_other.key); }
	constexpr bool operator>=(const KeyValue &p_other) const { return key == p_other.key ? (value >= p_other.value) : (key > p_other.key); }
};

template <typename K, typename V>
struct KeyValueSort {
	constexpr bool operator()(const KeyValue<K, V> &p_lhs, const KeyValue<K, V> &p_rhs) const {
		return p_lhs.key < p_rhs.key;
	}
};

// KeyValue is zero-constructible if and only if both constrained types are zero-constructible.
template <typename K, typename V>
struct is_zero_constructible<KeyValue<K, V>> : std::conjunction<is_zero_constructible<K>, is_zero_constructible<V>> {};
