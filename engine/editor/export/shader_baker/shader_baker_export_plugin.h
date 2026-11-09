/**************************************************************************/
/*  shader_baker_export_plugin.h                                          */

#pragma once

#include "core/object/object.h"
#include "core/object/worker_thread_pool.h"
#include "core/os/condition_variable.h"
#include "core/containers/rb_set.h"
#include "editor/export/editor_export_plugin.h"

class ShaderRD;
class RenderingShaderContainerFormat;

class ShaderBakerExportPluginPlatform : public RefCounted {
	ORBLIT_CLASS(ShaderBakerExportPluginPlatform, RefCounted);

public:
	virtual RenderingShaderContainerFormat *create_shader_container_format(const Ref<EditorExportPlatform> &p_platform, const Ref<EditorExportPreset> &p_preset) = 0;
	virtual bool matches_driver(const String &p_driver) = 0;
	virtual ~ShaderBakerExportPluginPlatform() {}
};

class ShaderBakerExportPlugin : public EditorExportPlugin {
	ORBLIT_SOFTCLASS(ShaderBakerExportPlugin, EditorExportPlugin);

protected:
	struct WorkItem {
		String cache_path;
		String shader_name;
		Vector<String> stage_sources;
		Vector<uint64_t> dynamic_buffers;
		int64_t variant = 0;
	};

	struct WorkResult {
		// Since this result is per group, this vector will have gaps in the data it covers as the indices must stay relative to all variants.
		Vector<PackedByteArray> variant_data;
	};

	struct ShaderGroupItem {
		String cache_path;
		LocalVector<int> variants;
		LocalVector<WorkerThreadPool::TaskID> variant_tasks;
	};

	String shader_cache_platform_name;
	String shader_cache_renderer_name;
	String shader_cache_export_path;
	RBSet<String> shader_paths_processed;
	HashMap<String, WorkResult> shader_work_results;
	Mutex shader_work_results_mutex;
	LocalVector<ShaderGroupItem> shader_group_items;
	RenderingShaderContainerFormat *shader_container_format = nullptr;
	String shader_container_driver;
	Vector<Ref<ShaderBakerExportPluginPlatform>> platforms;
	uint64_t customization_configuration_hash = 0;
	uint32_t tasks_processed = 0;
	uint32_t tasks_total = 0;
	std::atomic<bool> tasks_cancelled;
	BinaryMutex tasks_mutex;
	ConditionVariable tasks_condition;

	virtual String get_name() const override;
	virtual bool _is_active(const Vector<String> &p_features) const;
	virtual bool _initialize_container_format(const Ref<EditorExportPlatform> &p_platform, const Ref<EditorExportPreset> &p_preset);
	virtual void _cleanup_container_format();
	virtual bool _initialize_cache_directory();
	virtual bool _begin_customize_resources(const Ref<EditorExportPlatform> &p_platform, const Vector<String> &p_features) override;
	virtual bool _begin_customize_scenes(const Ref<EditorExportPlatform> &p_platform, const Vector<String> &p_features) override;
	virtual void _end_customize_resources() override;
	virtual Ref<Resource> _customize_resource(const Ref<Resource> &p_resource, const String &p_path) override;
	virtual Entity *_customize_scene(Entity *p_root, const String &p_path) override;
	virtual uint64_t _get_customization_configuration_hash() const override;
	virtual void _customize_shader_version(ShaderRD *p_shader, RID p_version);
	void _process_work_item(WorkItem p_work_item);

public:
	ShaderBakerExportPlugin();
	virtual ~ShaderBakerExportPlugin() override;
	void add_platform(Ref<ShaderBakerExportPluginPlatform> p_platform);
	void remove_platform(Ref<ShaderBakerExportPluginPlatform> p_platform);
};
