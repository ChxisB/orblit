/**************************************************************************/
/*  packet_peer_dtls.h                                                    */

#pragma once

#include "core/crypto/crypto.h"
#include "core/io/packet_peer_udp.h"

class PacketPeerDTLS : public PacketPeer {
	ORBLIT_CLASS(PacketPeerDTLS, PacketPeer);

protected:
	static inline PacketPeerDTLS *(*_create)(bool p_notify_postinitialize) = nullptr;
	static void register_members();

	static inline bool available = false;

public:
	enum Status {
		STATUS_DISCONNECTED,
		STATUS_HANDSHAKING,
		STATUS_CONNECTED,
		STATUS_ERROR,
		STATUS_ERROR_HOSTNAME_MISMATCH
	};

	virtual void poll() = 0;
	virtual Error connect_to_peer(Ref<PacketPeerUDP> p_base, const String &p_hostname, Ref<TLSOptions> p_options = Ref<TLSOptions>()) = 0;
	virtual void disconnect_from_peer() = 0;
	virtual Status get_status() const = 0;

	static PacketPeerDTLS *create(bool p_notify_postinitialize = true);
	static bool is_available();
};

VARIANT_ENUM_CAST(PacketPeerDTLS::Status);
