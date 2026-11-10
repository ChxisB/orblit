/**************************************************************************/
/*  editor_scene_importer_ufbx.h                                          */

#pragma once

#include "editor/import/spatial/resource_importer_scene.h"

class Animation;
class Entity;

class EditorSceneFormatImporterUFBX : public EditorSceneFormatImporter {
	ORBLIT_CLASS(EditorSceneFormatImporterUFBX, EditorSceneFormatImporter);

public:
	enum FBX_IMPORTER_TYPE {
		FBX_IMPORTER_UFBX,
		FBX_IMPORTER_FBX2GLTF,
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
