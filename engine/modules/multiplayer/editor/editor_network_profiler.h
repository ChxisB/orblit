/**************************************************************************/
/*  editor_network_profiler.h                                             */

#pragma once

#include "../multiplayer_debugger.h"

#include "scene/debugger/scene_debugger.h"
#include "scene/ui/box_container.h"
#include "scene/ui/button.h"
#include "scene/ui/label.h"
#include "scene/ui/tree.h"

class Timer;

class EditorNetworkProfiler : public VBoxContainer {
	ORBLIT_CLASS(EditorNetworkProfiler, VBoxContainer)

public:
	struct NodeInfo {
		ObjectID id;
		String type;
		String path;

		NodeInfo() {}
		NodeInfo(const ObjectID &p_id) {
			id = p_id;
			path = String::num_int64(p_id);
		}
	};

private:
	using RPCNodeInfo = MultiplayerDebugger::RPCNodeInfo;
	using SyncInfo = MultiplayerDebugger::SyncInfo;

	bool dirty = false;
	Timer *refresh_timer = nullptr;
	Button *activate = nullptr;
	Button *clear_button = nullptr;
	Tree *counters_display = nullptr;
	LineEdit *incoming_bandwidth_text = nullptr;
	LineEdit *outgoing_bandwidth_text = nullptr;
	Tree *replication_display = nullptr;

	Label *up_label = nullptr;
	Label *down_label = nullptr;

	int incoming_bandwidth = 0;
	int outgoing_bandwidth = 0;

	HashMap<ObjectID, RPCNodeInfo> rpc_data;
	HashMap<ObjectID, SyncInfo> sync_data;
	HashMap<ObjectID, NodeInfo> node_data;
	HashSet<ObjectID> missing_node_data;

	struct ThemeCache {
		Ref<Texture2D> node_icon;
		Ref<Texture2D> stop_icon;
		Ref<Texture2D> play_icon;
		Ref<Texture2D> clear_icon;

		Ref<Texture2D> multiplayer_synchronizer_icon;
		Ref<Texture2D> instance_options_icon;

		Ref<Texture2D> incoming_bandwidth_icon;
		Ref<Texture2D> outgoing_bandwidth_icon;

		Color incoming_bandwidth_color;
		Color outgoing_bandwidth_color;
	} theme_cache;

	void _activate_pressed();
	void _clear_pressed();
	void _autostart_toggled(bool p_toggled_on);
	void _refresh();
	void _update_button_text();
	void _replication_button_clicked(TreeItem *p_item, int p_column, int p_idx, MouseButton p_button);

protected:
	virtual void _update_theme_item_cache() override;

	void _notification(int p_what);
	static void register_members();

public:
	void refresh_rpc_data();
	void refresh_replication_data();

	Array pop_missing_node_data();
	void add_node_data(const NodeInfo &p_info);
	void add_rpc_frame_data(const RPCNodeInfo &p_frame);
	void add_sync_frame_data(const SyncInfo &p_frame);
	void set_bandwidth(int p_incoming, int p_outgoing);
	bool is_profiling();

	void set_profiling(bool p_pressed);
	void started();
	void stopped();

	EditorNetworkProfiler();
};
