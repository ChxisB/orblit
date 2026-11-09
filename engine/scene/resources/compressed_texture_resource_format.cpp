/**************************************************************************/
/*  compressed_texture_resource_format.cpp                                */

#include "compressed_texture_resource_format.h"

#include "scene/resources/compressed_texture.h"

Ref<Resource> ResourceFormatLoaderCompressedTexture2D::load(const String &p_path, const String &p_original_path, Error *r_error, bool p_use_sub_threads, float *r_progress, CacheMode p_cache_mode) {
	Ref<CompressedTexture2D> st;
	st.instantiate();
	Error err = st->load(p_path);
	if (r_error) {
		*r_error = err;
	}
	if (err != OK) {
		return Ref<Resource>();
	}

	return st;
}

void ResourceFormatLoaderCompressedTexture2D::get_recognized_extensions(List<String> *p_extensions) const {
	p_extensions->push_back("ctex");
}

bool ResourceFormatLoaderCompressedTexture2D::handles_type(const String &p_type) const {
	return p_type == "CompressedTexture2D";
}

String ResourceFormatLoaderCompressedTexture2D::get_resource_type(const String &p_path) const {
	if (p_path.has_extension("ctex")) {
		return "CompressedTexture2D";
	}
	return "";
}

Ref<Resource> ResourceFormatLoaderCompressedTextureLayered::load(const String &p_path, const String &p_original_path, Error *r_error, bool p_use_sub_threads, float *r_progress, CacheMode p_cache_mode) {
	Ref<CompressedTextureLayered> ct;
	if (p_path.has_extension("ctexarray")) {
		Ref<CompressedTexture2DArray> c;
		c.instantiate();
		ct = c;
	} else if (p_path.has_extension("ccube")) {
		Ref<CompressedCubemap> c;
		c.instantiate();
		ct = c;
	} else if (p_path.has_extension("ccubearray")) {
		Ref<CompressedCubemapArray> c;
		c.instantiate();
		ct = c;
	} else {
		if (r_error) {
			*r_error = ERR_FILE_UNRECOGNIZED;
		}
		return Ref<Resource>();
	}
	Error err = ct->load(p_path);
	if (r_error) {
		*r_error = err;
	}
	if (err != OK) {
		return Ref<Resource>();
	}

	return ct;
}

void ResourceFormatLoaderCompressedTextureLayered::get_recognized_extensions(List<String> *p_extensions) const {
	p_extensions->push_back("ctexarray");
	p_extensions->push_back("ccube");
	p_extensions->push_back("ccubearray");
}

bool ResourceFormatLoaderCompressedTextureLayered::handles_type(const String &p_type) const {
	return p_type == "CompressedTexture2DArray" || p_type == "CompressedCubemap" || p_type == "CompressedCubemapArray";
}

String ResourceFormatLoaderCompressedTextureLayered::get_resource_type(const String &p_path) const {
	if (p_path.has_extension("ctexarray")) {
		return "CompressedTexture2DArray";
	}
	if (p_path.has_extension("ccube")) {
		return "CompressedCubemap";
	}
	if (p_path.has_extension("ccubearray")) {
		return "CompressedCubemapArray";
	}
	return "";
}

Ref<Resource> ResourceFormatLoaderCompressedTexture3D::load(const String &p_path, const String &p_original_path, Error *r_error, bool p_use_sub_threads, float *r_progress, CacheMode p_cache_mode) {
	Ref<CompressedTexture3D> st;
	st.instantiate();
	Error err = st->load(p_path);
	if (r_error) {
		*r_error = err;
	}
	if (err != OK) {
		return Ref<Resource>();
	}

	return st;
}

void ResourceFormatLoaderCompressedTexture3D::get_recognized_extensions(List<String> *p_extensions) const {
	p_extensions->push_back("ctex3d");
}

bool ResourceFormatLoaderCompressedTexture3D::handles_type(const String &p_type) const {
	return p_type == "CompressedTexture3D";
}

String ResourceFormatLoaderCompressedTexture3D::get_resource_type(const String &p_path) const {
	if (p_path.has_extension("ctex3d")) {
		return "CompressedTexture3D";
	}
	return "";
}
