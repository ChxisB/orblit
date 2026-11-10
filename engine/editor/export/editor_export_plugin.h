/**************************************************************************/
/*  editor_export_plugin.h                                                */

#pragma once

#include "core/os/shared_object.h"
#include "core/variant/typed_array.h"
#include "editor/export/editor_export_platform.h"
#include "editor/export/editor_export_preset.h"
#include "scene/stage/entity.h"

class EditorExportPlugin : public RefCounted {
	ORBLIT_CLASS(EditorExportPlugin, RefCounted);

	friend class EditorExport;
	friend class EditorExportPlatform;
	friend class EditorExportPreset;

public:
	// Data for an SPM dependency, collected via add_apple_embedded_platform_spm_package() during export
	struct AppleEmbeddedSPMPackage {
		String url;
		String version;
		Vector<String> products;
	};

private:
	String export_base_path;
	Ref<EditorExportPreset> export_preset;

	Vector<SharedObject> shared_objects;
	struct ExtraFile {
		String path;
		Vector<uint8_t> data;
		bool remap = false;
	};
	Vector<ExtraFile> extra_files;
	bool skipped = false;

	Vector<String> apple_embedded_platform_frameworks;
	Vector<String> apple_embedded_platform_embedded_frameworks;
	Vector<String> apple_embedded_platform_project_static_libs;
	String apple_embedded_platform_plist_content;
	String apple_embedded_platform_linker_flags;
	Vector<String> apple_embedded_platform_bundle_files;
	String apple_embedded_platform_cpp_code;
	Vector<AppleEmbeddedSPMPackage> apple_embedded_platform_spm_packages;

	Vector<String> macos_plugin_files;

	_FORCE_INLINE_ void _clear() {
		shared_objects.clear();
		extra_files.clear();
		skipped = false;
	}

	_FORCE_INLINE_ void _export_end_clear() {
		apple_embedded_platform_frameworks.clear();
		apple_embedded_platform_embedded_frameworks.clear();
		apple_embedded_platform_bundle_files.clear();
		apple_embedded_platform_plist_content = "";
		apple_embedded_platform_linker_flags = "";
		apple_embedded_platform_cpp_code = "";
		apple_embedded_platform_spm_packages.clear();
		macos_plugin_files.clear();
	}

	// Export
	void _export_file_script(const String &p_path, const String &p_type, const Vector<String> &p_features);
	void _export_begin_script(const Vector<String> &p_features, bool p_debug, const String &p_path, int p_flags);
	void _export_end_script();

	String _has_valid_export_configuration(const Ref<EditorExportPlatform> &p_export_platform, const Ref<EditorExportPreset> &p_preset);

protected:
	void set_export_base_path(const String &p_export_base_path);
	const String &get_export_base_path() const;
	void set_export_preset(const Ref<EditorExportPreset> &p_preset);
	Ref<EditorExportPreset> get_export_preset() const;
	Ref<EditorExportPlatform> get_export_platform() const;

	void add_file(const String &p_path, const Vector<uint8_t> &p_file, bool p_remap);
	void add_shared_object(const String &p_path, const Vector<String> &tags, const String &p_target = String());
	void _add_shared_object(const SharedObject &p_shared_object);

	void add_apple_embedded_platform_framework(const String &p_path);
	void add_apple_embedded_platform_embedded_framework(const String &p_path);
	void add_apple_embedded_platform_project_static_lib(const String &p_path);
	void add_apple_embedded_platform_plist_content(const String &p_plist_content);
	void add_apple_embedded_platform_linker_flags(const String &p_flags);
	void add_apple_embedded_platform_bundle_file(const String &p_path);
	void add_apple_embedded_platform_cpp_code(const String &p_code);
	void add_apple_embedded_platform_spm_package(const String &p_url, const String &p_version, const PackedStringArray &p_products);
	void add_macos_plugin_file(const String &p_path);

	void skip();

	virtual void _export_file(const String &p_path, const String &p_type, const HashSet<String> &p_features);
	virtual void _export_begin(const HashSet<String> &p_features, bool p_debug, const String &p_path, int p_flags);
	virtual void _export_end();
	virtual void _end_generate_apple_embedded_project(const String &p_path, bool p_will_build_archive);

	static void register_members();

	ORBLIT_VIRTUAL3(_export_file, String, String, Vector<String>)
	ORBLIT_VIRTUAL4(_export_begin, Vector<String>, bool, String, uint32_t)
	ORBLIT_VIRTUAL0(_export_end)
	ORBLIT_VIRTUAL2(_end_generate_apple_embedded_project, const String &, bool)

	ORBLIT_VIRTUAL2RC(bool, _begin_customize_resources, const Ref<EditorExportPlatform> &, const Vector<String> &)
	ORBLIT_VIRTUAL2R_REQUIRED(Ref<Resource>, _customize_resource, const Ref<Resource> &, String)

	ORBLIT_VIRTUAL2RC(bool, _begin_customize_scenes, const Ref<EditorExportPlatform> &, const Vector<String> &)
	ORBLIT_VIRTUAL2R_REQUIRED(Entity *, _customize_scene, Entity *, String)
	ORBLIT_VIRTUAL0RC_REQUIRED(uint64_t, _get_customization_configuration_hash)

	ORBLIT_VIRTUAL0(_end_customize_scenes)
	ORBLIT_VIRTUAL0(_end_customize_resources)

	ORBLIT_VIRTUAL2RC(PackedStringArray, _get_export_features, const Ref<EditorExportPlatform> &, bool);
	ORBLIT_VIRTUAL1RC(TypedArray<Dictionary>, _get_export_options, const Ref<EditorExportPlatform> &);
	ORBLIT_VIRTUAL1RC(Dictionary, _get_export_options_overrides, const Ref<EditorExportPlatform> &);
	ORBLIT_VIRTUAL1RC(bool, _should_update_export_options, const Ref<EditorExportPlatform> &);
	ORBLIT_VIRTUAL2RC(bool, _get_export_option_visibility, const Ref<EditorExportPlatform> &, String);
	ORBLIT_VIRTUAL2RC(String, _get_export_option_warning, const Ref<EditorExportPlatform> &, String);

	ORBLIT_VIRTUAL0RC_REQUIRED(String, _get_name)

	ORBLIT_VIRTUAL1RC(bool, _supports_platform, const Ref<EditorExportPlatform> &);

	ORBLIT_VIRTUAL2RC(PackedStringArray, _get_android_dependencies, const Ref<EditorExportPlatform> &, bool);
	ORBLIT_VIRTUAL2RC(PackedStringArray, _get_android_dependencies_maven_repos, const Ref<EditorExportPlatform> &, bool);
	ORBLIT_VIRTUAL2RC(PackedStringArray, _get_android_libraries, const Ref<EditorExportPlatform> &, bool);
	ORBLIT_VIRTUAL2RC(String, _get_android_manifest_activity_element_contents, const Ref<EditorExportPlatform> &, bool);
	ORBLIT_VIRTUAL2RC(String, _get_android_manifest_application_element_contents, const Ref<EditorExportPlatform> &, bool);
	ORBLIT_VIRTUAL2RC(String, _get_android_manifest_element_contents, const Ref<EditorExportPlatform> &, bool);
	ORBLIT_VIRTUAL2RC(PackedByteArray, _update_android_prebuilt_manifest, const Ref<EditorExportPlatform> &, const PackedByteArray &);

	virtual bool _begin_customize_resources(const Ref<EditorExportPlatform> &p_platform, const Vector<String> &p_features); // Return true if this plugin does property export customization
	virtual Ref<Resource> _customize_resource(const Ref<Resource> &p_resource, const String &p_path); // If nothing is returned, it means do not touch (nothing changed). If something is returned (either the same or a different resource) it means changes are made.

	virtual bool _begin_customize_scenes(const Ref<EditorExportPlatform> &p_platform, const Vector<String> &p_features); // Return true if this plugin does property export customization
	virtual Entity *_customize_scene(Entity *p_root, const String &p_path); // Return true if a change was made

	virtual uint64_t _get_customization_configuration_hash() const; // Hash used for caching customized resources and scenes.

	virtual void _end_customize_scenes();
	virtual void _end_customize_resources();

	virtual PackedStringArray _get_export_features(const Ref<EditorExportPlatform> &p_export_platform, bool p_debug) const;
	virtual void _get_export_options(const Ref<EditorExportPlatform> &p_export_platform, List<EditorExportPlatform::ExportOption> *r_options) const;
	virtual Dictionary _get_export_options_overrides(const Ref<EditorExportPlatform> &p_export_platform) const;
	virtual bool _should_update_export_options(const Ref<EditorExportPlatform> &p_export_platform) const;
	virtual bool _get_export_option_visibility(const Ref<EditorExportPlatform> &p_export_platform, const String &p_option_name) const;
	virtual String _get_export_option_warning(const Ref<EditorExportPlatform> &p_export_platform, const String &p_option_name) const;

public:
	virtual String get_name() const;

	virtual bool supports_platform(const Ref<EditorExportPlatform> &p_export_platform) const;
	PackedStringArray get_export_features(const Ref<EditorExportPlatform> &p_export_platform, bool p_debug) const;

	void end_generate_apple_embedded_project(const String &p_path, bool p_will_build_archive);

	virtual PackedStringArray get_android_dependencies(const Ref<EditorExportPlatform> &p_export_platform, bool p_debug) const;
	virtual PackedStringArray get_android_dependencies_maven_repos(const Ref<EditorExportPlatform> &p_export_platform, bool p_debug) const;
	virtual PackedStringArray get_android_libraries(const Ref<EditorExportPlatform> &p_export_platform, bool p_debug) const;
	virtual String get_android_manifest_activity_element_contents(const Ref<EditorExportPlatform> &p_export_platform, bool p_debug) const;
	virtual String get_android_manifest_application_element_contents(const Ref<EditorExportPlatform> &p_export_platform, bool p_debug) const;
	virtual String get_android_manifest_element_contents(const Ref<EditorExportPlatform> &p_export_platform, bool p_debug) const;
	virtual PackedByteArray update_android_prebuilt_manifest(const Ref<EditorExportPlatform> &p_export_platform, const PackedByteArray &p_manifest_data) const;

	Vector<String> get_apple_embedded_platform_frameworks() const;
	Vector<String> get_apple_embedded_platform_embedded_frameworks() const;
	Vector<String> get_apple_embedded_platform_project_static_libs() const;
	String get_apple_embedded_platform_plist_content() const;
	String get_apple_embedded_platform_linker_flags() const;
	Vector<String> get_apple_embedded_platform_bundle_files() const;
	String get_apple_embedded_platform_cpp_code() const;
	Vector<AppleEmbeddedSPMPackage> get_apple_embedded_platform_spm_packages() const;
	const Vector<String> &get_macos_plugin_files() const;
	Variant get_option(const StringName &p_name) const;
};
