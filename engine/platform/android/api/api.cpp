/**************************************************************************/
/*  api.cpp                                                               */

#include "api.h"

#include "java_class_wrapper.h"
#include "jni_singleton.h"

#include "core/config/engine.h"
#include "core/object/class_registry.h"

#if !defined(ANDROID_ENABLED)
static JavaClassWrapper *java_class_wrapper = nullptr;
#endif

void register_android_api() {
#if !defined(ANDROID_ENABLED)
	// On Android platforms, the `java_class_wrapper` instantiation occurs in
	// `platform/android/java_lib_jni.cpp#Java_org_orblitengine_orblit_OrblitLib_setup`
	java_class_wrapper = memnew(JavaClassWrapper);
#endif
	ORBLIT_REGISTER_CLASS(JNISingleton);
	ORBLIT_REGISTER_CLASS(JavaClass);
	ORBLIT_REGISTER_CLASS(JavaObject);
	ORBLIT_REGISTER_CLASS(JavaClassWrapper);
	Engine::get_singleton()->add_singleton(Engine::Singleton("JavaClassWrapper", JavaClassWrapper::get_singleton()));
}

void unregister_android_api() {
#if !defined(ANDROID_ENABLED)
	memdelete(java_class_wrapper);
#endif
}

void JavaClass::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_java_class_name"), &JavaClass::get_java_class_name);
	ClassRegistry::bind_method(D_METHOD("get_java_method_list"), &JavaClass::get_java_method_list);
	ClassRegistry::bind_method(D_METHOD("get_java_parent_class"), &JavaClass::get_java_parent_class);
	ClassRegistry::bind_method(D_METHOD("has_java_method", "method"), &JavaClass::has_java_method);
}

void JavaObject::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_java_class"), &JavaObject::get_java_class);
	ClassRegistry::bind_method(D_METHOD("has_java_method", "method"), &JavaObject::has_java_method);
}

void JavaClassWrapper::register_members() {
	ClassRegistry::bind_method(D_METHOD("wrap", "name"), &JavaClassWrapper::wrap);
	ClassRegistry::bind_method(D_METHOD("get_exception"), &JavaClassWrapper::get_exception);
	ClassRegistry::bind_method(D_METHOD("create_sam_callback", "sam_interface", "callable"), &JavaClassWrapper::create_sam_callback);
	ClassRegistry::bind_method(D_METHOD("create_proxy", "object", "interfaces"), &JavaClassWrapper::create_proxy);
}

#if !defined(ANDROID_ENABLED)
bool JavaClass::_get(const StringName &p_name, Variant &r_ret) const {
	return false;
}

bool JavaClass::_set(const StringName &p_name, const Variant &p_property) {
	return false;
}

Variant JavaClass::callp(const StringName &, const Variant **, int, Callable::CallError &) {
	return Variant();
}

String JavaClass::get_java_class_name() const {
	return "";
}

TypedArray<Dictionary> JavaClass::get_java_method_list() const {
	return TypedArray<Dictionary>();
}

Ref<JavaClass> JavaClass::get_java_parent_class() const {
	return Ref<JavaClass>();
}

bool JavaClass::has_java_method(const StringName &) const {
	return false;
}

JavaClass::JavaClass() {
}

JavaClass::~JavaClass() {
}

bool JavaObject::_get(const StringName &p_name, Variant &r_ret) const {
	return false;
}

bool JavaObject::_set(const StringName &p_name, const Variant &p_property) {
	return false;
}

Variant JavaObject::callp(const StringName &, const Variant **, int, Callable::CallError &) {
	return Variant();
}

Ref<JavaClass> JavaObject::get_java_class() const {
	return Ref<JavaClass>();
}

bool JavaObject::has_java_method(const StringName &) const {
	return false;
}

JavaClassWrapper *JavaClassWrapper::singleton = nullptr;

Ref<JavaClass> JavaClassWrapper::_wrap(const String &, bool) {
	return Ref<JavaClass>();
}

Ref<JavaObject> JavaClassWrapper::create_sam_callback(const String &p_interface, const Callable &p_callable) {
	return Ref<JavaObject>();
}

Ref<JavaObject> JavaClassWrapper::create_proxy(const Object *p_object, const PackedStringArray &p_interfaces) {
	return Ref<JavaObject>();
}

JavaClassWrapper::JavaClassWrapper() {
	singleton = this;
}

#endif
