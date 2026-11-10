/**************************************************************************/
/*  scene_replication_config.h                                            */

#pragma once

#include "core/io/resource.h"
#include "core/variant/typed_array.h"

class SceneReplicationConfig : public Resource {
	ORBLIT_CLASS(SceneReplicationConfig, Resource);
	OBJ_SAVE_TYPE(SceneReplicationConfig);
	RES_BASE_EXTENSION("repl");

public:
	enum ReplicationMode {
		REPLICATION_MODE_NEVER,
		REPLICATION_MODE_ALWAYS,
		REPLICATION_MODE_ON_CHANGE,
	};

private:
	struct ReplicationProperty {
		EntityPath name;
		bool spawn = true;
		ReplicationMode mode = REPLICATION_MODE_ALWAYS;

		bool operator==(const ReplicationProperty &p_to) {
			return name == p_to.name;
		}

		ReplicationProperty() {}

		ReplicationProperty(const EntityPath &p_name) {
			name = p_name;
		}
	};

	List<ReplicationProperty> properties;
	List<EntityPath> spawn_props;
	List<EntityPath> sync_props;
	List<EntityPath> watch_props;
	bool dirty = false;

	void _update();

protected:
	static void register_members();

	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;

public:
	virtual void reset_state() override; // Required since we use variable amount of properties.

	TypedArray<EntityPath> get_properties() const;

	void add_property(const EntityPath &p_path, int p_index = -1);
	void remove_property(const EntityPath &p_path);
	bool has_property(const EntityPath &p_path) const;

	int property_get_index(const EntityPath &p_path) const;
	bool property_get_spawn(const EntityPath &p_path);
	void property_set_spawn(const EntityPath &p_path, bool p_enabled);

	ReplicationMode property_get_replication_mode(const EntityPath &p_path);
	void property_set_replication_mode(const EntityPath &p_path, ReplicationMode p_mode);

	const List<EntityPath> &get_spawn_properties();
	const List<EntityPath> &get_sync_properties();
	const List<EntityPath> &get_watch_properties();

	SceneReplicationConfig() {}
};

VARIANT_ENUM_CAST(SceneReplicationConfig::ReplicationMode);
