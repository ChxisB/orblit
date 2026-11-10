/**************************************************************************/
/*  multiplayer_peer.cpp                                                  */

#include "multiplayer_peer.h"

#include "core/object/class_registry.h"
#include "core/os/os.h"

uint32_t MultiplayerPeer::generate_unique_id() const {
	uint32_t hash = 0;

	while (hash == 0 || hash == 1) {
		hash = hash_murmur3_one_32(
				(uint32_t)OS::get_singleton()->get_ticks_usec());
		hash = hash_murmur3_one_32(
				(uint32_t)OS::get_singleton()->get_unix_time(), hash);
		hash = hash_murmur3_one_32(
				(uint32_t)OS::get_singleton()->get_user_data_dir().hash64(), hash);
		hash = hash_murmur3_one_32(
				(uint32_t)((uint64_t)this), hash); // Rely on ASLR heap
		hash = hash_murmur3_one_32(
				(uint32_t)((uint64_t)&hash), hash); // Rely on ASLR stack

		hash = hash_fmix32(hash);
		hash = hash & 0x7FFFFFFF; // Make it compatible with unsigned, since negative ID is used for exclusion
	}

	return hash;
}

void MultiplayerPeer::set_transfer_channel(int p_channel) {
	transfer_channel = p_channel;
}

int MultiplayerPeer::get_transfer_channel() const {
	return transfer_channel;
}

void MultiplayerPeer::set_transfer_mode(TransferMode p_mode) {
	transfer_mode = p_mode;
}

MultiplayerPeer::TransferMode MultiplayerPeer::get_transfer_mode() const {
	return transfer_mode;
}

void MultiplayerPeer::set_refuse_new_connections(bool p_enable) {
	refuse_connections = p_enable;
}

bool MultiplayerPeer::is_refusing_new_connections() const {
	return refuse_connections;
}

bool MultiplayerPeer::is_server_relay_supported() const {
	return false;
}

void MultiplayerPeer::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_transfer_channel", "channel"), &MultiplayerPeer::set_transfer_channel);
	ClassRegistry::bind_method(D_METHOD("get_transfer_channel"), &MultiplayerPeer::get_transfer_channel);
	ClassRegistry::bind_method(D_METHOD("set_transfer_mode", "mode"), &MultiplayerPeer::set_transfer_mode);
	ClassRegistry::bind_method(D_METHOD("get_transfer_mode"), &MultiplayerPeer::get_transfer_mode);
	ClassRegistry::bind_method(D_METHOD("set_target_peer", "id"), &MultiplayerPeer::set_target_peer);

	ClassRegistry::bind_method(D_METHOD("get_packet_peer"), &MultiplayerPeer::get_packet_peer);
	ClassRegistry::bind_method(D_METHOD("get_packet_channel"), &MultiplayerPeer::get_packet_channel);
	ClassRegistry::bind_method(D_METHOD("get_packet_mode"), &MultiplayerPeer::get_packet_mode);

	ClassRegistry::bind_method(D_METHOD("poll"), &MultiplayerPeer::poll);
	ClassRegistry::bind_method(D_METHOD("close"), &MultiplayerPeer::close);
	ClassRegistry::bind_method(D_METHOD("disconnect_peer", "peer", "force"), &MultiplayerPeer::disconnect_peer, DEFVAL(false));

	ClassRegistry::bind_method(D_METHOD("get_connection_status"), &MultiplayerPeer::get_connection_status);
	ClassRegistry::bind_method(D_METHOD("get_unique_id"), &MultiplayerPeer::get_unique_id);
	ClassRegistry::bind_method(D_METHOD("generate_unique_id"), &MultiplayerPeer::generate_unique_id);

	ClassRegistry::bind_method(D_METHOD("set_refuse_new_connections", "enable"), &MultiplayerPeer::set_refuse_new_connections);
	ClassRegistry::bind_method(D_METHOD("is_refusing_new_connections"), &MultiplayerPeer::is_refusing_new_connections);

	ClassRegistry::bind_method(D_METHOD("is_server_relay_supported"), &MultiplayerPeer::is_server_relay_supported);

	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "refuse_new_connections"), "set_refuse_new_connections", "is_refusing_new_connections");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "transfer_mode", PROPERTY_HINT_ENUM, "Unreliable,Unreliable Ordered,Reliable"), "set_transfer_mode", "get_transfer_mode");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "transfer_channel", PROPERTY_HINT_RANGE, "0,255,1"), "set_transfer_channel", "get_transfer_channel");

	BIND_ENUM_CONSTANT(CONNECTION_DISCONNECTED);
	BIND_ENUM_CONSTANT(CONNECTION_CONNECTING);
	BIND_ENUM_CONSTANT(CONNECTION_CONNECTED);

	BIND_CONSTANT(TARGET_PEER_BROADCAST);
	BIND_CONSTANT(TARGET_PEER_SERVER);

	BIND_ENUM_CONSTANT(TRANSFER_MODE_UNRELIABLE);
	BIND_ENUM_CONSTANT(TRANSFER_MODE_UNRELIABLE_ORDERED);
	BIND_ENUM_CONSTANT(TRANSFER_MODE_RELIABLE);

	ADD_SIGNAL(MethodInfo("peer_connected", PropertyInfo(Variant::INT, "id")));
	ADD_SIGNAL(MethodInfo("peer_disconnected", PropertyInfo(Variant::INT, "id")));
}

/*************/

Error MultiplayerPeerExtension::get_packet(const uint8_t **r_buffer, int &r_buffer_size) {
	Error err;
	if (ORBLIT_VIRTUAL_CALL(_get_packet, r_buffer, &r_buffer_size, err)) {
		return err;
	}
	if (ORBLIT_VIRTUAL_IS_OVERRIDDEN(_get_packet_script)) {
		if (!ORBLIT_VIRTUAL_CALL(_get_packet_script, script_buffer)) {
			return FAILED;
		}

		if (script_buffer.is_empty()) {
			return Error::ERR_UNAVAILABLE;
		}

		*r_buffer = script_buffer.ptr();
		r_buffer_size = script_buffer.size();

		return Error::OK;
	}
	WARN_PRINT_ONCE("MultiplayerPeerExtension::_get_packet_native is unimplemented!");
	return FAILED;
}

Error MultiplayerPeerExtension::put_packet(const uint8_t *p_buffer, int p_buffer_size) {
	ERR_FAIL_COND_V(p_buffer_size < 0, ERR_INVALID_PARAMETER);
	Error err;
	if (ORBLIT_VIRTUAL_CALL(_put_packet, p_buffer, p_buffer_size, err)) {
		return err;
	}
	if (ORBLIT_VIRTUAL_IS_OVERRIDDEN(_put_packet_script)) {
		PackedByteArray a;
		a.resize(p_buffer_size);
		memcpy(a.ptrw(), p_buffer, p_buffer_size);

		if (!ORBLIT_VIRTUAL_CALL(_put_packet_script, a, err)) {
			return FAILED;
		}
		return err;
	}
	WARN_PRINT_ONCE("MultiplayerPeerExtension::_put_packet_native is unimplemented!");
	return FAILED;
}

void MultiplayerPeerExtension::set_refuse_new_connections(bool p_enable) {
	if (ORBLIT_VIRTUAL_CALL(_set_refuse_new_connections, p_enable)) {
		return;
	}
	MultiplayerPeer::set_refuse_new_connections(p_enable);
}

bool MultiplayerPeerExtension::is_refusing_new_connections() const {
	bool refusing;
	if (ORBLIT_VIRTUAL_CALL(_is_refusing_new_connections, refusing)) {
		return refusing;
	}
	return MultiplayerPeer::is_refusing_new_connections();
}

bool MultiplayerPeerExtension::is_server_relay_supported() const {
	bool can_relay;
	if (ORBLIT_VIRTUAL_CALL(_is_server_relay_supported, can_relay)) {
		return can_relay;
	}
	return MultiplayerPeer::is_server_relay_supported();
}

void MultiplayerPeerExtension::register_members() {
	ORBLIT_VIRTUAL_BIND(_get_packet, "r_buffer", "r_buffer_size");
	ORBLIT_VIRTUAL_BIND(_put_packet, "buffer", "buffer_size");
	ORBLIT_VIRTUAL_BIND(_get_available_packet_count);
	ORBLIT_VIRTUAL_BIND(_get_max_packet_size);

	ORBLIT_VIRTUAL_BIND(_get_packet_script)
	ORBLIT_VIRTUAL_BIND(_put_packet_script, "buffer");

	ORBLIT_VIRTUAL_BIND(_get_packet_channel);
	ORBLIT_VIRTUAL_BIND(_get_packet_mode);

	ORBLIT_VIRTUAL_BIND(_set_transfer_channel, "channel");
	ORBLIT_VIRTUAL_BIND(_get_transfer_channel);

	ORBLIT_VIRTUAL_BIND(_set_transfer_mode, "mode");
	ORBLIT_VIRTUAL_BIND(_get_transfer_mode);

	ORBLIT_VIRTUAL_BIND(_set_target_peer, "peer");

	ORBLIT_VIRTUAL_BIND(_get_packet_peer);
	ORBLIT_VIRTUAL_BIND(_is_server);
	ORBLIT_VIRTUAL_BIND(_poll);
	ORBLIT_VIRTUAL_BIND(_close);
	ORBLIT_VIRTUAL_BIND(_disconnect_peer, "peer", "force");
	ORBLIT_VIRTUAL_BIND(_get_unique_id);
	ORBLIT_VIRTUAL_BIND(_set_refuse_new_connections, "enable");
	ORBLIT_VIRTUAL_BIND(_is_refusing_new_connections);
	ORBLIT_VIRTUAL_BIND(_is_server_relay_supported);
	ORBLIT_VIRTUAL_BIND(_get_connection_status);

	ADD_PROPERTY_DEFAULT("transfer_mode", TRANSFER_MODE_RELIABLE);
	ADD_PROPERTY_DEFAULT("transfer_channel", 0);
}
