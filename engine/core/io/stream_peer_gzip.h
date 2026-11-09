/**************************************************************************/
/*  stream_peer_gzip.h                                                    */

#pragma once

#include "core/containers/ring_buffer.h"
#include "core/io/stream_peer.h"

class StreamPeerGZIP : public StreamPeer {
	ORBLIT_CLASS(StreamPeerGZIP, StreamPeer);

private:
	void *ctx = nullptr; // Will hold our z_stream instance.
	bool compressing = true;

	RingBuffer<uint8_t> rb;
	Vector<uint8_t> buffer;

	Error _process(uint8_t *p_dst, int p_dst_size, const uint8_t *p_src, int p_src_size, int &r_consumed, int &r_out, bool p_close = false);
	void _close();
	Error _start(bool p_compress, bool p_is_deflate, int p_buffer_size = 65535);

protected:
	static void register_members();

public:
	Error start_compression(bool p_is_deflate, int p_buffer_size = 65535);
	Error start_decompression(bool p_is_deflate, int p_buffer_size = 65535);

	Error finish();
	void clear();

	virtual Error put_data(const uint8_t *p_data, int p_bytes) override;
	virtual Error put_partial_data(const uint8_t *p_data, int p_bytes, int &r_sent) override;

	virtual Error get_data(uint8_t *p_buffer, int p_bytes) override;
	virtual Error get_partial_data(uint8_t *p_buffer, int p_bytes, int &r_received) override;

	virtual int get_available_bytes() const override;

	~StreamPeerGZIP();
};
