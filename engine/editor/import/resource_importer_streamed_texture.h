/**************************************************************************/
/*  resource_importer_streamed_texture.h                                  */

#pragma once

#include "core/io/resource_importer.h"
#include "systems/rendering/rendering_system_enums.h"

class StreamedTexture2D;

class ResourceImporterStreamedTexture : public ResourceImporter {
	ORBLIT_CLASS(ResourceImporterStreamedTexture, ResourceImporter);

	static ResourceImporterStreamedTexture *singleton;

	enum {
		MAKE_ROUGHNESS_FLAG = 1,
		MAKE_NORMAL_FLAG = 2,
	};

	Mutex mutex;
	struct MakeInfo {
		int flags = 0;
		String normal_path_for_roughness;
		RSE::TextureDetectRoughnessChannel channel_for_roughness = RSE::TEXTURE_DETECT_ROUGHNESS_R;
	};

	HashMap<StringName, MakeInfo> make_flags;

	static void _texture_reimport_roughness(const Ref<StreamedTexture2D> &p_tex, const String &p_normal_path, RSE::TextureDetectRoughnessChannel p_channel);
	static void _texture_reimport_normal(const Ref<StreamedTexture2D> &p_tex);

public:
	static ResourceImporterStreamedTexture *get_singleton() { return singleton; }

	virtual String get_importer_name() const override;
	virtual String get_visible_name() const override;
	virtual void get_recognized_extensions(List<String> *p_extensions) const override;
	virtual String get_save_extension() const override;
	virtual String get_resource_type() const override;
	virtual float get_priority() const override {
		return -1.0; // Set lower than resource_importer_texture
	}

	void update_imports();

	virtual void get_import_options(const String &p_path, List<ImportOption> *r_options, int p_preset = 0) const override;
	virtual bool get_option_visibility(const String &p_path, const String &p_option, const HashMap<StringName, Variant> &p_options) const override;
	virtual Error import(ResourceUID::ID p_source_id, const String &p_source_file, const String &p_save_path, const HashMap<StringName, Variant> &p_options, List<String> *r_platform_variants, List<String> *r_gen_files = nullptr, Variant *r_metadata = nullptr) override;

	ResourceImporterStreamedTexture(bool p_singleton = false);
	virtual ~ResourceImporterStreamedTexture() = default;
};
