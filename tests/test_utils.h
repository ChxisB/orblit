/**************************************************************************/
/*  test_utils.h                                                          */

#pragma once

class String;

namespace TestUtils {

String get_data_path(const String &p_file);
String get_executable_dir();
String get_temp_path(const String &p_suffix);
} // namespace TestUtils

// FIXME: This was originally constrained to `tests/core/config/test_project_settings.h`, but that
//  file is no longer a header. Some other tests relied on the ability to override the resource
//  path, so relocating the accessor is the least-intrusive workaround.
class TestProjectSettingsInternalsAccessor {
public:
	static String &resource_path();
};
