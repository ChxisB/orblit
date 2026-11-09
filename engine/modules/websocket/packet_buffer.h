/**************************************************************************/
/*  packet_buffer.h                                                       */

#pragma once

#include "core/containers/ring_buffer.h"

template <typename T>
class PacketBuffer {
private:
	typedef struct {
		uint32_t size;
		T info;
	} _Packet;

	Vector<_Packet> _packets;
	int _queued = 0;
	int _write_pos = 0;
	int _read_pos = 0;
	RingBuffer<uint8_t> _payload;

public:
	Error write_packet(const uint8_t *p_payload, uint32_t p_size, const T *p_info) {
		ERR_FAIL_COND_V_MSG(p_payload && (uint32_t)_payload.space_left() < p_size, ERR_OUT_OF_MEMORY, "Buffer payload full! Dropping data.");
		ERR_FAIL_COND_V_MSG(p_info && _queued >= _packets.size(), ERR_OUT_OF_MEMORY, "Too many packets in queue! Dropping data.");

		// If p_info is nullptr, only the payload is written
		if (p_info) {
			ERR_FAIL_COND_V(_write_pos > _packets.size(), ERR_OUT_OF_MEMORY);
			_Packet p;
			p.size = p_size;
			p.info = *p_info;
			_packets.write[_write_pos] = p;
			_queued += 1;
			_write_pos++;
			if (_write_pos >= _packets.size()) {
				_write_pos = 0;
			}
		}

		// If p_payload is nullptr, only the packet information is written.
		if (p_payload) {
			_payload.write((const uint8_t *)p_payload, p_size);
		}

		return OK;
	}

	Error read_packet(uint8_t *r_payload, int p_bytes, T *r_info, int &r_read) {
		ERR_FAIL_COND_V(_queued < 1, ERR_UNAVAILABLE);
		_Packet p = _packets[_read_pos];
		_read_pos += 1;
		if (_read_pos >= _packets.size()) {
			_read_pos = 0;
		}
		_queued -= 1;

		ERR_FAIL_COND_V(_payload.data_left() < (int)p.size, ERR_BUG);
		ERR_FAIL_COND_V(p_bytes < (int)p.size, ERR_OUT_OF_MEMORY);

		r_read = p.size;
		memcpy(r_info, &p.info, sizeof(T));
		_payload.read(r_payload, p.size);
		return OK;
	}

	void resize(int p_buf_shift, int p_max_packets) {
		_payload.resize(p_buf_shift);
		_packets.resize(p_max_packets);
		_read_pos = 0;
		_write_pos = 0;
		_queued = 0;
	}

	int packets_left() const {
		return _queued;
	}

	int payload_space_left() const {
		return _payload.space_left();
	}

	int packets_space_left() const {
		return _packets.size() - _queued;
	}

	void clear() {
		_payload.resize(0);
		_packets.resize(0);
		_read_pos = 0;
		_write_pos = 0;
		_queued = 0;
	}

	PacketBuffer() {
		clear();
	}

	~PacketBuffer() {
		clear();
	}
};
