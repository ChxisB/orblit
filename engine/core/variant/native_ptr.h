/**************************************************************************/
/*  native_ptr.h                                                          */

#pragma once

#include "core/variant/method_ptrcall.h"
#include "core/variant/type_info.h"
#include "core/variant/variant_caster.h"
#include "core/variant/variant_internal.h"

struct AudioFrame;

// Metafunction for pointee name. Specialized in ORBLIT_VIRTUAL_NATIVE_PTR; takes into account const; falls back to "void".
template <typename T>
struct NativeExtensionPtrName {
	static const char *get() { return "void"; }
};
template <typename T>
struct NativeExtensionPtrName<const T> {
	static const char *get() {
		static const CharString name = (String("const ") + NativeExtensionPtrName<T>::get()).utf8();
		return name.get_data();
	}
};
template <typename T>
struct NativeExtensionPtrName<T *> {
	static const char *get() {
		static const CharString name = (String(NativeExtensionPtrName<T>::get()) + " *").utf8();
		return name.get_data();
	}
};

#define ORBLIT_VIRTUAL_NATIVE_PTR(m_type) \
	template <> \
	struct NativeExtensionPtrName<m_type> { \
		static const char *get() { return #m_type; } \
	};

// Raw pointer passed across the NativeExtension boundary. T can be const-qualified to map to `const MyClass*`.
template <typename T>
struct NativeExtensionPtr {
	T *data = nullptr;
	NativeExtensionPtr() {}
	NativeExtensionPtr(T *p_assign) { data = p_assign; }
	static const char *get_name() { return NativeExtensionPtrName<T>::get(); }
	operator T *() const { return data; }
	operator Variant() const { return uint64_t(data); }
};

template <typename T>
struct GetTypeInfo<NativeExtensionPtr<T>> {
	static constexpr Variant::Type VARIANT_TYPE = Variant::INT;
	static constexpr OrblitTypeInfo::Metadata METADATA = OrblitTypeInfo::METADATA_NONE;
	static inline PropertyInfo get_class_info() {
		return PropertyInfo(Variant::INT, String(), PROPERTY_HINT_INT_IS_POINTER, NativeExtensionPtr<T>::get_name());
	}
};

template <typename T>
struct VariantCaster<NativeExtensionPtr<T>> {
	static _FORCE_INLINE_ NativeExtensionPtr<T> cast(const Variant &p_variant) {
		return NativeExtensionPtr<T>(reinterpret_cast<T *>(p_variant.operator uint64_t()));
	}
};

template <typename T>
struct VariantInternalAccessor<NativeExtensionPtr<T>> {
	static _FORCE_INLINE_ const NativeExtensionPtr<T> &get(const Variant *p_variant) {
		return *reinterpret_cast<const NativeExtensionPtr<T> *>(VariantInternal::get_int(p_variant));
	}
	static _FORCE_INLINE_ void set(Variant *r_variant, const NativeExtensionPtr<T> &p_value) {
		*VariantInternal::get_int(r_variant) = uint64_t(p_value.data);
	}
};

template <typename T>
struct PtrToArg<NativeExtensionPtr<T>> {
	_FORCE_INLINE_ static NativeExtensionPtr<T> convert(const void *p_ptr) {
		return NativeExtensionPtr<T>(static_cast<T *>(const_cast<void *>(p_ptr)));
	}
	typedef T *EncodeT;
	_FORCE_INLINE_ static void encode(NativeExtensionPtr<T> p_val, void *p_ptr) {
		*static_cast<T **>(p_ptr) = p_val.data;
	}
};

ORBLIT_VIRTUAL_NATIVE_PTR(AudioFrame)
ORBLIT_VIRTUAL_NATIVE_PTR(bool)
ORBLIT_VIRTUAL_NATIVE_PTR(char)
ORBLIT_VIRTUAL_NATIVE_PTR(char16_t)
ORBLIT_VIRTUAL_NATIVE_PTR(char32_t)
ORBLIT_VIRTUAL_NATIVE_PTR(wchar_t)
ORBLIT_VIRTUAL_NATIVE_PTR(uint8_t)
ORBLIT_VIRTUAL_NATIVE_PTR(int8_t)
ORBLIT_VIRTUAL_NATIVE_PTR(uint16_t)
ORBLIT_VIRTUAL_NATIVE_PTR(int16_t)
ORBLIT_VIRTUAL_NATIVE_PTR(uint32_t)
ORBLIT_VIRTUAL_NATIVE_PTR(int32_t)
ORBLIT_VIRTUAL_NATIVE_PTR(int64_t)
ORBLIT_VIRTUAL_NATIVE_PTR(uint64_t)
ORBLIT_VIRTUAL_NATIVE_PTR(float)
ORBLIT_VIRTUAL_NATIVE_PTR(double)
