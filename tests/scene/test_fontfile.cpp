/**************************************************************************/
/*  test_fontfile.cpp                                                     */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_fontfile)

#include "core/io/dir_access.h"
#include "core/io/file_access.h"
#include "scene/resources/font.h"
#include "tests/test_utils.h"

#include "modules/modules_enabled.gen.h" // For freetype.

namespace TestFontFile {

TEST_CASE("[FontFile] Load Dynamic Font - getters") {
#ifdef MODULE_FREETYPE_ENABLED
	String test_dynamic_font = "thirdparty/fonts/NotoSansHebrew_Regular.woff2";
	Ref<FontFile> ff;
	ff.instantiate();
	CHECK(ff->load_dynamic_font(test_dynamic_font) == OK);

	// These properties come from the font file itself.
	CHECK(ff->get_font_name() == "Noto Sans Hebrew");
	CHECK(ff->get_font_style_name() == "Regular");
	CHECK(ff->get_font_weight() == 400);
	CHECK(ff->get_font_stretch() == 100);
	CHECK(ff->get_opentype_features() == Dictionary());

	Dictionary expected_ot_name_strings;
	Dictionary en_dict;
	en_dict["copyright"] = "Copyright 2024 The Noto Project Authors (https://github.com/notofonts/hebrew)";
	en_dict["family_name"] = "Noto Sans Hebrew";
	en_dict["subfamily_name"] = "Regular";
	en_dict["unique_identifier"] = "3.001;GOOG;NotoSansHebrew-Regular";
	en_dict["full_name"] = "Noto Sans Hebrew Regular";
	en_dict["version"] = "Version 3.001";
	en_dict["postscript_name"] = "NotoSansHebrew-Regular";
	en_dict["trademark"] = "Noto is a trademark of Google Inc.";
	en_dict["manufacturer"] = "Google LLC";
	en_dict["designer"] = "Ben Nathan";
	en_dict["description"] = "Designed by Ben Nathan";
	en_dict["vendor_url"] = "http://www.google.com/get/noto/";
	en_dict["designer_url"] = "https://hafontia.com/";
	en_dict["license"] = "This Font Software is licensed under the SIL Open Font License, Version 1.1. This Font Software is distributed on an \"AS IS\" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the SIL Open Font License for the specific language, permissions and limitations governing your use of this Font Software.";
	en_dict["license_url"] = "http://scripts.sil.org/OFL";
	expected_ot_name_strings["en"] = en_dict;
	CHECK(ff->get_ot_name_strings() == expected_ot_name_strings);

	// These are dependent on size and potentially other state. Act as regression tests based of arbitrary small size 10 and large size 100.
	CHECK(ff->get_height(10) == doctest::Approx((real_t)14));
	CHECK(ff->get_ascent(10) == doctest::Approx((real_t)11));
	CHECK(ff->get_descent(10) == doctest::Approx((real_t)3));
	CHECK(ff->get_underline_position(10) == doctest::Approx((real_t)1.25));
	CHECK(ff->get_underline_thickness(10) == doctest::Approx((real_t)0.5));
	CHECK(ff->get_height(100) == doctest::Approx((real_t)137));
	CHECK(ff->get_ascent(100) == doctest::Approx((real_t)107));
	CHECK(ff->get_descent(100) == doctest::Approx((real_t)30));
	CHECK(ff->get_underline_position(100) == doctest::Approx((real_t)12.5));
	CHECK(ff->get_underline_thickness(100) == doctest::Approx((real_t)5));
#endif
}

TEST_CASE("[FontFile] A bitmap font block that runs past the end of the file is rejected") {
	const String path = TestUtils::get_temp_path("short_block.fnt");
	{
		Ref<FileAccess> f = FileAccess::open(path, FileAccess::WRITE);
		REQUIRE(f.is_valid());
		f->store_buffer((const uint8_t *)"BMF\x03", 4);
		f->store_8(1); // Info block.
		f->store_32(100); // Claims 100 bytes, 15 follow.
		for (int i = 0; i < 15; i++) {
			f->store_8(0);
		}
	}

	Ref<FontFile> ff;
	ff.instantiate();
	ERR_PRINT_OFF;
	const Error err = ff->load_bitmap_font(path);
	ERR_PRINT_ON;
	CHECK(err == ERR_FILE_CORRUPT);

	DirAccess::remove_absolute(path);
}

TEST_CASE("[FontFile] A bitmap font ignores characters past the Unicode range") {
	const String path = TestUtils::get_temp_path("wide_chars.fnt");
	{
		Ref<FileAccess> f = FileAccess::open(path, FileAccess::WRITE);
		REQUIRE(f.is_valid());
		f->store_buffer((const uint8_t *)"BMF\x03", 4);
		f->store_8(4); // Chars block, two entries of 20 bytes.
		f->store_32(40);
		for (const uint32_t id : { 0x41u, 0x110000u }) {
			f->store_32(id);
			for (int i = 0; i < 16; i++) {
				f->store_8(0);
			}
		}
		f->store_8(5); // Kerning block, two entries of 10 bytes.
		f->store_32(20);
		for (const uint32_t second : { 0x41u, 0x110000u }) {
			f->store_32(0x41);
			f->store_32(second);
			f->store_16(1);
		}
	}

	Ref<FontFile> ff;
	ff.instantiate();
	CHECK(ff->load_bitmap_font(path) == OK);
	CHECK(ff->get_glyph_list(0, Vector2i(16, 0)).size() == 1);
	CHECK(ff->get_kerning_list(0, 16).size() == 1);

	DirAccess::remove_absolute(path);
}

TEST_CASE("[FontFile] A text bitmap font ignores characters past the Unicode range") {
	const String path = TestUtils::get_temp_path("wide_chars_text.fnt");
	{
		Ref<FileAccess> f = FileAccess::open(path, FileAccess::WRITE);
		REQUIRE(f.is_valid());
		f->store_line("char id=65 x=0 y=0 width=1 height=1 xoffset=0 yoffset=0 xadvance=1 page=0 chnl=15");
		f->store_line("char id=1114112 x=0 y=0 width=1 height=1 xoffset=0 yoffset=0 xadvance=1 page=0 chnl=15");
		f->store_line("kerning first=65 second=65 amount=1");
		f->store_line("kerning first=65 second=1114112 amount=1");
	}

	Ref<FontFile> ff;
	ff.instantiate();
	CHECK(ff->load_bitmap_font(path) == OK);
	CHECK(ff->get_glyph_list(0, Vector2i(16, 0)).size() == 1);
	CHECK(ff->get_kerning_list(0, 16).size() == 1);

	DirAccess::remove_absolute(path);
}

TEST_CASE("[FontFile] Create font file and check data") {
	// Create test instance.
	Ref<FontFile> font_file;
	font_file.instantiate();

#ifdef MODULE_FREETYPE_ENABLED
	// Try to load non-existent files.
	ERR_PRINT_OFF
	CHECK(font_file->load_dynamic_font("") == OK);
	CHECK_MESSAGE(font_file->get_data().is_empty() == true, "Invalid fontfile should not be loaded.");

	CHECK(font_file->load_dynamic_font("thirdparty/fonts/nofonthasthisname.woff2") == OK);
	CHECK_MESSAGE(font_file->get_data().is_empty() == true, "Invalid fontfile should not be loaded.");
	ERR_PRINT_ON

	// Load a valid file.
	CHECK(font_file->load_dynamic_font("thirdparty/fonts/Inter_Regular.woff2") == OK);

	// Check fontfile data.
	CHECK_MESSAGE(font_file->get_data().is_empty() == false, "Fontfile should have been loaded.");
	CHECK_MESSAGE(font_file->get_font_name() == "Inter", "Loaded correct font name.");
	CHECK_MESSAGE(font_file->get_font_style_name() == "Regular", "Loaded correct font style.");
	CHECK_MESSAGE(font_file->get_data().size() == 111268llu, "Whole fontfile was loaded.");

	// Valid glyphs.
	CHECK_MESSAGE(font_file->get_glyph_index(2, 'a', 0) != 0, "Glyph index for 'a' is valid.");
	CHECK_MESSAGE(font_file->get_glyph_index(2, 'b', 0) != 0, "Glyph index for 'b' is valid.");
	CHECK_MESSAGE(font_file->get_glyph_index(2, 0x0103, 0) != 0, "Glyph index for 'latin small letter a with breve' is valid.");
	CHECK_MESSAGE(font_file->get_glyph_index(2, 0x03a8, 0) != 0, "Glyph index for 'Greek psi' is valid.");
	CHECK_MESSAGE(font_file->get_glyph_index(2, 0x0416, 0) != 0, "Glyph index for 'Cyrillic zhe' is valid.");
	CHECK_MESSAGE(font_file->get_glyph_index(2, '&', 0) != 0, "Glyph index for '&' is valid.");

	// Invalid glyphs.
	CHECK_MESSAGE(font_file->get_glyph_index(2, 0x4416, 0) == 0, "Glyph index is invalid.");
	CHECK_MESSAGE(font_file->get_glyph_index(2, 0x5555, 0) == 0, "Glyph index is invalid.");
	CHECK_MESSAGE(font_file->get_glyph_index(2, 0x2901, 0) == 0, "Glyph index is invalid.");
#endif
}

} // namespace TestFontFile
