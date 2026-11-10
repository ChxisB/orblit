/**************************************************************************/
/*  dir_access_macos.h                                                    */

#pragma once

#if defined(UNIX_ENABLED)

#include "backends/unix/dir_access_unix.h"

#include <dirent.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <unistd.h>

class DirAccessMacOS : public DirAccessUnix {
	ORBLIT_SOFTCLASS(DirAccessMacOS, DirAccessUnix);

protected:
	virtual String fix_unicode_name(const char *p_name) const override;

	virtual int get_drive_count() override;
	virtual String get_drive(int p_drive) override;

	virtual bool is_hidden(const String &p_name) override;
	virtual bool is_case_sensitive(const String &p_path) const override;

	virtual String get_filesystem_type() const override;

	virtual bool is_bundle(const String &p_file) const override;
};

#endif // UNIX ENABLED
