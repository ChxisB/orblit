/**************************************************************************/
/*  dir_access_unix.h                                                     */

#pragma once

#if defined(UNIX_ENABLED)

#include "core/io/dir_access.h"

#include <dirent.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <unistd.h>

class DirAccessUnix : public DirAccess {
	ORBLIT_SOFTCLASS(DirAccessUnix, DirAccess);
	DIR *dir_stream = nullptr;

	bool _cisdir = false;
	bool _cishidden = false;

protected:
	String current_dir;
	virtual String fix_unicode_name(const char *p_name) const { return String::utf8(p_name); }
	virtual bool is_hidden(const String &p_name);
	virtual String fix_path(const String &p_path) const override;

public:
	typedef void (*RemoveNotificationFunc)(const String &p_file);
	static RemoveNotificationFunc remove_notification_func;

	virtual Error list_dir_begin() override; ///< This starts dir listing
	virtual String get_next() override;
	virtual bool current_is_dir() const override;
	virtual bool current_is_hidden() const override;

	virtual void list_dir_end() override; ///<

	virtual int get_drive_count() override;
	virtual String get_drive(int p_drive) override;
	virtual int get_current_drive() override;
	virtual bool drives_are_shortcuts() override;

	virtual Error change_dir(String p_dir) override; ///< can be relative or absolute, return false on success
	virtual String get_current_dir(bool p_include_drive = true) const override; ///< return current dir location
	virtual Error make_dir(String p_dir) override;

	virtual bool file_exists(String p_file) override;
	virtual bool dir_exists(String p_dir) override;
	virtual bool is_readable(String p_dir) override;
	virtual bool is_writable(String p_dir) override;

	virtual uint64_t get_modified_time(String p_file);

	virtual Error rename(String p_path, String p_new_path) override;
	virtual Error remove(String p_path) override;

	virtual bool is_link(String p_file) override;
	virtual String read_link(String p_file) override;
	virtual Error create_link(String p_source, String p_target) override;

	virtual bool is_case_sensitive(const String &p_path) const override;
	virtual bool is_equivalent(const String &p_path_a, const String &p_path_b) const override;

	virtual uint64_t get_space_left() override;

	virtual String get_filesystem_type() const override;

	DirAccessUnix();
	~DirAccessUnix();
};

#endif // UNIX_ENABLED
