/**************************************************************************/
/*  editor_scene_exporter_gltf_settings.h                                 */

#pragma once

#include "../gltf_document.h"

class EditorSceneExporterGLTFSettings : public RefCounted {
	ORBLIT_CLASS(EditorSceneExporterGLTFSettings, RefCounted);
	List<PropertyInfo> _property_list;
	Ref<GLTFDocument> _document;
	HashMap<String, Ref<GLTFDocumentExtension>> _config_name_to_extension_map;

	String _copyright;
	double _bake_fps = 30.0;

protected:
	static void register_members();
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;
	void _on_extension_property_list_changed();

	bool _set_extension_setting(const String &p_name_str, const Variant &p_value);
	bool _get_extension_setting(const String &p_name_str, Variant &r_ret) const;

public:
	void generate_property_list(Ref<GLTFDocument> p_document, Entity *p_root = nullptr);

	String get_copyright() const;
	void set_copyright(const String &p_copyright);

	double get_bake_fps() const;
	void set_bake_fps(const double p_bake_fps);
};
