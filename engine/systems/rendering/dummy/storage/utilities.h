/**************************************************************************/
/*  utilities.h                                                           */

#pragma once

#include "systems/rendering/storage/utilities.h"

namespace RendererDummy {

class Utilities : public RendererUtilities {
private:
	static Utilities *singleton;

public:
	static Utilities *get_singleton() { return singleton; }

	Utilities();
	~Utilities();

	/* INSTANCES */

	virtual RSE::InstanceType get_base_type(RID p_rid) const override;
	virtual bool free(RID p_rid) override;

	/* DEPENDENCIES */

	virtual void base_update_dependency(RID p_base, DependencyTracker *p_instance) override;

	/* VISIBILITY NOTIFIER */

	virtual RID visibility_notifier_allocate() override { return RID(); }
	virtual void visibility_notifier_initialize(RID p_notifier) override {}
	virtual void visibility_notifier_free(RID p_notifier) override {}

	virtual void visibility_notifier_set_aabb(RID p_notifier, const AABB &p_aabb) override {}
	virtual void visibility_notifier_set_callbacks(RID p_notifier, const Callable &p_enter_callbable, const Callable &p_exit_callable) override {}

	virtual AABB visibility_notifier_get_aabb(RID p_notifier) const override { return AABB(); }
	virtual void visibility_notifier_call(RID p_notifier, bool p_enter, bool p_deferred) override {}

	/* TIMING */

	virtual void capture_timestamps_begin() override {}
	virtual void capture_timestamp(const String &p_name) override {}
	virtual uint32_t get_captured_timestamps_count() const override { return 0; }
	virtual uint64_t get_captured_timestamps_frame() const override { return 0; }
	virtual uint64_t get_captured_timestamp_gpu_time(uint32_t p_index) const override { return 0; }
	virtual uint64_t get_captured_timestamp_cpu_time(uint32_t p_index) const override { return 0; }
	virtual String get_captured_timestamp_name(uint32_t p_index) const override { return String(); }

	/* MISC */

	virtual void update_dirty_resources() override {}
	virtual void set_debug_generate_wireframes(bool p_generate) override {}

	virtual bool has_os_feature(const String &p_feature) const override {
		return p_feature == "rgtc" || p_feature == "bptc" || p_feature == "s3tc" || p_feature == "etc2";
	}

	virtual void update_memory_info() override {}

	virtual uint64_t get_rendering_info(RSE::RenderingInfo p_info) override { return 0; }
	virtual String get_video_adapter_name() const override { return String(); }
	virtual String get_video_adapter_vendor() const override { return String(); }
#ifdef RD_ENABLED
	virtual RenderingDeviceEnums::DeviceType get_video_adapter_type() const override { return RenderingDeviceEnums::DeviceType::DEVICE_TYPE_OTHER; }
#endif // RD_ENABLED
	virtual String get_video_adapter_api_version() const override { return String(); }

	virtual Size2i get_maximum_viewport_size() const override { return Size2i(); }
	virtual uint32_t get_maximum_shader_varyings() const override { return 31; } // Fair assumption for everything except old OpenGL-only phones.
	virtual uint64_t get_maximum_uniform_buffer_size() const override { return 65536; } // Fair assumption for all devices.
};

} // namespace RendererDummy
