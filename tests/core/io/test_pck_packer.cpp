/**************************************************************************/
/*  test_pck_packer.cpp                                                   */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_pck_packer)

#include "core/io/file_access.h"
#include "core/io/pck_packer.h"
#include "core/os/os.h"
#include "tests/test_utils.h"

namespace TestPCKPacker {

TEST_CASE("[PCKPacker] Pack an empty PCK file") {
	PCKPacker pck_packer;
	const String output_pck_path = TestUtils::get_temp_path("output_empty.opack");
	CHECK_MESSAGE(
			pck_packer.pck_start(output_pck_path) == OK,
			"Starting a PCK file should return an OK error code.");

	CHECK_MESSAGE(
			pck_packer.flush() == OK,
			"Flushing the PCK should return an OK error code.");

	Error err;
	Ref<FileAccess> f = FileAccess::open(output_pck_path, FileAccess::READ, &err);
	CHECK_MESSAGE(
			err == OK,
			"The generated empty PCK file should be opened successfully.");
	CHECK_MESSAGE(
			f->get_length() >= 100,
			"The generated empty PCK file shouldn't be too small (it should have the PCK header).");
	CHECK_MESSAGE(
			f->get_length() <= 500,
			"The generated empty PCK file shouldn't be too large.");
}

TEST_CASE("[PCKPacker] Pack empty with zero alignment invalid") {
	PCKPacker pck_packer;
	const String output_pck_path = TestUtils::get_temp_path("output_empty.opack");
	ERR_PRINT_OFF;
	CHECK_MESSAGE(pck_packer.pck_start(output_pck_path, 0) != OK, "PCK with zero alignment should fail.");
	ERR_PRINT_ON;
}

TEST_CASE("[PCKPacker] Pack empty with invalid key") {
	PCKPacker pck_packer;
	const String output_pck_path = TestUtils::get_temp_path("output_empty.opack");
	ERR_PRINT_OFF;
	CHECK_MESSAGE(pck_packer.pck_start(output_pck_path, 32, "") != OK, "PCK with invalid key should fail.");
	ERR_PRINT_ON;
}

TEST_CASE("[PCKPacker] Pack a PCK file with some files and directories") {
	PCKPacker pck_packer;
	const String output_pck_path = TestUtils::get_temp_path("output_with_files.opack");
	CHECK_MESSAGE(
			pck_packer.pck_start(output_pck_path) == OK,
			"Starting a PCK file should return an OK error code.");

	const String base_dir = OS::get_singleton()->get_executable_path().get_base_dir();

	CHECK_MESSAGE(
			pck_packer.add_file("version.py", base_dir.path_join("../version.py"), "version.py") == OK,
			"Adding a file to the PCK should return an OK error code.");
	CHECK_MESSAGE(
			pck_packer.add_file("some/directories with spaces/to/create/icon.png", base_dir.path_join("../dist/logo/icon.png")) == OK,
			"Adding a file to a new subdirectory in the PCK should return an OK error code.");
	CHECK_MESSAGE(
			pck_packer.add_file("some/directories with spaces/to/create/icon.svg", base_dir.path_join("../dist/logo/icon.svg")) == OK,
			"Adding a file to an existing subdirectory in the PCK should return an OK error code.");
	CHECK_MESSAGE(
			pck_packer.add_file("some/directories with spaces/to/create/icon.png", base_dir.path_join("../dist/logo/logo.png")) == OK,
			"Overriding a non-flushed file to an existing subdirectory in the PCK should return an OK error code.");
	CHECK_MESSAGE(
			pck_packer.add_file_from_buffer("buffer/new.txt", String("Hello world!").to_utf8_buffer()) == OK,
			"Adding a file from a buffer to the PCK in a new subdirectory should return an OK error code.");
	CHECK_MESSAGE(
			pck_packer.flush() == OK,
			"Flushing the PCK should return an OK error code.");

	Error err;
	Ref<FileAccess> f = FileAccess::open(output_pck_path, FileAccess::READ, &err);
	CHECK_MESSAGE(
			err == OK,
			"The generated non-empty PCK file should be opened successfully.");

	// The PCK keeps the first icon.png too, so every file added above counts. The slack covers the header and index.
	const char *added[] = { "../version.py", "../dist/logo/icon.png", "../dist/logo/icon.svg", "../dist/logo/logo.png" };
	int64_t stored = 12; // "Hello world!"
	for (const char *name : added) {
		stored += FileAccess::get_size(base_dir.path_join(name));
	}
	CHECK_MESSAGE(
			(int64_t)f->get_length() >= stored,
			"The generated non-empty PCK file should be large enough to actually hold the contents specified above.");
	CHECK_MESSAGE(
			(int64_t)f->get_length() <= stored + 2000,
			"The generated non-empty PCK file shouldn't be too large.");
}

} // namespace TestPCKPacker
