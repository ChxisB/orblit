/**************************************************************************/
/*  editor_export.h                                                       */

#pragma once

#include "editor/export/editor_export_platform.h"
#include "editor/export/editor_export_plugin.h"

class Timer;

class EditorExport : public Entity {
	ORBLIT_CLASS(EditorExport, Entity);

	Vector<Ref<EditorExportPlatform>> export_platforms;
	Vector<Ref<EditorExportPreset>> export_presets;
	Vector<Ref<EditorExportPlugin>> export_plugins;
	HashMap<Ref<EditorExportPlatform>, Ref<EditorExportPreset>> runnable_presets;

	static inline const String RUNNABLE_SECTION_NAME = "runnable_presets";

	static inline StringName _export_presets_updated;
	static inline StringName _export_presets_runnable_updated;

	Timer *save_timer = nullptr;
	bool block_save = false;
	bool should_update_presets = false;
	bool should_reload_presets = false;

	static EditorExport *singleton;

	void _save();

protected:
	friend class EditorExportPreset;
	void save_presets();
	void emit_presets_runnable_changed();

	void _notification(int p_what);
	static void register_members();

public:
	static EditorExport *get_singleton() { return singleton; }

	// Encodes a platform/device pair into a single menu/device id and decodes it back.
	static int encode_platform_device_id(int p_platform_idx, int p_device_idx) { return p_platform_idx * 10000 + p_device_idx; }
	static int decode_platform_from_id(int p_id) { return p_id / 10000; }
	static int decode_device_from_id(int p_id) { return p_id % 10000; }

	void add_export_platform(const Ref<EditorExportPlatform> &p_platform);
	int get_export_platform_count() const;
	int get_export_platform_index_by_name(const String &p_name);
	Ref<EditorExportPlatform> get_export_platform(int p_idx);
	void remove_export_platform(const Ref<EditorExportPlatform> &p_platform);

	void add_export_preset(const Ref<EditorExportPreset> &p_preset, int p_at_pos = -1);
	int get_export_preset_count() const;
	Ref<EditorExportPreset> get_export_preset(int p_idx);
	void remove_export_preset(int p_idx);

	void add_export_plugin(const Ref<EditorExportPlugin> &p_plugin);
	void remove_export_plugin(const Ref<EditorExportPlugin> &p_plugin);
	Vector<Ref<EditorExportPlugin>> get_export_plugins();

	void set_runnable_preset(const Ref<EditorExportPreset> &p_preset);
	void unset_runnable_preset(const Ref<EditorExportPreset> &p_preset);
	Ref<EditorExportPreset> get_runnable_preset_for_platform(const Ref<EditorExportPlatform> &p_for_platform) const;

	void load_config();
	void update_export_presets();
	bool poll_export_platforms();
	bool has_preset_with_name(const String &p_name, int p_exclude_index = -1) const;
	void connect_presets_runnable_updated(const Callable &p_target);

	EditorExport();
};
