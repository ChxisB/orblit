/**************************************************************************/
/*  test_old_files.cpp                                                    */
#include "tests/test_macros.h"

TEST_FORCE_LINK(test_old_files)

#include "core/config/project_settings.h"
#include "core/io/dir_access.h"
#include "core/io/file_access.h"
#include "core/io/file_access_pack.h"
#include "core/io/resource_loader.h"
#include "tests/test_utils.h"

namespace TestOldFiles {

static void write_text_file(const String &p_path, const String &p_text) {
	Ref<FileAccess> f = FileAccess::open(p_path, FileAccess::WRITE);
	REQUIRE(f.is_valid());
	f->store_string(p_text);
}

TEST_CASE("[OldFiles] A pack with the old engine magic is refused") {
	const String path = TestUtils::get_temp_path("old_engine.opack");
	{
		Ref<FileAccess> f = FileAccess::open(path, FileAccess::WRITE);
		REQUIRE(f.is_valid());
		f->store_32(0x43504447); // "GDPC" in ASCII.
		for (int i = 0; i < 32; i++) {
			f->store_32(0);
		}
	}

	ERR_PRINT_OFF;
	const Error err = PackedData::get_singleton()->add_pack(path, false, 0);
	ERR_PRINT_ON;
	CHECK_MESSAGE(err == ERR_FILE_UNRECOGNIZED, "A pack that starts with GDPC should not be opened.");
}

TEST_CASE("[OldFiles] An old engine text scene and resource are refused") {
	struct OldFile {
		String name;
		String text;
	};
	const OldFile old_files[] = {
		{ "old_scene.oscn", "[gd_scene load_steps=1 format=3]\n\n[node name=\"Root\" type=\"Entity\"]\n" },
		{ "old_resource.ores", "[gd_resource type=\"Resource\" format=3]\n\n[resource]\n" },
	};

	for (const OldFile &old_file : old_files) {
		const String path = TestUtils::get_temp_path(old_file.name);
		write_text_file(path, old_file.text);

		Error err = OK;
		ERR_PRINT_OFF;
		const Ref<Resource> loaded = ResourceLoader::load(path, "", ResourceFormatLoader::CACHE_MODE_IGNORE, &err);
		ERR_PRINT_ON;
		CHECK_MESSAGE(loaded.is_null(), vformat("%s should not load.", old_file.name));
		CHECK_MESSAGE(err != OK, vformat("%s should report an error.", old_file.name));
	}
}

TEST_CASE("[OldFiles] No loader reads an old engine file extension") {
	List<String> extensions;
	ResourceLoader::get_recognized_extensions_for_type("", &extensions);

	const char *old_extensions[] = { "tscn", "scn", "tres", "res", "gdshader", "gdshaderinc", "gdextension" };
	for (const char *old_extension : old_extensions) {
		CHECK_MESSAGE(extensions.find(String(old_extension)) == nullptr, vformat("The extension %s should have no loader.", old_extension));
	}

	const char *new_extensions[] = { "oscn", "oscnb", "ores", "oresb", "oshader", "oext" };
	for (const char *new_extension : new_extensions) {
		CHECK_MESSAGE(extensions.find(String(new_extension)) != nullptr, vformat("The extension %s should have a loader.", new_extension));
	}
}

TEST_CASE("[OldFiles] A folder with only an old project file has no project file") {
	const String folder = TestUtils::get_temp_path("old_project");
	REQUIRE(DirAccess::make_dir_recursive_absolute(folder) == OK);
	write_text_file(folder.path_join("project.cfg"), "config_version=5\n\n[application]\n\nconfig/name=\"Old\"\n");

	ERR_PRINT_OFF;
	const Error err = ProjectSettings::get_singleton()->load_custom(folder.path_join("project.orblit"));
	ERR_PRINT_ON;
	CHECK_MESSAGE(err == ERR_FILE_NOT_FOUND, "The engine reads project.orblit and ignores any other project file.");
}

} // namespace TestOldFiles
