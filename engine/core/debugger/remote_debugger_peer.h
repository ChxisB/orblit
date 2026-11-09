/**************************************************************************/
/*  remote_debugger_peer.h                                                */

#pragma once

#include "core/object/ref_counted.h"
#include "core/os/mutex.h"
#include "core/os/thread.h"
#include "core/string/ustring.h"

class StreamPeerSocket;

class RemoteDebuggerPeer : public RefCounted {
	ORBLIT_SOFTCLASS(RemoteDebuggerPeer, RefCounted);

protected:
	int max_queued_messages = 4096;

public:
	virtual bool is_peer_connected() = 0;
	virtual int get_max_message_size() const = 0;
	virtual bool has_message() = 0;
	virtual Error put_message(const Array &p_arr) = 0;
	virtual Array get_message() = 0;
	virtual void close() = 0;
	virtual void poll() = 0;
	virtual bool can_block() const { return true; } // If blocking io is allowed on main thread (debug).

	RemoteDebuggerPeer();
};

class RemoteDebuggerPeerTCP : public RemoteDebuggerPeer {
	ORBLIT_SOFTCLASS(RemoteDebuggerPeerTCP, RemoteDebuggerPeer);

private:
	Ref<StreamPeerSocket> tcp_client;
	Mutex mutex;
	Thread thread;
	List<Array> in_queue;
	List<Array> out_queue;
	int out_left = 0;
	int out_pos = 0;
	Vector<uint8_t> out_buf;
	int in_left = 0;
	int in_pos = 0;
	Vector<uint8_t> in_buf;
	bool connected = false;
	bool running = false;

	static void _thread_func(void *p_ud);

	void _poll();
	void _write_out();
	void _read_in();
	void _disconnect_with_error(const String &p_reason);
	static Error _try_connect(Ref<StreamPeerSocket> p_stream);

public:
	static Ref<RemoteDebuggerPeer> create_tcp(const String &p_uri);
	static Ref<RemoteDebuggerPeer> create_unix(const String &p_uri);

	bool is_peer_connected() override;
	int get_max_message_size() const override;
	bool has_message() override;
	Error put_message(const Array &p_arr) override;
	Array get_message() override;
	void poll() override;
	void close() override;

	RemoteDebuggerPeerTCP(Ref<StreamPeerSocket> p_stream);
	RemoteDebuggerPeerTCP();
	~RemoteDebuggerPeerTCP();
};
