/**************************************************************************/
/*  bit_field.h                                                           */

#pragma once

#include "core/typedefs.h"

#include <type_traits>

// TODO: Replace `typename` with enum concept once C++20 concepts/constraints are allowed.

template <typename T>
class BitField {
	static_assert(std::is_enum_v<T>);
	uint64_t value;

public:
	_ALWAYS_INLINE_ constexpr void set_flag(BitField p_flag) { value |= p_flag.value; }
	_ALWAYS_INLINE_ constexpr bool has_flag(BitField p_flag) const { return value & p_flag.value; }
	_ALWAYS_INLINE_ constexpr bool is_empty() const { return value == 0; }
	_ALWAYS_INLINE_ constexpr void clear_flag(BitField p_flag) { value &= ~p_flag.value; }
	_ALWAYS_INLINE_ constexpr void clear() { value = 0; }

	[[nodiscard]] _ALWAYS_INLINE_ constexpr BitField get_combined(BitField p_other) const { return BitField(value | p_other.value); }
	[[nodiscard]] _ALWAYS_INLINE_ constexpr BitField get_shared(BitField p_other) const { return BitField(value & p_other.value); }
	[[nodiscard]] _ALWAYS_INLINE_ constexpr BitField get_different(BitField p_other) const { return BitField(value ^ p_other.value); }

	_ALWAYS_INLINE_ constexpr BitField() = default;
	_ALWAYS_INLINE_ constexpr BitField(T p_value) :
			value(static_cast<uint64_t>(p_value)) {}
	_ALWAYS_INLINE_ constexpr operator T() const { return static_cast<T>(value); }

	// TODO: Unify as single constructor once C++20 `explicit` conditionals are allowed.

	template <typename V, std::enable_if_t<std::is_arithmetic_v<V> && std::is_convertible_v<T, int>, int> = 0>
	_ALWAYS_INLINE_ constexpr BitField(V p_value) :
			value(static_cast<uint64_t>(p_value)) {}
	template <typename V, std::enable_if_t<std::is_arithmetic_v<V> && !std::is_convertible_v<T, int>, int> = 0>
	_ALWAYS_INLINE_ constexpr explicit BitField(V p_value) :
			value(static_cast<uint64_t>(p_value)) {}
	template <typename V, std::enable_if_t<std::is_arithmetic_v<V>, int> = 0>
	_ALWAYS_INLINE_ constexpr explicit operator V() const { return static_cast<V>(value); }
};

// Implicitly zero-constructible as a trivially-constructible type.
static_assert(is_zero_constructible_v<BitField<Error>>);
