/**************************************************************************/
/*  test_utils.cpp                                                        */

#include "tests/test_utils.h"

#include "core/config/project_settings.h"
#include "core/io/dir_access.h"
#include "core/os/os.h"

String TestUtils::get_data_path(const String &p_file) {
	String data_path = "../tests/data";
	return get_executable_dir().path_join(data_path.path_join(p_file));
}

String TestUtils::get_executable_dir() {
	return OS::get_singleton()->get_executable_path().get_base_dir();
}

String TestUtils::get_temp_path(const String &p_suffix) {
	const String temp_base = OS::get_singleton()->get_cache_path().path_join("orblit_test");
	DirAccess::make_dir_absolute(temp_base); // Ensure the directory exists.
	return temp_base.path_join(p_suffix);
}

String &TestProjectSettingsInternalsAccessor::resource_path() {
	return ProjectSettings::get_singleton()->resource_path;
}
