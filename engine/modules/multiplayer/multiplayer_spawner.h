/**************************************************************************/
/*  multiplayer_spawner.h                                                 */

#pragma once

#include "core/containers/local_vector.h"
#include "scene/stage/entity.h"
#include "scene/resources/prefab.h"

class MultiplayerSpawner : public Entity {
	ORBLIT_CLASS(MultiplayerSpawner, Entity);

public:
	enum {
		INVALID_ID = 0xFF,
	};

private:
	struct SpawnableScene {
		String path;
		Ref<Prefab> cache;
	};

	LocalVector<SpawnableScene> spawnable_scenes;

	HashSet<ResourceUID::ID> spawnable_ids;
	EntityPath spawn_path;

	struct SpawnInfo {
		Variant args;
		int id = INVALID_ID;
		SpawnInfo(Variant p_args, int p_id) {
			id = p_id;
			args = p_args;
		}
		SpawnInfo() {}
	};

	ObjectID spawn_node;
	HashMap<ObjectID, SpawnInfo> tracked_nodes;
	uint32_t spawn_limit = 0;
	Callable spawn_function;

	void _update_spawn_node();
	void _track(Entity *p_node, const Variant &p_argument, int p_scene_id = INVALID_ID);
	void _node_added(Entity *p_node);
	void _node_exit(ObjectID p_id);
	void _spawn_notify(ObjectID p_id);

	Vector<String> _get_spawnable_scenes() const;
	void _set_spawnable_scenes(const Vector<String> &p_scenes);

protected:
	static void register_members();
	void _notification(int p_what);

#ifdef TOOLS_ENABLED
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;
#endif
public:
	PackedStringArray get_configuration_warnings() const override;

	Entity *get_spawn_node() const {
		return spawn_node.is_valid() ? ObjectRegistry::get_instance<Entity>(spawn_node) : nullptr;
	}

	void add_spawnable_scene(const String &p_path);
	int get_spawnable_scene_count() const;
	String get_spawnable_scene(int p_idx) const;
	void clear_spawnable_scenes();

	EntityPath get_spawn_path() const;
	void set_spawn_path(const EntityPath &p_path);
	uint32_t get_spawn_limit() const { return spawn_limit; }
	void set_spawn_limit(uint32_t p_limit) { spawn_limit = p_limit; }
	void set_spawn_function(Callable p_spawn_function) { spawn_function = p_spawn_function; }
	Callable get_spawn_function() const { return spawn_function; }

	const Variant get_spawn_argument(const ObjectID &p_id) const;
	int find_spawnable_scene_index_from_object(const ObjectID &p_id) const;
	int find_spawnable_scene_index_from_path(const String &p_path) const;
	Entity *spawn(const Variant &p_data = Variant());
	Entity *instantiate_custom(const Variant &p_data);
	Entity *instantiate_scene(int p_idx);

	MultiplayerSpawner() {}
};
