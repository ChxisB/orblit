/**************************************************************************/
/*  scene_cache_interface.h                                               */

#pragma once

#include "core/object/ref_counted.h"

class Entity;
class SceneMultiplayer;

class SceneCacheInterface : public RefCounted {
	ORBLIT_CLASS(SceneCacheInterface, RefCounted);

private:
	SceneMultiplayer *multiplayer = nullptr;

	//path sent caches
	struct NodeCache {
		int cache_id = 0;
		HashMap<int, int> recv_ids; // peer id, remote cache id
		HashMap<int, bool> confirmed_peers; // peer id, confirmed
	};

	struct RecvNode {
		ObjectID oid;
		EntityPath path;

		RecvNode(const ObjectID &p_oid, const EntityPath &p_path) {
			oid = p_oid;
			path = p_path;
		}
	};

	struct PeerInfo {
		HashMap<int, RecvNode> recv_nodes; // remote cache id, (ObjectID, EntityPath)
		HashSet<ObjectID> sent_nodes;
	};

	HashMap<ObjectID, NodeCache> nodes_cache;
	HashMap<int, ObjectID> assigned_ids;
	HashMap<int, PeerInfo> peers_info;
	int last_send_cache_id = 1;

	void _remove_node_cache(ObjectID p_oid);
	NodeCache &_track(Entity *p_node);

protected:
	Error _send_confirm_path(Entity *p_node, NodeCache &p_cache, const List<int> &p_peers);

public:
	void clear();
	void on_peer_change(int p_id, bool p_connected);
	void process_simplify_path(int p_from, const uint8_t *p_packet, int p_packet_len);
	void process_confirm_path(int p_from, const uint8_t *p_packet, int p_packet_len);

	// Returns true if all peers have cached path.
	bool send_object_cache(Object *p_obj, int p_target, int &p_id);
	int make_object_cache(Object *p_obj);
	Object *get_cached_object(int p_from, uint32_t p_cache_id);
	bool is_cache_confirmed(Entity *p_path, int p_peer);

	SceneCacheInterface(SceneMultiplayer *p_multiplayer) { multiplayer = p_multiplayer; }
};
