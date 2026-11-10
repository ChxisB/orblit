/**************************************************************************/
/*  remote_debugger_peer_messageport.h                                    */

#pragma once

#include "js.h"

#include "core/debugger/remote_debugger_peer.h"

class RemoteDebuggerPeerMessagePort : public RemoteDebuggerPeer {
	ORBLIT_SOFTCLASS(RemoteDebuggerPeerMessagePort, RemoteDebuggerPeer);

	List<Array> in_queue;
	Vector<uint8_t> out_buffer;

	int max_queued_messages;

	int _js_id = -1;

	WASM_EXPORT static void on_debug_message(void *p_ref, const uint8_t *p_buffer, size_t p_len);

public:
	static Ref<RemoteDebuggerPeer> create(const String &p_uri);

	Error connect_to_host(const String &p_uri);

	virtual bool is_peer_connected() override;
	virtual int get_max_message_size() const override;
	virtual bool has_message() override;
	virtual Error put_message(const Array &p_arr) override;
	virtual Array get_message() override;
	virtual void close() override;
	virtual void poll() override;
	virtual bool can_block() const override;

	RemoteDebuggerPeerMessagePort(int p_id = -1);
	virtual ~RemoteDebuggerPeerMessagePort();
};
