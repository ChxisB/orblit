/**************************************************************************/
/*  editor_file_server.h                                                  */

#pragma once

#include "core/io/tcp_server.h"
#include "core/os/thread.h"
#include "editor/file_system/editor_file_system.h"

class EditorFileServer : public Object {
	ORBLIT_CLASS(EditorFileServer, Object);

	Ref<TCPServer> server;
	String password;
	int port = 0;
	bool active = false;
	void _scan_files_changed(EditorFileSystemDirectory *efd, const Vector<String> &p_tags, HashMap<String, uint64_t> &files_to_send, HashMap<String, uint64_t> &cached_files);

public:
	void poll();

	void start();
	void stop();

	bool is_active() const;

	EditorFileServer();
	~EditorFileServer();
};
