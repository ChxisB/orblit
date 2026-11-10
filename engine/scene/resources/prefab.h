/**************************************************************************/
/*  prefab.h                                                              */

#pragma once

#include "core/io/resource.h"
#include "scene/stage/entity.h"

class Prefab;

class SceneState : public RefCounted {
	ORBLIT_CLASS(SceneState, RefCounted);
	friend class Prefab;

	Vector<StringName> names;
	Vector<Variant> variants;
	Vector<EntityPath> node_paths;
	Vector<PackedInt32Array> id_paths;
	mutable PackedInt32Array ids;
	Vector<EntityPath> editable_instances;
	mutable HashMap<EntityPath, int> node_path_cache;
	mutable HashMap<int, int> base_scene_node_remap;

	int base_scene_idx = -1;

	enum {
		NO_PARENT_SAVED = 0x7FFFFFFF,
		NAME_INDEX_BITS = 18,
		NAME_MASK = (1 << NAME_INDEX_BITS) - 1,
	};

	struct NodeData {
		int parent = 0;
		int owner = 0;
		int type = 0;
		int name = 0;
		int instance = 0;
		int index = 0;

		struct Property {
			int name = 0;
			int value = 0;
		};

		Vector<Property> properties;
		Vector<int> groups;
	};

	struct DeferredEntityPathProperties {
		ObjectID base;
		StringName property;
		Variant value;
	};

	Vector<NodeData> nodes;

	struct ConnectionData {
		int from = 0;
		int to = 0;
		int signal = 0;
		int method = 0;
		int flags = 0;
		int unbinds = 0;
		Vector<int> binds;
	};

	Vector<ConnectionData> connections;

	Error _parse_node(Entity *p_owner, Entity *p_node, int p_parent_idx, HashMap<StringName, int> &name_map, HashMap<Variant, int> &variant_map, HashMap<Entity *, int> &node_map, HashMap<Entity *, int> &nodepath_map, HashSet<int32_t> &ids_saved);
	Error _parse_connections(Entity *p_owner, Entity *p_node, HashMap<StringName, int> &name_map, HashMap<Variant, int> &variant_map, HashMap<Entity *, int> &node_map, HashMap<Entity *, int> &nodepath_map);

	String path;

	uint64_t last_modified_time = 0;

	static bool disable_placeholders;

	Vector<String> _get_node_groups(int p_idx) const;

	int _find_base_scene_node_remap_key(int p_idx) const;

	Entity *_recover_node_path_index(Entity *p_base, int p_idx) const;

	static Variant _duplicate_recursive(const Variant &p_variant, HashMap<Entity *, HashMap<Ref<Resource>, Ref<Resource>>> &p_remap_cache, const Variant &p_fallback, Entity *p_for_scene);

#ifdef TOOLS_ENABLED
public:
	typedef void (*InstantiationWarningNotify)(const String &p_warning);

private:
	static InstantiationWarningNotify instantiation_warn_notify;
#endif

protected:
	static void register_members();

public:
	enum {
		FLAG_ID_IS_PATH = (1 << 30),
		TYPE_INSTANTIATED = 0x7FFFFFFF,
		FLAG_INSTANCE_IS_PLACEHOLDER = (1 << 30),
		FLAG_PATH_PROPERTY_IS_NODE = (1 << 30),
		FLAG_PROP_NAME_MASK = FLAG_PATH_PROPERTY_IS_NODE - 1,
		FLAG_MASK = (1 << 24) - 1,
	};

	enum GenEditState {
		GEN_EDIT_STATE_DISABLED,
		GEN_EDIT_STATE_INSTANCE,
		GEN_EDIT_STATE_MAIN,
		GEN_EDIT_STATE_MAIN_INHERITED,
	};

	struct PackState {
		Ref<SceneState> state;
		int node = -1;
	};

	static void set_disable_placeholders(bool p_disable);
	static Ref<Resource> get_remap_resource(const Ref<Resource> &p_resource, HashMap<Entity *, HashMap<Ref<Resource>, Ref<Resource>>> &remap_cache, const Ref<Resource> &p_fallback, Entity *p_for_scene);

	int find_node_by_path(const EntityPath &p_node) const;
	Variant get_property_value(int p_node, const StringName &p_property, bool &r_found, bool &r_node_deferred) const;
	bool is_node_in_group(int p_node, const StringName &p_group) const;
	bool is_connection(int p_node, const StringName &p_signal, int p_to_node, const StringName &p_to_method) const;

	void set_bundled_scene(const Dictionary &p_dictionary);
	Dictionary get_bundled_scene() const;

	Error pack(Entity *p_scene);

	void set_path(const String &p_path);
	String get_path() const;

	void clear();
	Error copy_from(const Ref<SceneState> &p_scene_state);

	bool can_instantiate() const;
	Entity *instantiate(GenEditState p_edit_state) const;

private:
	Entity *_instantiate(GenEditState p_edit_state, LocalVector<DeferredEntityPathProperties> *r_parent_deferred_node_paths) const;

public:
	Array setup_resources_in_array(Array &array_to_scan, const SceneState::NodeData &n, HashMap<Entity *, HashMap<Ref<Resource>, Ref<Resource>>> &p_resources_local_to_scenes, Entity *node, const StringName sname, int i, Entity **ret_nodes, SceneState::GenEditState p_edit_state) const;
	Dictionary setup_resources_in_dictionary(Dictionary &p_dictionary_to_scan, const SceneState::NodeData &p_n, HashMap<Entity *, HashMap<Ref<Resource>, Ref<Resource>>> &p_resources_local_to_scenes, Entity *p_node, const StringName p_sname, int p_i, Entity **p_ret_nodes, SceneState::GenEditState p_edit_state) const;
	Variant make_local_resource(Variant &value, const SceneState::NodeData &p_node_data, HashMap<Entity *, HashMap<Ref<Resource>, Ref<Resource>>> &p_resources_local_to_scenes, Entity *p_node, const StringName p_sname, int p_i, Entity **p_ret_nodes, SceneState::GenEditState p_edit_state) const;
	bool has_local_resource(const Array &p_array) const;

	Ref<SceneState> get_base_scene_state() const;

	void update_instance_resource(String p_path, Ref<Prefab> p_prefab);

	//unbuild API

	int get_entity_count() const;
	StringName get_entity_type(int p_idx) const;
	StringName get_entity_name(int p_idx) const;
	EntityPath get_entity_path(int p_idx, bool p_for_parent = false) const;
	int32_t get_node_unique_id(int p_idx) const;
	PackedInt32Array get_node_id_path(int p_idx) const;
	PackedInt32Array get_node_parent_id_path(int p_idx) const;
	EntityPath get_entity_owner_path(int p_idx) const;
	PackedInt32Array get_node_owner_id_path(int p_idx) const;
	Ref<Prefab> get_entity_instance(int p_idx) const;
	String get_entity_instance_placeholder(int p_idx) const;
	bool is_entity_instance_placeholder(int p_idx) const;
	Vector<StringName> get_entity_groups(int p_idx) const;
	int get_entity_index(int p_idx) const;

	int get_entity_property_count(int p_idx) const;
	StringName get_entity_property_name(int p_idx, int p_prop) const;
	Variant get_entity_property_value(int p_idx, int p_prop) const;
	Vector<String> get_node_deferred_nodepath_properties(int p_idx) const;

	int get_connection_count() const;
	EntityPath get_connection_source(int p_idx) const;
	StringName get_connection_signal(int p_idx) const;
	EntityPath get_connection_target(int p_idx) const;
	StringName get_connection_method(int p_idx) const;

	PackedInt32Array get_connection_source_id_path(int p_idx) const;
	PackedInt32Array get_connection_target_id_path(int p_idx) const;

	int get_connection_flags(int p_idx) const;
	int get_connection_unbinds(int p_idx) const;
	Array get_connection_binds(int p_idx) const;

	bool has_connection(const EntityPath &p_node_from, const StringName &p_signal, const EntityPath &p_node_to, const StringName &p_method, bool p_no_inheritance = false);

	Vector<EntityPath> get_editable_instances() const;
	Ref<Resource> get_sub_resource(const String &p_path);
	Vector<Ref<Resource>> get_sub_resources();

	//build API

	int add_name(const StringName &p_name);
	int add_value(const Variant &p_value);
	int add_node_path(const EntityPath &p_path, const PackedInt32Array &p_uid_path);
	int add_node(int p_parent, int p_owner, int p_type, int p_name, int p_instance, int p_index, int32_t p_unique_id);
	void add_node_property(int p_node, int p_name, int p_value, bool p_deferred_node_path = false);
	void add_node_group(int p_node, int p_group);
	void set_base_scene(int p_idx);
	void add_connection(int p_from, int p_to, int p_signal, int p_method, int p_flags, int p_unbinds, const Vector<int> &p_binds);
	void add_editable_instance(const EntityPath &p_path);

	bool remove_group_references(const StringName &p_name);
	bool rename_group_references(const StringName &p_old_name, const StringName &p_new_name);
	HashSet<StringName> get_all_groups();

	virtual void set_last_modified_time(uint64_t p_time) { last_modified_time = p_time; }
	uint64_t get_last_modified_time() const { return last_modified_time; }

	// Used when saving pointers (saves a path property instead).
	static String get_meta_pointer_property(const String &p_property);

#ifdef TOOLS_ENABLED
	static void set_instantiation_warning_notify_func(InstantiationWarningNotify p_warn_notify) { instantiation_warn_notify = p_warn_notify; }
#endif

	SceneState();
};

VARIANT_ENUM_CAST(SceneState::GenEditState)

class Prefab : public Resource {
	ORBLIT_CLASS(Prefab, Resource);
	RES_BASE_EXTENSION("oscnb");
	friend class SceneState;

	Ref<SceneState> state;

	void _set_bundled_scene(const Dictionary &p_scene);
	Dictionary _get_bundled_scene() const;

protected:
	virtual bool editor_can_reload_from_file() override { return false; } // this is handled by editor better
	static void register_members();
	virtual void reset_state() override;

public:
	enum GenEditState {
		GEN_EDIT_STATE_DISABLED,
		GEN_EDIT_STATE_INSTANCE,
		GEN_EDIT_STATE_MAIN,
		GEN_EDIT_STATE_MAIN_INHERITED,
	};

	Error pack(Entity *p_scene);

	void clear();

	bool can_instantiate() const;
	Entity *instantiate(GenEditState p_edit_state = GEN_EDIT_STATE_DISABLED) const;

private:
	Entity *_instantiate(GenEditState p_edit_state, LocalVector<SceneState::DeferredEntityPathProperties> *r_parent_deferred_node_paths) const;

public:
	void recreate_state();
	void replace_state(Ref<SceneState> p_by);

	virtual void reload_from_file() override;

	virtual void set_path(const String &p_path, bool p_take_over = false) override;
	virtual void set_path_cache(const String &p_path) override;

#ifdef TOOLS_ENABLED
	virtual void set_last_modified_time(uint64_t p_time) override {
		Resource::set_last_modified_time(p_time);
		state->set_last_modified_time(p_time);
	}

	static HashSet<StringName> get_scene_groups(const String &p_path);
#endif
	Ref<SceneState> get_state() const;

	Prefab();
};

VARIANT_ENUM_CAST(Prefab::GenEditState)
