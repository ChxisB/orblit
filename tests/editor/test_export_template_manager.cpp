/**************************************************************************/
/*  test_export_template_manager.cpp                                      */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_export_template_manager)

#ifdef TOOLS_ENABLED

#include "editor/export/export_template_manager.h"

namespace TestExportTemplateManager {

TEST_CASE("[Editor][ExportTemplateManager] Windows x86_64 needs its executables and the D3D12 libraries") {
	const PackedStringArray files = ExportTemplateManager::get_template_files("Windows Desktop", "x86_64");
	CHECK(files.size() == 6);
	CHECK(files.has("windows_debug_x86_64.exe"));
	CHECK(files.has("windows_release_x86_64.exe"));
	CHECK(files.has("windows_release_x86_64_console.exe"));
	CHECK(files.has("D3D12Core.x86_64.dll"));
	CHECK(files.has("d3d12SDKLayers.x86_64.dll"));
}

TEST_CASE("[Editor][ExportTemplateManager] Linux x86_64 needs its two executables") {
	CHECK(ExportTemplateManager::get_template_files("Linux", "x86_64") == PackedStringArray{ "linux_debug.x86_64", "linux_release.x86_64" });
}

TEST_CASE("[Editor][ExportTemplateManager] macOS needs one archive for every architecture") {
	CHECK(ExportTemplateManager::get_template_files("macOS", "universal") == PackedStringArray{ "macos.zip" });
	CHECK(ExportTemplateManager::get_template_files("macOS", "arm64") == PackedStringArray{ "macos.zip" });
}

TEST_CASE("[Editor][ExportTemplateManager] A template the release does not build gives no files") {
	CHECK(ExportTemplateManager::get_template_files("Web", "").is_empty());
	CHECK(ExportTemplateManager::get_template_files("Android", "").is_empty());
	CHECK(ExportTemplateManager::get_template_files("Linux", "arm64").is_empty());
	CHECK(ExportTemplateManager::get_template_files("Windows Desktop", "arm64").is_empty());
}

TEST_CASE("[Editor][PlatformTemplateDownload] The first mirror with a URL is the source") {
	const String list = R"({"mirrors": [{"name": "None"}, {"name": "Empty", "url": ""}, {"name": "Number", "url": 5}, {"name": "GitHub release", "url": "https://example.com/t.tpz"}]})";
	CHECK(PlatformTemplateDownload::find_first_mirror(list.to_utf8_buffer()) == "https://example.com/t.tpz");
}

TEST_CASE("[Editor][PlatformTemplateDownload] A list with no mirror gives no source") {
	CHECK(PlatformTemplateDownload::find_first_mirror(String("not json").to_utf8_buffer()).is_empty());
	CHECK(PlatformTemplateDownload::find_first_mirror(String(R"({"mirrors": []})").to_utf8_buffer()).is_empty());
	CHECK(PlatformTemplateDownload::find_first_mirror(String("[1, 2]").to_utf8_buffer()).is_empty());
}

} // namespace TestExportTemplateManager

#endif // TOOLS_ENABLED
