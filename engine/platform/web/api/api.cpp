/**************************************************************************/
/*  api.cpp                                                               */

#include "api.h"

#include "javascript_bridge_singleton.h"

#include "core/config/engine.h"
#include "core/object/class_registry.h"

static JavaScriptBridge *javascript_bridge_singleton;

void register_web_api() {
	ORBLIT_REGISTER_ABSTRACT_CLASS(JavaScriptObject);
	ORBLIT_REGISTER_ABSTRACT_CLASS(JavaScriptBridge);
	javascript_bridge_singleton = memnew(JavaScriptBridge);
	Engine::get_singleton()->add_singleton(Engine::Singleton("JavaScriptBridge", javascript_bridge_singleton));
}

void unregister_web_api() {
	memdelete(javascript_bridge_singleton);
}

JavaScriptBridge *JavaScriptBridge::singleton = nullptr;

JavaScriptBridge *JavaScriptBridge::get_singleton() {
	return singleton;
}

JavaScriptBridge::JavaScriptBridge() {
	ERR_FAIL_COND_MSG(singleton != nullptr, "JavaScriptBridge singleton already exists.");
	singleton = this;
}

JavaScriptBridge::~JavaScriptBridge() {}

void JavaScriptBridge::register_members() {
	ClassRegistry::bind_method(D_METHOD("eval", "code", "use_global_execution_context"), &JavaScriptBridge::eval, DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("get_interface", "interface"), &JavaScriptBridge::get_interface);
	ClassRegistry::bind_method(D_METHOD("create_callback", "callable"), &JavaScriptBridge::create_callback);
	ClassRegistry::bind_method(D_METHOD("is_js_buffer", "javascript_object"), &JavaScriptBridge::is_js_buffer);
	ClassRegistry::bind_method(D_METHOD("js_buffer_to_packed_byte_array", "javascript_buffer"), &JavaScriptBridge::js_buffer_to_packed_byte_array);
	{
		MethodInfo mi;
		mi.name = "create_object";
		mi.arguments.push_back(PropertyInfo(Variant::STRING, "object"));
		ClassRegistry::bind_vararg_method(METHOD_FLAGS_DEFAULT, "create_object", &JavaScriptBridge::_create_object_bind, mi);
	}
	ClassRegistry::bind_method(D_METHOD("download_buffer", "buffer", "name", "mime"), &JavaScriptBridge::download_buffer, DEFVAL("application/octet-stream"));
	ClassRegistry::bind_method(D_METHOD("pwa_needs_update"), &JavaScriptBridge::pwa_needs_update);
	ClassRegistry::bind_method(D_METHOD("pwa_update"), &JavaScriptBridge::pwa_update);
	ClassRegistry::bind_method(D_METHOD("force_fs_sync"), &JavaScriptBridge::force_fs_sync);
	ADD_SIGNAL(MethodInfo("pwa_update_available"));
}

#if !defined(WEB_ENABLED) || !defined(JAVASCRIPT_EVAL_ENABLED)

Variant JavaScriptBridge::eval(const String &p_code, bool p_use_global_exec_context) {
	return Variant();
}

Ref<JavaScriptObject> JavaScriptBridge::get_interface(const String &p_interface) {
	return Ref<JavaScriptObject>();
}

Ref<JavaScriptObject> JavaScriptBridge::create_callback(const Callable &p_callable) {
	return Ref<JavaScriptObject>();
}

bool JavaScriptBridge::is_js_buffer(Ref<JavaScriptObject> p_js_obj) {
	return false;
}

PackedByteArray JavaScriptBridge::js_buffer_to_packed_byte_array(Ref<JavaScriptObject> p_js_obj) {
	return PackedByteArray();
}

Variant JavaScriptBridge::_create_object_bind(const Variant **p_args, int p_argcount, Callable::CallError &r_error) {
	if (p_argcount < 1) {
		r_error.error = Callable::CallError::CALL_ERROR_TOO_FEW_ARGUMENTS;
		r_error.expected = 1;
		return Ref<JavaScriptObject>();
	}
	if (!p_args[0]->is_string()) {
		r_error.error = Callable::CallError::CALL_ERROR_INVALID_ARGUMENT;
		r_error.argument = 0;
		r_error.expected = Variant::STRING;
		return Ref<JavaScriptObject>();
	}
	return Ref<JavaScriptObject>();
}

#endif

#if !defined(WEB_ENABLED)

bool JavaScriptBridge::pwa_needs_update() const {
	return false;
}

Error JavaScriptBridge::pwa_update() {
	return ERR_UNAVAILABLE;
}

void JavaScriptBridge::force_fs_sync() {
}

void JavaScriptBridge::download_buffer(Vector<uint8_t> p_arr, const String &p_name, const String &p_mime) {
}

#endif
