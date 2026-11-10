/**************************************************************************/
/*  editor_scene_importer_blend.h                                         */

#pragma once

#include "editor/file_system/editor_file_system.h"
#include "editor/import/spatial/resource_importer_scene.h"

class Animation;
class Entity;
class ConfirmationDialog;

class EditorSceneFormatImporterBlend : public EditorSceneFormatImporter {
	ORBLIT_CLASS(EditorSceneFormatImporterBlend, EditorSceneFormatImporter);

	int blender_major_version = -1;
	int blender_minor_version = -1;
	String last_tested_blender_path;

public:
	enum {
		BLEND_VISIBLE_ALL,
		BLEND_VISIBLE_VISIBLE_ONLY,
		BLEND_VISIBLE_RENDERABLE
	};
	enum {
		BLEND_BONE_INFLUENCES_NONE,
		BLEND_BONE_INFLUENCES_COMPATIBLE,
		BLEND_BONE_INFLUENCES_ALL
	};
	enum {
		BLEND_MATERIAL_EXPORT_PLACEHOLDER,
		BLEND_MATERIAL_EXPORT_EXPORT,
		BLEND_MATERIAL_EXPORT_NAMED_PLACEHOLDER,
	};
	enum {
		BLEND_MODIFIERS_NONE,
		BLEND_MODIFIERS_ALL
	};
	enum {
		BLEND_VERTEX_COLOR_MATERIAL,
		BLEND_VERTEX_COLOR_ACTIVE,
		BLEND_VERTEX_COLOR_NONE
	};

	virtual void get_extensions(List<String> *r_extensions) const override;
	virtual Entity *import_scene(const String &p_path, uint32_t p_flags,
			const HashMap<StringName, Variant> &p_options,
			List<String> *r_missing_deps, Error *r_err = nullptr) override;
	virtual void get_import_options(const String &p_path,
			List<ResourceImporter::ImportOption> *r_options) override;
	virtual Variant get_option_visibility(const String &p_path, const String &p_scene_import_type, const String &p_option,
			const HashMap<StringName, Variant> &p_options) override;
	virtual void handle_compatibility_options(HashMap<StringName, Variant> &p_import_params) const override;
};

class LineEdit;
class Button;
class EditorFileDialog;
class Label;

class EditorFileSystemImportFormatSupportQueryBlend : public EditorFileSystemImportFormatSupportQuery {
	ORBLIT_CLASS(EditorFileSystemImportFormatSupportQueryBlend, EditorFileSystemImportFormatSupportQuery);

	ConfirmationDialog *configure_blender_dialog = nullptr;
	LineEdit *blender_path = nullptr;
	Button *blender_path_browse = nullptr;
	EditorFileDialog *browse_dialog = nullptr;
	Label *path_status = nullptr;
	bool confirmed = false;

	String auto_detected_path;
	void _validate_path(String p_path);

	bool _autodetect_path();

	void _path_confirmed();

	void _select_install(String p_path);
	void _browse_install();

	void _update_icons();

public:
	virtual bool is_active() const override;
	virtual Vector<String> get_file_extensions() const override;
	virtual bool query() override;
};
