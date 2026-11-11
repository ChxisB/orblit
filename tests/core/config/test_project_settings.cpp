/**************************************************************************/
/*  test_project_settings.cpp                                             */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_project_settings)

#include "core/config/project_settings.h"
#include "core/io/dir_access.h"
#include "core/object/message_queue.h"
#include "core/variant/variant.h"
#include "tests/signal_watcher.h"
#include "tests/test_utils.h"

namespace TestProjectSettings {

TEST_CASE("[ProjectSettings] Get existing setting") {
	CHECK(ProjectSettings::get_singleton()->has_setting("application/run/main_scene"));

	Variant variant = ProjectSettings::get_singleton()->get_setting("application/run/main_scene");
	CHECK_EQ(variant.get_type(), Variant::STRING);

	String name = variant;
	CHECK_EQ(name, String());
}

TEST_CASE("[ProjectSettings] Default value is ignored if setting exists") {
	CHECK(ProjectSettings::get_singleton()->has_setting("application/run/main_scene"));

	Variant variant = ProjectSettings::get_singleton()->get_setting("application/run/main_scene", "SomeDefaultValue");
	CHECK_EQ(variant.get_type(), Variant::STRING);

	String name = variant;
	CHECK_EQ(name, String());
}

TEST_CASE("[ProjectSettings] Non existing setting is null") {
	CHECK_FALSE(ProjectSettings::get_singleton()->has_setting("not_existing_setting"));

	Variant variant = ProjectSettings::get_singleton()->get_setting("not_existing_setting");
	CHECK_EQ(variant.get_type(), Variant::NIL);
}

TEST_CASE("[ProjectSettings] Non existing setting should return default value") {
	CHECK_FALSE(ProjectSettings::get_singleton()->has_setting("not_existing_setting"));

	Variant variant = ProjectSettings::get_singleton()->get_setting("not_existing_setting");
	CHECK_EQ(variant.get_type(), Variant::NIL);

	variant = ProjectSettings::get_singleton()->get_setting("not_existing_setting", "my_nice_default_value");
	CHECK_EQ(variant.get_type(), Variant::STRING);

	String name = variant;
	CHECK_EQ(name, "my_nice_default_value");

	CHECK_FALSE(ProjectSettings::get_singleton()->has_setting("not_existing_setting"));
}

TEST_CASE("[ProjectSettings] Set value should be returned when retrieved") {
	CHECK_FALSE(ProjectSettings::get_singleton()->has_setting("my_custom_setting"));

	Variant variant = ProjectSettings::get_singleton()->get_setting("my_custom_setting");
	CHECK_EQ(variant.get_type(), Variant::NIL);

	ProjectSettings::get_singleton()->set_setting("my_custom_setting", true);
	CHECK(ProjectSettings::get_singleton()->has_setting("my_custom_setting"));

	variant = ProjectSettings::get_singleton()->get_setting("my_custom_setting");
	CHECK_EQ(variant.get_type(), Variant::BOOL);

	bool value = variant;
	CHECK_EQ(true, value);

	CHECK(ProjectSettings::get_singleton()->has_setting("my_custom_setting"));
}

TEST_CASE("[ProjectSettings] localize_path") {
	String old_resource_path = TestProjectSettingsInternalsAccessor::resource_path();
	TestProjectSettingsInternalsAccessor::resource_path() = DirAccess::create(DirAccess::ACCESS_FILESYSTEM)->get_current_dir();
	String root_path = ProjectSettings::get_singleton()->get_resource_path();
#ifdef WINDOWS_ENABLED
	String root_path_win = ProjectSettings::get_singleton()->get_resource_path().replace_char('/', '\\');
#endif

	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("filename"), "res://filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("path/filename"), "res://path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("path/something/../filename"), "res://path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("path/./filename"), "res://path/filename");
#ifdef WINDOWS_ENABLED
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("path\\filename"), "res://path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("path\\something\\..\\filename"), "res://path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("path\\.\\filename"), "res://path/filename");
#endif

	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("../filename"), "../filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("../path/filename"), "../path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("..\\path\\filename"), "../path/filename");

	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("/testroot/filename"), "/testroot/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("/testroot/path/filename"), "/testroot/path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("/testroot/path/something/../filename"), "/testroot/path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("/testroot/path/./filename"), "/testroot/path/filename");
#ifdef WINDOWS_ENABLED
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("C:/testroot/filename"), "C:/testroot/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("C:/testroot/path/filename"), "C:/testroot/path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("C:/testroot/path/something/../filename"), "C:/testroot/path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("C:/testroot/path/./filename"), "C:/testroot/path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("C:\\testroot\\filename"), "C:/testroot/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("C:\\testroot\\path\\filename"), "C:/testroot/path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("C:\\testroot\\path\\something\\..\\filename"), "C:/testroot/path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path("C:\\testroot\\path\\.\\filename"), "C:/testroot/path/filename");
#endif

	CHECK_EQ(ProjectSettings::get_singleton()->localize_path(root_path + "/filename"), "res://filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path(root_path + "/path/filename"), "res://path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path(root_path + "/path/something/../filename"), "res://path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path(root_path + "/path/./filename"), "res://path/filename");
#ifdef WINDOWS_ENABLED
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path(root_path_win + "\\filename"), "res://filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path(root_path_win + "\\path\\filename"), "res://path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path(root_path_win + "\\path\\something\\..\\filename"), "res://path/filename");
	CHECK_EQ(ProjectSettings::get_singleton()->localize_path(root_path_win + "\\path\\.\\filename"), "res://path/filename");
#endif

	TestProjectSettingsInternalsAccessor::resource_path() = old_resource_path;
}

TEST_CASE("[Stage][ProjectSettings] settings_changed signal") {
	SIGNAL_WATCH(ProjectSettings::get_singleton(), SNAME("settings_changed"));

	ProjectSettings::get_singleton()->set_setting("test_signal_setting", "test_value");
	MessageQueue::get_singleton()->flush();

	SIGNAL_CHECK("settings_changed", { {} });

	SIGNAL_UNWATCH(ProjectSettings::get_singleton(), SNAME("settings_changed"));
}

TEST_CASE("[ProjectSettings] get_changed_settings basic functionality") {
	String setting_name = "test_changed_setting";
	ProjectSettings::get_singleton()->set_setting(setting_name, "test_value");

	PackedStringArray changes = ProjectSettings::get_singleton()->get_changed_settings();
	CHECK(changes.has(setting_name));
}

TEST_CASE("[ProjectSettings] get_changed_settings multiple settings") {
	ProjectSettings::get_singleton()->set_setting("test_setting_1", "value1");
	ProjectSettings::get_singleton()->set_setting("test_setting_2", "value2");
	ProjectSettings::get_singleton()->set_setting("another_group/setting", "value3");

	PackedStringArray changes = ProjectSettings::get_singleton()->get_changed_settings();
	CHECK(changes.has("test_setting_1"));
	CHECK(changes.has("test_setting_2"));
	CHECK(changes.has("another_group/setting"));
}

TEST_CASE("[ProjectSettings] check_changed_settings_in_group") {
	ProjectSettings::get_singleton()->set_setting("group1/setting1", "value1");
	ProjectSettings::get_singleton()->set_setting("group1/setting2", "value2");
	ProjectSettings::get_singleton()->set_setting("group2/setting1", "value3");
	ProjectSettings::get_singleton()->set_setting("other_setting", "value4");

	CHECK(ProjectSettings::get_singleton()->check_changed_settings_in_group("group1/"));
	CHECK(ProjectSettings::get_singleton()->check_changed_settings_in_group("group2/"));
	CHECK_FALSE(ProjectSettings::get_singleton()->check_changed_settings_in_group("nonexistent/"));

	CHECK(ProjectSettings::get_singleton()->check_changed_settings_in_group("group1"));
	CHECK(ProjectSettings::get_singleton()->check_changed_settings_in_group("other_setting"));
}

TEST_CASE("[Stage][ProjectSettings] Changes cleared after settings_changed signal") {
	SIGNAL_WATCH(ProjectSettings::get_singleton(), SNAME("settings_changed"));

	ProjectSettings::get_singleton()->set_setting("signal_clear_test", "value");

	PackedStringArray changes_before = ProjectSettings::get_singleton()->get_changed_settings();
	CHECK(changes_before.has("signal_clear_test"));

	MessageQueue::get_singleton()->flush();

	SIGNAL_CHECK("settings_changed", { {} });

	PackedStringArray changes_after = ProjectSettings::get_singleton()->get_changed_settings();
	CHECK_FALSE(changes_after.has("signal_clear_test"));

	SIGNAL_UNWATCH(ProjectSettings::get_singleton(), SNAME("settings_changed"));
}

TEST_CASE("[ProjectSettings] No tracking when setting same value") {
	String setting_name = "same_value_test";
	String test_value = "same_value";

	ProjectSettings::get_singleton()->set_setting(setting_name, test_value);
	int count_before = ProjectSettings::get_singleton()->get_changed_settings().size();

	// Setting the same value should not be tracked due to early return.
	ProjectSettings::get_singleton()->set_setting(setting_name, test_value);
	int count_after = ProjectSettings::get_singleton()->get_changed_settings().size();

	CHECK_EQ(count_before, count_after);
}

TEST_CASE("[ProjectSettings] The globals key registers a global and the autoload key does not") {
	ProjectSettings *settings = ProjectSettings::get_singleton();

	settings->set_setting("globals/TestGlobal", "*res://test_global.oscn");
	settings->set_setting("autoload/TestOldGlobal", "*res://test_old_global.oscn");
	CHECK(settings->has_autoload("TestGlobal"));
	CHECK_FALSE(settings->has_autoload("TestOldGlobal"));

	settings->set_setting("globals/TestGlobal", Variant());
	settings->set_setting("autoload/TestOldGlobal", Variant());
	CHECK_FALSE(settings->has_autoload("TestGlobal"));
}

TEST_CASE("[ProjectSettings] Merging class lists keeps known classes") {
	Dictionary base_class;
	base_class["class"] = "Base";
	base_class["path"] = "res://base.cs";
	Dictionary extra_class;
	extra_class["class"] = "Extra";
	extra_class["path"] = "res://extra.cs";
	Dictionary newer_base;
	newer_base["class"] = "Base";
	newer_base["path"] = "res://base_v2.cs";

	const Array known = { base_class };
	const Array added = { newer_base, extra_class };
	const Array merged = ProjectSettings::merge_global_class_lists(known, added);

	REQUIRE(merged.size() == 2);
	CHECK(Dictionary(merged[0])["path"] == "res://base_v2.cs");
	CHECK(Dictionary(merged[1])["class"] == "Extra");
	CHECK_MESSAGE(Dictionary(known[0])["path"] == "res://base.cs", "The known list is left as it was.");

	const Array only_added = ProjectSettings::merge_global_class_lists(Array(), added);
	CHECK(only_added.size() == 2);
}

} // namespace TestProjectSettings
