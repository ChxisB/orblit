/**************************************************************************/
/*  multiplayer_debugger.h                                                */

#pragma once

#include "core/debugger/engine_profiler.h"

class MultiplayerSynchronizer;

class MultiplayerDebugger {
public:
	struct RPCNodeInfo {
		ObjectID node;
		String node_path;
		int incoming_rpc = 0;
		int incoming_size = 0;
		int outgoing_rpc = 0;
		int outgoing_size = 0;
	};

	struct RPCFrame {
		Vector<RPCNodeInfo> infos;

		Array serialize();
		bool deserialize(const Array &p_arr);
	};

	struct SyncInfo {
		ObjectID synchronizer;
		ObjectID config;
		ObjectID root_node;
		int incoming_syncs = 0;
		int incoming_size = 0;
		int outgoing_syncs = 0;
		int outgoing_size = 0;

		void write_to_array(Array &r_arr) const;
		bool read_from_array(const Array &p_arr, int p_offset);

		SyncInfo() {}
		SyncInfo(MultiplayerSynchronizer *p_sync);
	};

	struct ReplicationFrame {
		HashMap<ObjectID, SyncInfo> infos;

		Array serialize();
		bool deserialize(const Array &p_arr);
	};

private:
	class BandwidthProfiler : public EngineProfiler {
		ORBLIT_SOFTCLASS(BandwidthProfiler, EngineProfiler);

	protected:
		struct BandwidthFrame {
			uint32_t timestamp;
			int packet_size;
		};

		int bandwidth_in_ptr = 0;
		Vector<BandwidthFrame> bandwidth_in;
		int bandwidth_out_ptr = 0;
		Vector<BandwidthFrame> bandwidth_out;
		uint64_t last_bandwidth_time = 0;

		int bandwidth_usage(const Vector<BandwidthFrame> &p_buffer, int p_pointer);

	public:
		void toggle(bool p_enable, const Array &p_opts) override;
		void add(const Array &p_data) override;
		void tick(double p_frame_time, double p_process_time, double p_physics_time, double p_physics_frame_time) override;
	};

	class RPCProfiler : public EngineProfiler {
		ORBLIT_SOFTCLASS(RPCProfiler, EngineProfiler);

	private:
		HashMap<ObjectID, RPCNodeInfo> rpc_node_data;
		uint64_t last_profile_time = 0;

		void init_node(const ObjectID p_node);

	public:
		void toggle(bool p_enable, const Array &p_opts) override;
		void add(const Array &p_data) override;
		void tick(double p_frame_time, double p_process_time, double p_physics_time, double p_physics_frame_time) override;
	};

	class ReplicationProfiler : public EngineProfiler {
		ORBLIT_SOFTCLASS(ReplicationProfiler, EngineProfiler);

	private:
		HashMap<ObjectID, SyncInfo> sync_data;
		uint64_t last_profile_time = 0;

	public:
		void toggle(bool p_enable, const Array &p_opts) override;
		void add(const Array &p_data) override;
		void tick(double p_frame_time, double p_process_time, double p_physics_time, double p_physics_frame_time) override;
	};

	static Error _capture(void *p_user, const String &p_msg, const Array &p_args, bool &r_captured);

public:
	static void initialize();
	static void deinitialize();
};
