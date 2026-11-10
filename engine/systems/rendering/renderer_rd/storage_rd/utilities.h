/**************************************************************************/
/*  utilities.h                                                           */

#pragma once

#include "core/containers/rid_owner.h"
#include "systems/rendering/storage/utilities.h"

namespace RendererRD {

/* VISIBILITY NOTIFIER */

struct VisibilityNotifier {
	AABB aabb;
	Callable enter_callback;
	Callable exit_callback;
	Dependency dependency;
};

class Utilities : public RendererUtilities {
private:
	static Utilities *singleton;

	/* VISIBILITY NOTIFIER */

	mutable RID_Owner<VisibilityNotifier> visibility_notifier_owner;

	/* MISC */

	//keep cached since it can be called form any thread
	uint64_t texture_mem_cache = 0;
	uint64_t buffer_mem_cache = 0;
	uint64_t total_mem_cache = 0;

public:
	static Utilities *get_singleton() { return singleton; }

	Utilities();
	virtual ~Utilities() override;

	/* INSTANCES */

	virtual RSE::InstanceType get_base_type(RID p_rid) const override;
	virtual bool free(RID p_rid) override;

	/* DEPENDENCIES */

	virtual void base_update_dependency(RID p_base, DependencyTracker *p_instance) override;

	/* VISIBILITY NOTIFIER */

	VisibilityNotifier *get_visibility_notifier(RID p_rid) { return visibility_notifier_owner.get_or_null(p_rid); }
	bool owns_visibility_notifier(RID p_rid) const { return visibility_notifier_owner.owns(p_rid); }

	virtual RID visibility_notifier_allocate() override;
	virtual void visibility_notifier_initialize(RID p_notifier) override;
	virtual void visibility_notifier_free(RID p_notifier) override;

	virtual void visibility_notifier_set_aabb(RID p_notifier, const AABB &p_aabb) override;
	virtual void visibility_notifier_set_callbacks(RID p_notifier, const Callable &p_enter_callbable, const Callable &p_exit_callable) override;

	virtual AABB visibility_notifier_get_aabb(RID p_notifier) const override;
	virtual void visibility_notifier_call(RID p_notifier, bool p_enter, bool p_deferred) override;

	/* TIMING */

	virtual void capture_timestamps_begin() override;
	virtual void capture_timestamp(const String &p_name) override;
	virtual uint32_t get_captured_timestamps_count() const override;
	virtual uint64_t get_captured_timestamps_frame() const override;
	virtual uint64_t get_captured_timestamp_gpu_time(uint32_t p_index) const override;
	virtual uint64_t get_captured_timestamp_cpu_time(uint32_t p_index) const override;
	virtual String get_captured_timestamp_name(uint32_t p_index) const override;

	/* MISC */

	virtual void update_dirty_resources() override;
	virtual void set_debug_generate_wireframes(bool p_generate) override {}

	virtual bool has_os_feature(const String &p_feature) const override;

	virtual void update_memory_info() override;

	virtual uint64_t get_rendering_info(RSE::RenderingInfo p_info) override;

	virtual String get_video_adapter_name() const override;
	virtual String get_video_adapter_vendor() const override;
#ifdef RD_ENABLED
	virtual RenderingDeviceEnums::DeviceType get_video_adapter_type() const override;
#endif
	virtual String get_video_adapter_api_version() const override;

	virtual Size2i get_maximum_viewport_size() const override;
	virtual uint32_t get_maximum_shader_varyings() const override;
	virtual uint64_t get_maximum_uniform_buffer_size() const override;
};

} // namespace RendererRD
