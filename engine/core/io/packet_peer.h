/**************************************************************************/
/*  packet_peer.h                                                         */

#pragma once

#include "core/containers/ring_buffer.h"
#include "core/io/stream_peer.h"
#include "core/native_extension/ext_wrappers.gen.h"
#include "core/object/orblit_virtual.gen.h"
#include "core/variant/native_ptr.h"

class PacketPeer : public RefCounted {
	ORBLIT_CLASS(PacketPeer, RefCounted);

	Variant _bnd_get_var(bool p_allow_objects = false);

	static void register_members();

	Error _put_packet(const Vector<uint8_t> &p_buffer);
	Vector<uint8_t> _get_packet();
	Error _get_packet_error() const;

	mutable Error last_get_error = OK;

	int encode_buffer_max_size = 8 * 1024 * 1024;
	Vector<uint8_t> encode_buffer;

public:
	virtual int get_available_packet_count() const = 0;
	virtual Error get_packet(const uint8_t **r_buffer, int &r_buffer_size) = 0; ///< buffer is GONE after next get_packet
	virtual Error put_packet(const uint8_t *p_buffer, int p_buffer_size) = 0;

	virtual int get_max_packet_size() const = 0;

	/* helpers / binders */

	virtual Error get_packet_buffer(Vector<uint8_t> &r_buffer);
	virtual Error put_packet_buffer(const Vector<uint8_t> &p_buffer);

	virtual Error get_var(Variant &r_variant, bool p_allow_objects = false);
	virtual Error put_var(const Variant &p_packet, bool p_full_objects = false);

	void set_encode_buffer_max_size(int p_max_size);
	int get_encode_buffer_max_size() const;
};

class PacketPeerExtension : public PacketPeer {
	ORBLIT_CLASS(PacketPeerExtension, PacketPeer);

protected:
	static void register_members();

public:
	virtual Error get_packet(const uint8_t **r_buffer, int &r_buffer_size) override; ///< buffer is GONE after next get_packet
	ORBLIT_VIRTUAL2R(Error, _get_packet, NativeExtensionPtr<const uint8_t *>, NativeExtensionPtr<int>);

	virtual Error put_packet(const uint8_t *p_buffer, int p_buffer_size) override;
	ORBLIT_VIRTUAL2R(Error, _put_packet, NativeExtensionPtr<const uint8_t>, int);

	EXBIND0RC(int, get_available_packet_count);
	EXBIND0RC(int, get_max_packet_size);
};

class PacketPeerStream : public PacketPeer {
	ORBLIT_CLASS(PacketPeerStream, PacketPeer);

	//the way the buffers work sucks, will change later

	mutable Ref<StreamPeer> peer;
	mutable RingBuffer<uint8_t> ring_buffer;
	mutable Vector<uint8_t> input_buffer;
	mutable Vector<uint8_t> output_buffer;

	Error _poll_buffer() const;

protected:
	static void register_members();

public:
	virtual int get_available_packet_count() const override;
	virtual Error get_packet(const uint8_t **r_buffer, int &r_buffer_size) override;
	virtual Error put_packet(const uint8_t *p_buffer, int p_buffer_size) override;

	virtual int get_max_packet_size() const override;

	void set_stream_peer(const Ref<StreamPeer> &p_peer);
	Ref<StreamPeer> get_stream_peer() const;
	void set_input_buffer_max_size(int p_max_size);
	int get_input_buffer_max_size() const;
	void set_output_buffer_max_size(int p_max_size);
	int get_output_buffer_max_size() const;
	PacketPeerStream();
};
