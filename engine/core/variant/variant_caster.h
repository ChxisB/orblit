/**************************************************************************/
/*  variant_caster.h                                                      */

#pragma once

#include "core/object/object.h"
#include "core/variant/type_info.h"

enum class HatDir;
enum class HatMask;
enum class JoyAxis;
enum class JoyButton;

enum class MIDIMessage;
enum class MouseButton;
enum class MouseButtonMask;

enum class Key;
enum class KeyModifierMask;
enum class KeyLocation;

// Variant cannot define an implicit cast operator for every Object subclass, so the
// casting is done here, to allow binding methods with parameters more specific than Object *

// The Variant of a freed object keeps a stale pointer. Debug builds look the object up by its id,
// so a freed object reads as null and is never dereferenced.
_FORCE_INLINE_ Object *variant_object_pointer(const Variant &p_variant) {
#ifdef DEBUG_ENABLED
	return p_variant.get_validated_object();
#else
	return p_variant;
#endif
}

template <typename T>
struct VariantCaster {
	static _FORCE_INLINE_ T cast(const Variant &p_variant) {
		using TStripped = std::remove_pointer_t<T>;
		if constexpr (std::is_base_of_v<Object, TStripped>) {
			return Object::cast_to<TStripped>(variant_object_pointer(p_variant));
		} else {
			return p_variant.to<T>();
		}
	}
};

template <typename T>
struct VariantCaster<T &> {
	static _FORCE_INLINE_ T cast(const Variant &p_variant) {
		using TStripped = std::remove_pointer_t<T>;
		if constexpr (std::is_base_of_v<Object, TStripped>) {
			return Object::cast_to<TStripped>(variant_object_pointer(p_variant));
		} else {
			return p_variant.to<T>();
		}
	}
};

template <typename T>
struct VariantCaster<const T &> {
	static _FORCE_INLINE_ T cast(const Variant &p_variant) {
		using TStripped = std::remove_pointer_t<T>;
		if constexpr (std::is_base_of_v<Object, TStripped>) {
			return Object::cast_to<TStripped>(variant_object_pointer(p_variant));
		} else {
			return p_variant.to<T>();
		}
	}
};

// Object enum casts must go here
VARIANT_BITFIELD_CAST(Object::ConnectFlags);

VARIANT_ENUM_CAST(Vector2::Axis);
VARIANT_ENUM_CAST(Vector2i::Axis);
VARIANT_ENUM_CAST(Vector3::Axis);
VARIANT_ENUM_CAST(Vector3i::Axis);
VARIANT_ENUM_CAST(Vector4::Axis);
VARIANT_ENUM_CAST(Vector4i::Axis);
VARIANT_ENUM_CAST(EulerOrder);
VARIANT_ENUM_CAST(Projection::Planes);

VARIANT_ENUM_CAST(Error);
VARIANT_ENUM_CAST(Side);
VARIANT_ENUM_CAST(ClockDirection);
VARIANT_ENUM_CAST(Corner);
VARIANT_ENUM_CAST(HatDir);
VARIANT_BITFIELD_CAST(HatMask);
VARIANT_ENUM_CAST(JoyAxis);
VARIANT_ENUM_CAST(JoyButton);

VARIANT_ENUM_CAST(MIDIMessage);
VARIANT_ENUM_CAST(MouseButton);
VARIANT_BITFIELD_CAST(MouseButtonMask);
VARIANT_ENUM_CAST(Orientation);
VARIANT_ENUM_CAST(HorizontalAlignment);
VARIANT_ENUM_CAST(VerticalAlignment);
VARIANT_ENUM_CAST(InlineAlignment);
VARIANT_ENUM_CAST(PropertyHint);
VARIANT_BITFIELD_CAST(PropertyUsageFlags);
VARIANT_ENUM_CAST(Variant::Type);
VARIANT_ENUM_CAST(Variant::Operator);

// Key

VARIANT_ENUM_CAST(Key);
VARIANT_BITFIELD_CAST(KeyModifierMask);
VARIANT_ENUM_CAST(KeyLocation);

template <>
struct VariantCaster<char32_t> {
	static _FORCE_INLINE_ char32_t cast(const Variant &p_variant) {
		return (char32_t)p_variant.operator int();
	}
};

template <typename T>
struct VariantObjectClassChecker {
	static _FORCE_INLINE_ bool check(const Variant &p_variant) {
		using TStripped = std::remove_pointer_t<T>;
		if constexpr (std::is_base_of_v<Object, TStripped>) {
			bool previously_freed = false;
			Object *obj = p_variant.get_validated_object_with_check(previously_freed);
			return !previously_freed && (Object::cast_to<TStripped>(obj) || !obj);
		} else {
			return true;
		}
	}
};

template <typename T>
class Ref;

template <typename T>
struct VariantObjectClassChecker<const Ref<T> &> {
	static _FORCE_INLINE_ bool check(const Variant &p_variant) {
		Object *obj = p_variant;
		const Ref<T> node = p_variant;
		return node.ptr() || !obj;
	}
};

#ifdef DEBUG_ENABLED

template <typename T>
struct VariantCasterAndValidate {
	static _FORCE_INLINE_ T cast(const Variant **p_args, uint32_t p_arg_idx, Callable::CallError &r_error) {
		Variant::Type argtype = GetTypeInfo<T>::VARIANT_TYPE;
		if (!Variant::can_convert_strict(p_args[p_arg_idx]->get_type(), argtype) ||
				!VariantObjectClassChecker<T>::check(*p_args[p_arg_idx])) {
			r_error.error = Callable::CallError::CALL_ERROR_INVALID_ARGUMENT;
			r_error.argument = p_arg_idx;
			r_error.expected = argtype;
		}

		return VariantCaster<T>::cast(*p_args[p_arg_idx]);
	}
};

template <typename T>
struct VariantCasterAndValidate<T &> {
	static _FORCE_INLINE_ T cast(const Variant **p_args, uint32_t p_arg_idx, Callable::CallError &r_error) {
		Variant::Type argtype = GetTypeInfo<T>::VARIANT_TYPE;
		if (!Variant::can_convert_strict(p_args[p_arg_idx]->get_type(), argtype) ||
				!VariantObjectClassChecker<T>::check(*p_args[p_arg_idx])) {
			r_error.error = Callable::CallError::CALL_ERROR_INVALID_ARGUMENT;
			r_error.argument = p_arg_idx;
			r_error.expected = argtype;
		}

		return VariantCaster<T>::cast(*p_args[p_arg_idx]);
	}
};

template <typename T>
struct VariantCasterAndValidate<const T &> {
	static _FORCE_INLINE_ T cast(const Variant **p_args, uint32_t p_arg_idx, Callable::CallError &r_error) {
		Variant::Type argtype = GetTypeInfo<T>::VARIANT_TYPE;
		if (!Variant::can_convert_strict(p_args[p_arg_idx]->get_type(), argtype) ||
				!VariantObjectClassChecker<T>::check(*p_args[p_arg_idx])) {
			r_error.error = Callable::CallError::CALL_ERROR_INVALID_ARGUMENT;
			r_error.argument = p_arg_idx;
			r_error.expected = argtype;
		}

		return VariantCaster<T>::cast(*p_args[p_arg_idx]);
	}
};

#endif // DEBUG_ENABLED
