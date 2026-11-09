/**************************************************************************/
/*  shader_include_resource_format.cpp                                    */

#include "shader_include_resource_format.h"

#include "core/io/file_access.h"
#include "scene/resources/shader_include.h"

// ResourceFormatLoaderShaderInclude

Ref<Resource> ResourceFormatLoaderShaderInclude::load(const String &p_path, const String &p_original_path, Error *r_error, bool p_use_sub_threads, float *r_progress, CacheMode p_cache_mode) {
	if (r_error) {
		*r_error = ERR_FILE_CANT_OPEN;
	}

	Error error = OK;
	Vector<uint8_t> buffer = FileAccess::get_file_as_bytes(p_path, &error);
	ERR_FAIL_COND_V_MSG(error, nullptr, "Cannot load shader include: " + p_path);

	String str;
	if (buffer.size() > 0) {
		error = str.append_utf8((const char *)buffer.ptr(), buffer.size());
		ERR_FAIL_COND_V_MSG(error, nullptr, "Cannot parse shader include: " + p_path);
	}

	Ref<ShaderInclude> shader_inc;
	shader_inc.instantiate();

	shader_inc->set_include_path(p_path);
	shader_inc->set_code(str);

	if (r_error) {
		*r_error = OK;
	}

	return shader_inc;
}

void ResourceFormatLoaderShaderInclude::get_recognized_extensions(List<String> *p_extensions) const {
	p_extensions->push_back("oshaderinc");
}

bool ResourceFormatLoaderShaderInclude::handles_type(const String &p_type) const {
	return (p_type == "ShaderInclude");
}

String ResourceFormatLoaderShaderInclude::get_resource_type(const String &p_path) const {
	if (p_path.has_extension("oshaderinc")) {
		return "ShaderInclude";
	}
	return "";
}

// ResourceFormatSaverShaderInclude

Error ResourceFormatSaverShaderInclude::save(const Ref<Resource> &p_resource, const String &p_path, uint32_t p_flags) {
	Ref<ShaderInclude> shader_inc = p_resource;
	ERR_FAIL_COND_V(shader_inc.is_null(), ERR_INVALID_PARAMETER);

	String source = shader_inc->get_code();

	Error error;
	Ref<FileAccess> file = FileAccess::open(p_path, FileAccess::WRITE, &error);

	ERR_FAIL_COND_V_MSG(error, error, "Cannot save shader include '" + p_path + "'.");

	file->store_string(source);
	if (file->get_error() != OK && file->get_error() != ERR_FILE_EOF) {
		return ERR_CANT_CREATE;
	}

	return OK;
}

void ResourceFormatSaverShaderInclude::get_recognized_extensions(const Ref<Resource> &p_resource, List<String> *p_extensions) const {
	const ShaderInclude *shader_inc = Object::cast_to<ShaderInclude>(*p_resource);
	if (shader_inc != nullptr) {
		p_extensions->push_back("oshaderinc");
	}
}

bool ResourceFormatSaverShaderInclude::recognize(const Ref<Resource> &p_resource) const {
	return p_resource->get_class_name() == "ShaderInclude"; //only shader, not inherited
}
