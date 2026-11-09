/**************************************************************************/
/*  web_script_proxy.cpp                                                  */

#include "web_script_proxy.h"

#include "web_game_bridge.h"

#include "js.h"

#include "core/io/resource_loader.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "core/object/script_language.h"
#include "core/containers/hash_map.h"
#include "scene/spatial/entity_spatial.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h" // Stage only forward declares it.

// The entity metadata that holds the class name of its C# script.
#define WEB_SCRIPT_CLASS_META SNAME("_web_script_class")

// A script on an entity in a scene. The C# class lives in the page, so the
// engine builds no script instance for it. The class name is the file name
// without its extension, which is the rule for C# scripts.
class WebProxyScript : public Script {
	ORBLIT_CLASS(WebProxyScript, Script);

	StringName global_name;

public:
	void set_global_name(const StringName &p_name) { global_name = p_name; }

	bool can_instantiate() const override { return true; }
	Ref<Script> get_base_script() const override { return Ref<Script>(); }
	StringName get_global_name() const override { return global_name; }
	bool inherits_script(const Ref<Script> &p_script) const override { return p_script.ptr() == this; }
	StringName get_instance_base_type() const override { return SNAME("Entity"); }
	// The engine keeps a script only through its instance, so the class name goes
	// on the entity instead, and the entity gets no instance.
	ScriptInstance *instance_create(Object *p_this) override {
		p_this->set_meta(WEB_SCRIPT_CLASS_META, String(global_name));
		return nullptr;
	}
	bool has_source_code() const override { return false; }
	String get_source_code() const override { return String(); }
	void set_source_code(const String &p_code) override {}
	Error reload(bool p_keep_state = false) override { return OK; }
#ifdef TOOLS_ENABLED
	StringName get_doc_class_name() const override { return StringName(); }
	Vector<DocData::ClassDoc> get_documentation() const override { return Vector<DocData::ClassDoc>(); }
	String get_class_icon_path() const override { return String(); }
#endif
	bool has_method(const StringName &p_method) const override { return false; }
	MethodInfo get_method_info(const StringName &p_method) const override { return MethodInfo(); }
	bool is_tool() const override { return false; }
	bool is_script_valid() const override { return true; }
	bool is_abstract() const override { return false; }
	// shortcut: no ScriptLanguage, so the script debugger and script reload must
	// not reach this script. Build a language if the Web build gets a script debugger.
	ScriptLanguage *get_language() const override { return nullptr; }
	bool has_script_signal(const StringName &p_signal) const override { return false; }
	void get_script_signal_list(List<MethodInfo> *r_signals) const override {}
	bool get_property_default_value(const StringName &p_property, Variant &r_value) const override { return false; }
	void get_script_method_list(List<MethodInfo> *r_list) const override {}
	void get_script_property_list(List<PropertyInfo> *r_list) const override {}
	const Variant get_rpc_config() const override { return Variant(); }
};

class ResourceFormatLoaderWebScript : public ResourceFormatLoader {
	ORBLIT_SOFTCLASS(ResourceFormatLoaderWebScript, ResourceFormatLoader);

public:
	Ref<Resource> load(const String &p_path, const String &p_original_path = "", Error *r_error = nullptr, bool p_use_sub_threads = false, float *r_progress = nullptr, CacheMode p_cache_mode = CACHE_MODE_REUSE) override {
		Ref<WebProxyScript> script;
		script.instantiate();
		script->set_global_name(p_path.get_file().get_basename());
		if (r_error) {
			*r_error = OK;
		}
		return script;
	}

	void get_recognized_extensions(List<String> *p_extensions) const override {
		p_extensions->push_back("cs");
	}

	// The C# editor writes the resource type as CSharpScript.
	bool handles_type(const String &p_type) const override {
		return p_type == "Script" || p_type == "CSharpScript";
	}

	String get_resource_type(const String &p_path) const override {
		return p_path.has_extension("cs") ? "CSharpScript" : "";
	}
};

enum ScriptEventKind {
	SCRIPT_EVENT_ATTACH = 0,
	SCRIPT_EVENT_EXIT = 1,
};

static Ref<ResourceFormatLoaderWebScript> script_loader;

// The handle each live script entity holds. The page needs it again when the
// entity leaves.
static HashMap<ObjectID, int32_t> script_handles;

static void _on_entity_added(Entity *p_entity) {
	if (!p_entity->has_meta(WEB_SCRIPT_CLASS_META) || script_handles.has(p_entity->get_instance_id())) {
		return;
	}
	const int32_t handle = web_game_acquire_handle(p_entity);
	if (handle == 0) {
		ERR_PRINT("Web script handles are used up, so this entity gets no C# script.");
		return;
	}
	script_handles[p_entity->get_instance_id()] = handle;
	const Entity3D *entity_3d = Object::cast_to<Entity3D>(p_entity);
	const Vector3 position = entity_3d != nullptr ? entity_3d->get_position() : Vector3();
	const CharString class_name = String(p_entity->get_meta(WEB_SCRIPT_CLASS_META)).utf8();
	orblit_js_script_event(SCRIPT_EVENT_ATTACH, handle, class_name.get_data(), position.x, position.y, position.z);
}

static void _on_entity_removed(Entity *p_entity) {
	HashMap<ObjectID, int32_t>::Iterator found = script_handles.find(p_entity->get_instance_id());
	if (found == script_handles.end()) {
		return;
	}
	const int32_t handle = found->value;
	script_handles.remove(found);
	orblit_js_script_event(SCRIPT_EVENT_EXIT, handle, "", 0.0f, 0.0f, 0.0f);
	web_game_release_handle(handle);
}

static void _attach_tree(Entity *p_entity) {
	_on_entity_added(p_entity);
	for (int i = 0; i < p_entity->get_child_count(); i++) {
		_attach_tree(p_entity->get_child(i));
	}
}

void web_script_proxy_register() {
	ORBLIT_REGISTER_CLASS(WebProxyScript);
	script_loader.instantiate();
	ResourceLoader::add_resource_format_loader(script_loader);
}

void web_script_proxy_unregister() {
	ResourceLoader::remove_resource_format_loader(script_loader);
	script_loader.unref();
	script_handles.clear();
}

void web_script_proxy_start() {
	Stage *stage = Stage::get_singleton();
	stage->connect(SNAME("entity_added"), callable_mp_static(&_on_entity_added));
	stage->connect(SNAME("entity_removed"), callable_mp_static(&_on_entity_removed));
	_attach_tree(stage->get_root());
}
