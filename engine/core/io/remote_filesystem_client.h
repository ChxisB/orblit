/**************************************************************************/
/*  remote_filesystem_client.h                                            */

#pragma once

#include "core/containers/hash_set.h"
#include "core/containers/local_vector.h"
#include "core/string/ustring.h"

class RemoteFilesystemClient {
	String cache_path;
	HashSet<String> validated_directories;

protected:
	String _get_cache_path() { return cache_path; }
	struct FileCache {
		String path; // Local path (as in "folder/to/file.png")
		uint64_t server_modified_time = 0; // MD5 checksum.
		uint64_t modified_time = 0;
	};
	virtual bool _is_configured() { return !cache_path.is_empty(); }
	// Can be re-implemented per platform. If so, feel free to ignore get_cache_path()
	virtual Vector<FileCache> _load_cache_file();
	virtual Error _store_file(const String &p_path, const LocalVector<uint8_t> &p_file, uint64_t &r_modified_time);
	virtual Error _remove_file(const String &p_path);
	virtual Error _store_cache_file(const Vector<FileCache> &p_cache);
	virtual Error _synchronize_with_server(const String &p_host, int p_port, const String &p_password, String &r_cache_path);

	virtual void _update_cache_path(String &r_cache_path);

public:
	Error synchronize_with_server(const String &p_host, int p_port, const String &p_password, String &r_cache_path);
	virtual ~RemoteFilesystemClient() {}
};
