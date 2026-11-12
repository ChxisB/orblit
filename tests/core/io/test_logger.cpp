/**************************************************************************/
/*  test_logger.cpp                                                       */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_logger)

#include "core/config/project_settings.h"
#include "core/io/dir_access.h"
#include "core/io/file_access.h"
#include "core/io/logger.h"
#include "core/os/os.h"

namespace TestLogger {

void initialize_logs() {
	ProjectSettings::get_singleton()->set_setting("application/config/name", "orblit_tests");
	DirAccess::make_dir_recursive_absolute(OS::get_singleton()->get_user_data_dir().path_join("logs"));
}

void cleanup_logs() {
	ProjectSettings::get_singleton()->set_setting("application/config/name", "orblit_tests");
	Ref<DirAccess> dir = DirAccess::open("user://logs");
	dir->list_dir_begin();
	String file = dir->get_next();
	while (file != "") {
		if (file.match("*.log")) {
			dir->remove(file);
		}
		file = dir->get_next();
	}
	DirAccess::remove_absolute(OS::get_singleton()->get_user_data_dir().path_join("logs"));
	DirAccess::remove_absolute(OS::get_singleton()->get_user_data_dir());
}

TEST_CASE("[Logger][RotatedFileLogger] Creates the first log file and logs on it") {
	initialize_logs();

	String waiting_for_orblit = "Waiting for Orblit";
	RotatedFileLogger logger("user://logs/orblit.log");
	logger.logf("%s", "Waiting for Orblit");

	Error err = Error::OK;
	Ref<FileAccess> log = FileAccess::open("user://logs/orblit.log", FileAccess::READ, &err);
	CHECK_EQ(err, Error::OK);
	CHECK_EQ(log->get_as_text(), waiting_for_orblit);

	cleanup_logs();
}

TEST_CASE("[Logger][RotatedFileLogger] Creates the folders of a relative log path") {
	initialize_logs();

	// The path is relative on purpose. The folders must appear where the file opens, not in the user data folder.
	const String file_path = "orblit_test_logs/nested/orblit.log";
	const String folders[] = { "orblit_test_logs/nested", "orblit_test_logs" };
	DirAccess::remove_absolute(file_path);
	for (const String &folder : folders) {
		DirAccess::remove_absolute(folder);
	}

	{
		RotatedFileLogger logger(file_path);
		logger.logf("%s", "Waiting for Orblit");
	}

	Error err = Error::OK;
	Ref<FileAccess> log = FileAccess::open(file_path, FileAccess::READ, &err);
	CHECK_EQ(err, Error::OK);
	if (log.is_valid()) {
		CHECK_EQ(log->get_as_text(), "Waiting for Orblit");
		log.unref();
	}

	DirAccess::remove_absolute(file_path);
	for (const String &folder : folders) {
		DirAccess::remove_absolute(folder);
	}
	cleanup_logs();
}

void get_log_files(Vector<String> &log_files) {
	Ref<DirAccess> dir = DirAccess::open("user://logs");
	dir->list_dir_begin();
	String file = dir->get_next();
	while (file != "") {
		// Filtering orblit.log because ordered_insert will put it first and should be the last.
		if (file.match("*.log") && file != "orblit.log") {
			log_files.ordered_insert(file);
		}
		file = dir->get_next();
	}
	if (FileAccess::exists("user://logs/orblit.log")) {
		log_files.push_back("orblit.log");
	}
}

// The rotations run back to back, so their backups share a timestamp and need distinct names.
TEST_CASE("[Logger][RotatedFileLogger] Rotates logs files") {
	initialize_logs();

	// A log left by an earlier test, as on Windows where an open log cannot be deleted. Its backup is
	// removed before the last rotation, which frees the oldest name of the second.
	{
		Ref<FileAccess> stale_log = FileAccess::open("user://logs/orblit.log", FileAccess::WRITE);
		stale_log->store_string("Stale log");
	}

	Vector<String> all_waiting_for_orblit;

	const int number_of_files = 3;
	for (int i = 0; i < number_of_files; i++) {
		String waiting_for_orblit = "Waiting for Orblit " + itos(i);
		RotatedFileLogger logger("user://logs/orblit.log", number_of_files);
		logger.logf("%s", waiting_for_orblit.ascii().get_data());
		all_waiting_for_orblit.push_back(waiting_for_orblit);
	}

	Vector<String> log_files;
	get_log_files(log_files);
	CHECK_MESSAGE(log_files.size() == number_of_files, "Did not rotate all files");

	for (int i = 0; i < log_files.size(); i++) {
		Error err = Error::OK;
		Ref<FileAccess> log_file = FileAccess::open("user://logs/" + log_files[i], FileAccess::READ, &err);
		REQUIRE_EQ(err, Error::OK);
		CHECK_EQ(log_file->get_as_text(), all_waiting_for_orblit[i]);
	}

	// This time the oldest log must be removed and orblit.log updated.
	String new_waiting_for_orblit = "Waiting for Orblit " + itos(number_of_files);
	all_waiting_for_orblit = all_waiting_for_orblit.slice(1, all_waiting_for_orblit.size());
	all_waiting_for_orblit.push_back(new_waiting_for_orblit);
	RotatedFileLogger logger("user://logs/orblit.log", number_of_files);
	logger.logf("%s", new_waiting_for_orblit.ascii().get_data());

	log_files.clear();
	get_log_files(log_files);
	CHECK_MESSAGE(log_files.size() == number_of_files, "Did not remove old log file");

	for (int i = 0; i < log_files.size(); i++) {
		Error err = Error::OK;
		Ref<FileAccess> log_file = FileAccess::open("user://logs/" + log_files[i], FileAccess::READ, &err);
		REQUIRE_EQ(err, Error::OK);
		CHECK_EQ(log_file->get_as_text(), all_waiting_for_orblit[i]);
	}

	cleanup_logs();
}

TEST_CASE("[Logger][CompositeLogger] Logs the same into multiple loggers") {
	initialize_logs();

	Vector<Logger *> all_loggers;
	all_loggers.push_back(memnew(RotatedFileLogger("user://logs/orblit_logger_1.log", 1)));
	all_loggers.push_back(memnew(RotatedFileLogger("user://logs/orblit_logger_2.log", 1)));

	String waiting_for_orblit = "Waiting for Orblit";
	CompositeLogger logger(all_loggers);
	logger.logf("%s", "Waiting for Orblit");

	Error err = Error::OK;
	Ref<FileAccess> log = FileAccess::open("user://logs/orblit_logger_1.log", FileAccess::READ, &err);
	CHECK_EQ(err, Error::OK);
	CHECK_EQ(log->get_as_text(), waiting_for_orblit);
	log = FileAccess::open("user://logs/orblit_logger_2.log", FileAccess::READ, &err);
	CHECK_EQ(err, Error::OK);
	CHECK_EQ(log->get_as_text(), waiting_for_orblit);

	cleanup_logs();
}

} // namespace TestLogger
