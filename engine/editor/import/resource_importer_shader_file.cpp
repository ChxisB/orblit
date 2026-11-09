/**************************************************************************/
/*  resource_importer_shader_file.cpp                                     */

#include "resource_importer_shader_file.h"

#include "core/io/file_access.h"
#include "core/io/resource_saver.h"
#include "core/object/callable_mp.h"
#include "editor/editor_node.h"
#include "editor/shader/shader_file_editor_plugin.h"
#include "systems/rendering/rendering_device_binds.h"

String ResourceImporterShaderFile::get_importer_name() const {
	return "glsl";
}

String ResourceImporterShaderFile::get_visible_name() const {
	return "GLSL Shader File";
}

void ResourceImporterShaderFile::get_recognized_extensions(List<String> *p_extensions) const {
	p_extensions->push_back("glsl");
}

String ResourceImporterShaderFile::get_save_extension() const {
	return "oresb";
}

String ResourceImporterShaderFile::get_resource_type() const {
	return "RDShaderFile";
}

int ResourceImporterShaderFile::get_preset_count() const {
	return 0;
}

String ResourceImporterShaderFile::get_preset_name(int p_idx) const {
	return String();
}

void ResourceImporterShaderFile::get_import_options(const String &p_path, List<ImportOption> *r_options, int p_preset) const {
}

bool ResourceImporterShaderFile::get_option_visibility(const String &p_path, const String &p_option, const HashMap<StringName, Variant> &p_options) const {
	return true;
}

#ifdef RD_ENABLED
static String _include_function(const String &p_path, void *userpointer) {
	Error err;

	String *base_path = (String *)userpointer;

	String include = p_path;
	if (include.is_relative_path()) {
		include = base_path->path_join(include);
	}

	Ref<FileAccess> file_inc = FileAccess::open(include, FileAccess::READ, &err);
	if (err != OK) {
		return String();
	}
	return file_inc->get_as_utf8_string();
}
#endif // RD_ENABLED

Error ResourceImporterShaderFile::import(ResourceUID::ID p_source_id, const String &p_source_file, const String &p_save_path, const HashMap<StringName, Variant> &p_options, List<String> *r_platform_variants, List<String> *r_gen_files, Variant *r_metadata) {
#ifdef RD_ENABLED
	Error err;
	Ref<FileAccess> file = FileAccess::open(p_source_file, FileAccess::READ, &err);
	ERR_FAIL_COND_V(err != OK, ERR_CANT_OPEN);
	ERR_FAIL_COND_V(file.is_null(), ERR_CANT_OPEN);

	String file_txt = file->get_as_utf8_string();
	Ref<RDShaderFile> shader_file;
	shader_file.instantiate();
	String base_path = p_source_file.get_base_dir();
	err = shader_file->parse_versions_from_text(file_txt, "", _include_function, &base_path);

	if (err != OK) {
		if (!ShaderFileEditor::singleton->is_visible_in_stage()) {
			callable_mp_static(&EditorNode::add_io_error).call_deferred(vformat(TTR("Error importing GLSL shader file: '%s'. Open the file in the filesystem dock in order to see the reason."), p_source_file));
		}
	}

	ResourceSaver::save(shader_file, p_save_path + ".oresb");

	return OK;
#else
	ERR_FAIL_V_MSG(ERR_CANT_OPEN, "RenderingDevice not available.");
#endif // RD_ENABLED
}
