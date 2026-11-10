/**************************************************************************/
/*  dir_access_macos.mm                                                   */

#import "dir_access_macos.h"

#if defined(UNIX_ENABLED)

#include "core/config/project_settings.h"

#import <AppKit/NSWorkspace.h>
#import <Foundation/Foundation.h>
#include <sys/mount.h>

#include <cerrno>

String DirAccessMacOS::get_filesystem_type() const {
	struct statfs fs;
	if (statfs(current_dir.utf8().get_data(), &fs) != 0) {
		return "";
	}
	return String::utf8(fs.f_fstypename).to_upper();
}

String DirAccessMacOS::fix_unicode_name(const char *p_name) const {
	String fname;
	if (p_name != nullptr) {
		NSString *nsstr = [[NSString stringWithUTF8String:p_name] precomposedStringWithCanonicalMapping];
		fname.append_utf8([nsstr UTF8String]);
	}

	return fname;
}

int DirAccessMacOS::get_drive_count() {
	NSArray *res_keys = [NSArray arrayWithObjects:NSURLVolumeURLKey, NSURLIsSystemImmutableKey, nil];
	NSArray *vols = [[NSFileManager defaultManager] mountedVolumeURLsIncludingResourceValuesForKeys:res_keys options:NSVolumeEnumerationSkipHiddenVolumes];

	return [vols count];
}

String DirAccessMacOS::get_drive(int p_drive) {
	NSArray *res_keys = [NSArray arrayWithObjects:NSURLVolumeURLKey, NSURLIsSystemImmutableKey, nil];
	NSArray *vols = [[NSFileManager defaultManager] mountedVolumeURLsIncludingResourceValuesForKeys:res_keys options:NSVolumeEnumerationSkipHiddenVolumes];
	int count = [vols count];

	ERR_FAIL_INDEX_V(p_drive, count, "");

	String volname;
	NSString *path = [vols[p_drive] path];

	volname.append_utf8([path UTF8String]);

	return volname;
}

bool DirAccessMacOS::is_hidden(const String &p_name) {
	String f = get_current_dir().path_join(p_name);
	NSURL *url = [NSURL fileURLWithPath:@(f.utf8().get_data())];
	NSNumber *hidden = nil;
	if (![url getResourceValue:&hidden forKey:NSURLIsHiddenKey error:nil]) {
		return DirAccessUnix::is_hidden(p_name);
	}
	return [hidden boolValue];
}

bool DirAccessMacOS::is_case_sensitive(const String &p_path) const {
	String f = p_path;
	if (!f.is_absolute_path()) {
		f = get_current_dir().path_join(f);
	}
	f = fix_path(f);

	NSURL *url = [NSURL fileURLWithPath:@(f.utf8().get_data())];
	NSNumber *cs = nil;
	if (![url getResourceValue:&cs forKey:NSURLVolumeSupportsCaseSensitiveNamesKey error:nil]) {
		return false;
	}
	return [cs boolValue];
}

bool DirAccessMacOS::is_bundle(const String &p_file) const {
	String f = p_file;
	if (!f.is_absolute_path()) {
		f = get_current_dir().path_join(f);
	}
	f = fix_path(f);

	return [[NSWorkspace sharedWorkspace] isFilePackageAtPath:[NSString stringWithUTF8String:f.utf8().get_data()]];
}

#endif // UNIX_ENABLED
