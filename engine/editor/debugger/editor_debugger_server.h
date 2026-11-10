/**************************************************************************/
/*  editor_debugger_server.h                                              */

#pragma once

#include "core/debugger/remote_debugger_peer.h"
#include "core/object/ref_counted.h"

class EditorDebuggerServer : public RefCounted {
	ORBLIT_SOFTCLASS(EditorDebuggerServer, RefCounted);

public:
	typedef Ref<EditorDebuggerServer> (*CreateServerFunc)(const String &p_uri);

private:
	static HashMap<StringName, CreateServerFunc> protocols;

public:
	static void initialize();
	static void deinitialize();

	static void register_protocol_handler(const String &p_protocol, CreateServerFunc p_func);
	static Ref<EditorDebuggerServer> create(const String &p_protocol);

	virtual String get_uri() const = 0;
	virtual void poll() = 0;
	virtual Error start(const String &p_uri) = 0;
	virtual void stop() = 0;
	virtual bool is_active() const = 0;
	virtual bool is_connection_available() const = 0;
	virtual Ref<RemoteDebuggerPeer> take_connection() = 0;
};
