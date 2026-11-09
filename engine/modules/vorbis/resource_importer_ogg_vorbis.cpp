/**************************************************************************/
/*  resource_importer_ogg_vorbis.cpp                                      */

#include "resource_importer_ogg_vorbis.h"

#include "core/io/resource_saver.h"
#include "core/object/class_registry.h"

#ifdef TOOLS_ENABLED
#include "editor/import/audio_stream_import_settings.h"
#endif

#include <ogg/ogg.h>
#include <vorbis/codec.h>

String ResourceImporterOggVorbis::get_importer_name() const {
	return "oggvorbisstr";
}

String ResourceImporterOggVorbis::get_visible_name() const {
	return "Ogg Vorbis";
}

void ResourceImporterOggVorbis::get_recognized_extensions(List<String> *p_extensions) const {
	p_extensions->push_back("ogg");
}

String ResourceImporterOggVorbis::get_save_extension() const {
	return "oggvorbisstr";
}

String ResourceImporterOggVorbis::get_resource_type() const {
	return "AudioStreamOggVorbis";
}

bool ResourceImporterOggVorbis::get_option_visibility(const String &p_path, const String &p_option, const HashMap<StringName, Variant> &p_options) const {
	return true;
}

int ResourceImporterOggVorbis::get_preset_count() const {
	return 0;
}

String ResourceImporterOggVorbis::get_preset_name(int p_idx) const {
	return String();
}

void ResourceImporterOggVorbis::get_import_options(const String &p_path, List<ImportOption> *r_options, int p_preset) const {
	r_options->push_back(ImportOption(PropertyInfo(Variant::BOOL, "loop"), false));
	r_options->push_back(ImportOption(PropertyInfo(Variant::FLOAT, "loop_offset"), 0));
	r_options->push_back(ImportOption(PropertyInfo(Variant::FLOAT, "bpm", PROPERTY_HINT_RANGE, "0,400,0.01,or_greater"), 0));
	r_options->push_back(ImportOption(PropertyInfo(Variant::INT, "beat_count", PROPERTY_HINT_RANGE, "0,512,or_greater"), 0));
	r_options->push_back(ImportOption(PropertyInfo(Variant::INT, "bar_beats", PROPERTY_HINT_RANGE, "2,32,or_greater"), 4));
}

#ifdef TOOLS_ENABLED
bool ResourceImporterOggVorbis::has_advanced_options() const {
	return true;
}

void ResourceImporterOggVorbis::show_advanced_options(const String &p_path) {
	Ref<AudioStreamOggVorbis> ogg_stream = AudioStreamOggVorbis::load_from_file(p_path);
	if (ogg_stream.is_valid()) {
		AudioStreamImportSettingsDialog::get_singleton()->edit(p_path, "oggvorbisstr", ogg_stream);
	}
}
#endif

Error ResourceImporterOggVorbis::import(ResourceUID::ID p_source_id, const String &p_source_file, const String &p_save_path, const HashMap<StringName, Variant> &p_options, List<String> *r_platform_variants, List<String> *r_gen_files, Variant *r_metadata) {
	bool loop = p_options["loop"];
	double loop_offset = p_options["loop_offset"];
	double bpm = p_options["bpm"];
	int beat_count = p_options["beat_count"];
	int bar_beats = p_options["bar_beats"];

	Ref<AudioStreamOggVorbis> ogg_vorbis_stream = AudioStreamOggVorbis::load_from_file(p_source_file);
	if (ogg_vorbis_stream.is_null()) {
		return ERR_CANT_OPEN;
	}

	ogg_vorbis_stream->set_loop(loop);
	ogg_vorbis_stream->set_loop_offset(loop_offset);
	ogg_vorbis_stream->set_bpm(bpm);
	ogg_vorbis_stream->set_beat_count(beat_count);
	ogg_vorbis_stream->set_bar_beats(bar_beats);

	return ResourceSaver::save(ogg_vorbis_stream, p_save_path + ".oggvorbisstr");
}

void ResourceImporterOggVorbis::register_members() {
}

ResourceImporterOggVorbis::ResourceImporterOggVorbis() {
}
