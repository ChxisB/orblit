/**************************************************************************/
/*  webrtc_multiplayer_peer.h                                             */

#pragma once

#include "webrtc_peer_connection.h"

#include "scene/stage/multiplayer_peer.h"

class WebRTCMultiplayerPeer : public MultiplayerPeer {
	ORBLIT_CLASS(WebRTCMultiplayerPeer, MultiplayerPeer);

protected:
	static void register_members();

private:
	enum {
		CH_RELIABLE = 0,
		CH_ORDERED = 1,
		CH_UNRELIABLE = 2,
		CH_RESERVED_MAX = 3
	};

	enum NetworkMode {
		MODE_NONE,
		MODE_SERVER,
		MODE_CLIENT,
		MODE_MESH,
	};

	class ConnectedPeer : public RefCounted {
	public:
		Ref<WebRTCPeerConnection> connection;
		List<Ref<WebRTCDataChannel>> channels;
		bool connected;

		ConnectedPeer() {
			connected = false;
			for (int i = 0; i < CH_RESERVED_MAX; i++) {
				channels.push_front(Ref<WebRTCDataChannel>());
			}
		}
	};

	uint32_t unique_id = 0;
	int target_peer = 0;
	int client_count = 0;
	ConnectionStatus connection_status = CONNECTION_DISCONNECTED;
	int next_packet_peer = 0;
	int next_packet_channel = 0;
	NetworkMode network_mode = MODE_NONE;

	HashMap<int, Ref<ConnectedPeer>> peer_map;
	List<TransferMode> channels_modes;
	List<Dictionary> channels_config;

	void _peer_to_dict(const Ref<ConnectedPeer> &p_connected_peer, Dictionary &r_dict);
	void _find_next_peer();
	Ref<ConnectedPeer> _get_next_peer();
	Error _initialize(int p_self_id, NetworkMode p_mode, const Array &p_channels_config = Array());

public:
	WebRTCMultiplayerPeer() {}
	~WebRTCMultiplayerPeer();

	Error create_server(const Array &p_channels_config = Array());
	Error create_client(int p_self_id, const Array &p_channels_config = Array());
	Error create_mesh(int p_self_id, const Array &p_channels_config = Array());
	Error add_peer(const Ref<WebRTCPeerConnection> &p_peer, int p_peer_id, int p_unreliable_lifetime = 1);
	void remove_peer(int p_peer_id);
	bool has_peer(int p_peer_id);
	Dictionary get_peer(int p_peer_id);
	Dictionary get_peers();

	// PacketPeer
	virtual Error get_packet(const uint8_t **r_buffer, int &r_buffer_size) override; ///< buffer is GONE after next get_packet
	virtual Error put_packet(const uint8_t *p_buffer, int p_buffer_size) override;
	virtual int get_available_packet_count() const override;
	virtual int get_max_packet_size() const override;

	// MultiplayerPeer
	virtual void set_target_peer(int p_peer_id) override;

	virtual int get_unique_id() const override;
	virtual int get_packet_peer() const override;
	virtual int get_packet_channel() const override;
	virtual TransferMode get_packet_mode() const override;

	virtual bool is_server() const override;
	virtual bool is_server_relay_supported() const override;

	virtual void poll() override;
	virtual void close() override;
	virtual void disconnect_peer(int p_peer_id, bool p_force = false) override;

	virtual ConnectionStatus get_connection_status() const override;
};
