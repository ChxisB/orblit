/**************************************************************************/
/*  resource_importer_bmfont.cpp                                          */

#include "resource_importer_bmfont.h"

#include "core/io/config_file.h"
#include "core/io/resource_saver.h"
#include "scene/resources/font.h"
#include "systems/text/text_system.h"

String ResourceImporterBMFont::get_importer_name() const {
	return "font_data_bmfont";
}

String ResourceImporterBMFont::get_visible_name() const {
	return "Font Data (AngelCode BMFont)";
}

void ResourceImporterBMFont::get_recognized_extensions(List<String> *p_extensions) const {
	if (p_extensions) {
		p_extensions->push_back("font");
		p_extensions->push_back("fnt");
	}
}

String ResourceImporterBMFont::get_save_extension() const {
	return "fontdata";
}

String ResourceImporterBMFont::get_resource_type() const {
	return "FontFile";
}

bool ResourceImporterBMFont::get_option_visibility(const String &p_path, const String &p_option, const HashMap<StringName, Variant> &p_options) const {
	return true;
}

void ResourceImporterBMFont::get_import_options(const String &p_path, List<ImportOption> *r_options, int p_preset) const {
	r_options->push_back(ImportOption(PropertyInfo(Variant::ARRAY, "fallbacks", PROPERTY_HINT_ARRAY_TYPE, MAKE_RESOURCE_TYPE_HINT("Font")), Array()));

	r_options->push_back(ImportOption(PropertyInfo(Variant::BOOL, "compress"), true));
	r_options->push_back(ImportOption(PropertyInfo(Variant::INT, "scaling_mode", PROPERTY_HINT_ENUM, "Disabled,Enabled (Integer),Enabled (Fractional)"), TextSystem::FIXED_SIZE_SCALE_ENABLED));
}

Error ResourceImporterBMFont::import(ResourceUID::ID p_source_id, const String &p_source_file, const String &p_save_path, const HashMap<StringName, Variant> &p_options, List<String> *r_platform_variants, List<String> *r_gen_files, Variant *r_metadata) {
	print_verbose("Importing BMFont font from: " + p_source_file);

	Array fallbacks = p_options["fallbacks"];
	TextSystem::FixedSizeScaleMode smode = (TextSystem::FixedSizeScaleMode)p_options["scaling_mode"].operator int();

	Ref<FontFile> font;
	font.instantiate();

	List<String> image_files;
	Error err = font->_load_bitmap_font(p_source_file, &image_files);
	ERR_FAIL_COND_V_MSG(err != OK, err, "Cannot load font to file \"" + p_source_file + "\".");

	// Update import settings for the image files used by font.
	for (const String &file : image_files) {
		Ref<ConfigFile> config;
		config.instantiate();

		err = config->load(file + ".import");
		if (err == OK) {
			config->clear();
			config->set_value("remap", "importer", "skip");

			config->save(file + ".import");
		}
	}

	font->set_allow_system_fallback(false);
	font->set_fallbacks(fallbacks);
	font->set_fixed_size_scale_mode(smode);

	int flg = 0;
	if ((bool)p_options["compress"]) {
		flg |= ResourceSaver::SaverFlags::FLAG_COMPRESS;
	}

	print_verbose("Saving to: " + p_save_path + ".fontdata");
	err = ResourceSaver::save(font, p_save_path + ".fontdata", flg);
	ERR_FAIL_COND_V_MSG(err != OK, err, "Cannot save font to file \"" + p_save_path + ".oresb\".");
	print_verbose("Done saving to: " + p_save_path + ".fontdata");
	return OK;
}
