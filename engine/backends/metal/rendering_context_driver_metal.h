/**************************************************************************/
/*  rendering_context_driver_metal.h                                      */

#pragma once

#ifdef METAL_ENABLED

#include "systems/rendering/rendering_context_driver.h"
#include "systems/rendering/rendering_device_driver.h"

#include <Metal/Metal.hpp>
#include <QuartzCore/QuartzCore.hpp>

namespace MTL3 {
class MDCommandBuffer;
}

class API_AVAILABLE(macos(11.0), ios(14.0), tvos(14.0)) RenderingContextDriverMetal : public RenderingContextDriver {
	bool capture_available = false;

protected:
	MTL::Device *metal_device = nullptr;
	Device device; // There is only one device on Apple Silicon.

public:
	Error initialize() final override;
	const Device &device_get(uint32_t p_device_index) const final override;
	uint32_t device_get_count() const final override;
	bool device_supports_present(uint32_t p_device_index, SurfaceID p_surface) const final override { return true; }
	RenderingDeviceDriver *driver_create() final override;
	void driver_free(RenderingDeviceDriver *p_driver) final override;
	SurfaceID surface_create(const void *p_platform_data) final override;
	void surface_set_size(SurfaceID p_surface, uint32_t p_width, uint32_t p_height) final override;
	void surface_set_vsync_mode(SurfaceID p_surface, DisplaySystemEnums::VSyncMode p_vsync_mode) final override;
	DisplaySystemEnums::VSyncMode surface_get_vsync_mode(SurfaceID p_surface) const final override;
	virtual void surface_set_hdr_output_enabled(SurfaceID p_surface, bool p_enabled) final override;
	virtual bool surface_get_hdr_output_enabled(SurfaceID p_surface) const final override;
	virtual void surface_set_hdr_output_reference_luminance(SurfaceID p_surface, float p_reference_luminance) final override;
	virtual float surface_get_hdr_output_reference_luminance(SurfaceID p_surface) const final override;
	virtual void surface_set_hdr_output_max_luminance(SurfaceID p_surface, float p_max_luminance) final override;
	virtual float surface_get_hdr_output_max_luminance(SurfaceID p_surface) const final override;
	virtual void surface_set_hdr_output_linear_luminance_scale(SurfaceID p_surface, float p_linear_luminance_scale) final override;
	virtual float surface_get_hdr_output_linear_luminance_scale(SurfaceID p_surface) const final override;
	virtual float surface_get_hdr_output_max_value(SurfaceID p_surface) const final override;
	uint32_t surface_get_width(SurfaceID p_surface) const final override;
	uint32_t surface_get_height(SurfaceID p_surface) const final override;
	void surface_set_needs_resize(SurfaceID p_surface, bool p_needs_resize) final override;
	bool surface_get_needs_resize(SurfaceID p_surface) const final override;
	void surface_destroy(SurfaceID p_surface) final override;
	bool is_debug_utils_enabled() const final override { return capture_available; }

#pragma mark - Metal-specific methods

	// Platform-specific data for the Windows embedded in this driver.
	struct WindowPlatformData {
		CA::MetalLayer *layer;
	};

	class API_AVAILABLE(macos(11.0), ios(14.0), tvos(14.0)) Surface {
	protected:
		MTL::Device *device;

	public:
		uint32_t width = 0;
		uint32_t height = 0;
		DisplaySystemEnums::VSyncMode vsync_mode = DisplaySystemEnums::VSYNC_ENABLED;
		double present_minimum_duration = 0.0;
		MTL::PixelFormat pixel_format = MTL::PixelFormatBGRA8Unorm;

		// BT.2408 recommendation of 203 nits for HDR Reference White, rounded to 200
		// to be a more pleasant player-facing value.
		float hdr_reference_luminance = 200.0f;
		float hdr_max_luminance = 1000.0f;
		// linear_luminance_scale must always equal hdr_reference_luminance on Apple platforms.
		float hdr_linear_luminance_scale = 200.0f;
		bool needs_resize = false;
		bool hdr_output = false;

		Surface(MTL::Device *p_device) :
				device(p_device) {}
		virtual ~Surface() = default;

		virtual MTL::PixelFormat get_pixel_format() const { return pixel_format; }
		void set_hdr_output_enabled(bool p_enabled) {
			if (hdr_output != p_enabled) {
				hdr_output = p_enabled;
				needs_resize = true;
			}
		}

		bool is_hdr_output_enabled() const {
			return hdr_output;
		}
		virtual Error resize(uint32_t p_desired_framebuffer_count, RDD::DataFormat &r_format, RDD::ColorSpace &r_color_space) = 0;
		virtual RDD::FramebufferID acquire_next_frame_buffer() = 0;
		virtual void present(MTL3::MDCommandBuffer *p_cmd_buffer) = 0;
		virtual MTL::Drawable *next_drawable() = 0;
		API_AVAILABLE(macos(26.0), ios(26.0))
		virtual MTL::ResidencySet *get_residency_set() const = 0;
		void set_max_fps(int p_max_fps) { present_minimum_duration = p_max_fps ? 1.0 / p_max_fps : 0.0; }
	};

	MTL::Device *get_metal_device() const {
		return metal_device;
	}

#pragma mark - Initialization

	RenderingContextDriverMetal();
	~RenderingContextDriverMetal() override;
};

#endif // METAL_ENABLED
