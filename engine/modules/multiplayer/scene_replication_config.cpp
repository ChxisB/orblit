/**************************************************************************/
/*  scene_replication_config.cpp                                          */

#include "scene_replication_config.h"

#include "core/object/class_registry.h"

bool SceneReplicationConfig::_set(const StringName &p_name, const Variant &p_value) {
	String prop_name = p_name;

	if (prop_name.begins_with("properties/")) {
		int idx = prop_name.get_slicec('/', 1).to_int();
		String what = prop_name.get_slicec('/', 2);

		if (properties.size() == idx && what == "path") {
			ERR_FAIL_COND_V(p_value.get_type() != Variant::ENTITY_PATH, false);
			EntityPath path = p_value;
			ERR_FAIL_COND_V(path.is_empty() || path.get_subname_count() == 0, false);
			add_property(path);
			return true;
		}
		ERR_FAIL_INDEX_V(idx, properties.size(), false);
		const ReplicationProperty &prop = properties.get(idx);
		if (what == "replication_mode") {
			ERR_FAIL_COND_V(p_value.get_type() != Variant::INT, false);
			ReplicationMode mode = (ReplicationMode)p_value.operator int();
			ERR_FAIL_COND_V(mode < REPLICATION_MODE_NEVER || mode > REPLICATION_MODE_ON_CHANGE, false);
			property_set_replication_mode(prop.name, mode);
			return true;
		}
		ERR_FAIL_COND_V(p_value.get_type() != Variant::BOOL, false);
		if (what == "spawn") {
			property_set_spawn(prop.name, p_value);
			return true;
		}
	}
	return false;
}

bool SceneReplicationConfig::_get(const StringName &p_name, Variant &r_ret) const {
	String prop_name = p_name;

	if (prop_name.begins_with("properties/")) {
		int idx = prop_name.get_slicec('/', 1).to_int();
		String what = prop_name.get_slicec('/', 2);
		ERR_FAIL_INDEX_V(idx, properties.size(), false);
		const ReplicationProperty &prop = properties.get(idx);
		if (what == "path") {
			r_ret = prop.name;
			return true;
		} else if (what == "spawn") {
			r_ret = prop.spawn;
			return true;
		} else if (what == "replication_mode") {
			r_ret = prop.mode;
			return true;
		}
	}
	return false;
}

void SceneReplicationConfig::_get_property_list(List<PropertyInfo> *p_list) const {
	for (int i = 0; i < properties.size(); i++) {
		p_list->push_back(PropertyInfo(Variant::STRING, "properties/" + itos(i) + "/path", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NO_EDITOR | PROPERTY_USAGE_INTERNAL));
		p_list->push_back(PropertyInfo(Variant::STRING, "properties/" + itos(i) + "/spawn", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NO_EDITOR | PROPERTY_USAGE_INTERNAL));
		p_list->push_back(PropertyInfo(Variant::INT, "properties/" + itos(i) + "/replication_mode", PROPERTY_HINT_ENUM, "Never,Always,On Change", PROPERTY_USAGE_NO_EDITOR | PROPERTY_USAGE_INTERNAL));
	}
}

void SceneReplicationConfig::reset_state() {
	dirty = false;
	properties.clear();
	sync_props.clear();
	spawn_props.clear();
	watch_props.clear();
}

TypedArray<EntityPath> SceneReplicationConfig::get_properties() const {
	TypedArray<EntityPath> paths;
	for (const ReplicationProperty &prop : properties) {
		paths.push_back(prop.name);
	}
	return paths;
}

void SceneReplicationConfig::add_property(const EntityPath &p_path, int p_index) {
	ERR_FAIL_COND(properties.find(p_path));
	ERR_FAIL_COND(p_path == EntityPath());

	if (p_index < 0 || p_index == properties.size()) {
		properties.push_back(ReplicationProperty(p_path));
		dirty = true;
		return;
	}

	ERR_FAIL_INDEX(p_index, properties.size());

	List<ReplicationProperty>::Element *I = properties.front();
	int c = 0;
	while (c < p_index) {
		I = I->next();
		c++;
	}
	properties.insert_before(I, ReplicationProperty(p_path));
	dirty = true;
}

void SceneReplicationConfig::remove_property(const EntityPath &p_path) {
	properties.erase(p_path);
	dirty = true;
}

bool SceneReplicationConfig::has_property(const EntityPath &p_path) const {
	for (const ReplicationProperty &property : properties) {
		if (property.name == p_path) {
			return true;
		}
	}
	return false;
}

int SceneReplicationConfig::property_get_index(const EntityPath &p_path) const {
	int i = 0;
	for (List<ReplicationProperty>::ConstIterator itr = properties.begin(); itr != properties.end(); ++itr, ++i) {
		if (itr->name == p_path) {
			return i;
		}
	}
	ERR_FAIL_V(-1);
}

bool SceneReplicationConfig::property_get_spawn(const EntityPath &p_path) {
	List<ReplicationProperty>::Element *E = properties.find(p_path);
	ERR_FAIL_COND_V(!E, false);
	return E->get().spawn;
}

void SceneReplicationConfig::property_set_spawn(const EntityPath &p_path, bool p_enabled) {
	List<ReplicationProperty>::Element *E = properties.find(p_path);
	ERR_FAIL_COND(!E);
	if (E->get().spawn == p_enabled) {
		return;
	}
	E->get().spawn = p_enabled;
	dirty = true;
}

SceneReplicationConfig::ReplicationMode SceneReplicationConfig::property_get_replication_mode(const EntityPath &p_path) {
	List<ReplicationProperty>::Element *E = properties.find(p_path);
	ERR_FAIL_COND_V(!E, REPLICATION_MODE_NEVER);
	return E->get().mode;
}

void SceneReplicationConfig::property_set_replication_mode(const EntityPath &p_path, ReplicationMode p_mode) {
	List<ReplicationProperty>::Element *E = properties.find(p_path);
	ERR_FAIL_COND(!E);
	if (E->get().mode == p_mode) {
		return;
	}
	E->get().mode = p_mode;
	dirty = true;
}

void SceneReplicationConfig::_update() {
	if (!dirty) {
		return;
	}
	dirty = false;
	sync_props.clear();
	spawn_props.clear();
	watch_props.clear();
	for (const ReplicationProperty &prop : properties) {
		if (prop.spawn) {
			spawn_props.push_back(prop.name);
		}
		switch (prop.mode) {
			case REPLICATION_MODE_ALWAYS:
				sync_props.push_back(prop.name);
				break;
			case REPLICATION_MODE_ON_CHANGE:
				watch_props.push_back(prop.name);
				break;
			default:
				break;
		}
	}
}

const List<EntityPath> &SceneReplicationConfig::get_spawn_properties() {
	if (dirty) {
		_update();
	}
	return spawn_props;
}

const List<EntityPath> &SceneReplicationConfig::get_sync_properties() {
	if (dirty) {
		_update();
	}
	return sync_props;
}

const List<EntityPath> &SceneReplicationConfig::get_watch_properties() {
	if (dirty) {
		_update();
	}
	return watch_props;
}

void SceneReplicationConfig::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_properties"), &SceneReplicationConfig::get_properties);
	ClassRegistry::bind_method(D_METHOD("add_property", "path", "index"), &SceneReplicationConfig::add_property, DEFVAL(-1));
	ClassRegistry::bind_method(D_METHOD("has_property", "path"), &SceneReplicationConfig::has_property);
	ClassRegistry::bind_method(D_METHOD("remove_property", "path"), &SceneReplicationConfig::remove_property);
	ClassRegistry::bind_method(D_METHOD("property_get_index", "path"), &SceneReplicationConfig::property_get_index);
	ClassRegistry::bind_method(D_METHOD("property_get_spawn", "path"), &SceneReplicationConfig::property_get_spawn);
	ClassRegistry::bind_method(D_METHOD("property_set_spawn", "path", "enabled"), &SceneReplicationConfig::property_set_spawn);
	ClassRegistry::bind_method(D_METHOD("property_get_replication_mode", "path"), &SceneReplicationConfig::property_get_replication_mode);
	ClassRegistry::bind_method(D_METHOD("property_set_replication_mode", "path", "mode"), &SceneReplicationConfig::property_set_replication_mode);

	BIND_ENUM_CONSTANT(REPLICATION_MODE_NEVER);
	BIND_ENUM_CONSTANT(REPLICATION_MODE_ALWAYS);
	BIND_ENUM_CONSTANT(REPLICATION_MODE_ON_CHANGE);
}
