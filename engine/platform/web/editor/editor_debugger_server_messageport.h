/**************************************************************************/
/*  editor_debugger_server_messageport.h                                  */

#pragma once

#include "../remote_debugger_peer_messageport.h"
#include "js.h"

#include "editor/debugger/editor_debugger_server.h"

class EditorDebuggerServerMessagePort : public EditorDebuggerServer {
	ORBLIT_CLASS(EditorDebuggerServerMessagePort, EditorDebuggerServer);

private:
	List<int> pending;
	static EditorDebuggerServerMessagePort *singleton;

	WASM_EXPORT static void _add_session(int p_session);

public:
	static void initialize();
	static Ref<EditorDebuggerServer> create(const String &p_protocol);

	virtual void poll() override;
	virtual String get_uri() const override;
	virtual Error start(const String &p_uri = "") override;
	virtual void stop() override;
	virtual bool is_active() const override;
	virtual bool is_connection_available() const override;
	virtual Ref<RemoteDebuggerPeer> take_connection() override;

	EditorDebuggerServerMessagePort();
	virtual ~EditorDebuggerServerMessagePort();
};
