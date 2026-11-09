/**************************************************************************/
/*  rendering_context_driver_d3d12.h                                      */

#pragma once

#include "backends/d3d12/rendering_device_driver_d3d12.h"
#include "systems/display/display_system_enums.h"
#include "systems/rendering/rendering_context_driver.h"

#include <backends/d3d12/d3dx12.h>

#ifdef DCOMP_ENABLED
#include <dcomp.h>
#endif

#include <wrl/client.h>

#define ARRAY_SIZE(a) std_size(a)

class RenderingContextDriverD3D12 : public RenderingContextDriver {
	Microsoft::WRL::ComPtr<ID3D12DeviceFactory> device_factory;
	Microsoft::WRL::ComPtr<IDXGIFactory2> dxgi_factory;
	TightLocalVector<Device> driver_devices;
	bool tearing_supported = false;

	Error _init_device_factory();
	Error _initialize_debug_layers();
	Error _create_dxgi_factory();
	Error _initialize_devices();

public:
	virtual Error initialize() override;
	virtual const Device &device_get(uint32_t p_device_index) const override;
	virtual uint32_t device_get_count() const override;
	virtual bool device_supports_present(uint32_t p_device_index, SurfaceID p_surface) const override;
	virtual RenderingDeviceDriver *driver_create() override;
	virtual void driver_free(RenderingDeviceDriver *p_driver) override;
	virtual SurfaceID surface_create(const void *p_platform_data) override;
	virtual void surface_set_size(SurfaceID p_surface, uint32_t p_width, uint32_t p_height) override;
	virtual void surface_set_vsync_mode(SurfaceID p_surface, DisplaySystemEnums::VSyncMode p_vsync_mode) override;
	virtual DisplaySystemEnums::VSyncMode surface_get_vsync_mode(SurfaceID p_surface) const override;
	virtual void surface_set_hdr_output_enabled(SurfaceID p_surface, bool p_enabled) override;
	virtual bool surface_get_hdr_output_enabled(SurfaceID p_surface) const override;
	virtual void surface_set_hdr_output_reference_luminance(SurfaceID p_surface, float p_reference_luminance) override;
	virtual float surface_get_hdr_output_reference_luminance(SurfaceID p_surface) const override;
	virtual void surface_set_hdr_output_max_luminance(SurfaceID p_surface, float p_max_luminance) override;
	virtual float surface_get_hdr_output_max_luminance(SurfaceID p_surface) const override;
	virtual void surface_set_hdr_output_linear_luminance_scale(SurfaceID p_surface, float p_linear_luminance_scale) override;
	virtual float surface_get_hdr_output_linear_luminance_scale(SurfaceID p_surface) const override;
	virtual float surface_get_hdr_output_max_value(SurfaceID p_surface) const override;
	virtual uint32_t surface_get_width(SurfaceID p_surface) const override;
	virtual uint32_t surface_get_height(SurfaceID p_surface) const override;
	virtual void surface_set_needs_resize(SurfaceID p_surface, bool p_needs_resize) override;
	virtual bool surface_get_needs_resize(SurfaceID p_surface) const override;
	virtual void surface_destroy(SurfaceID p_surface) override;
	virtual bool is_debug_utils_enabled() const override;

	// Platform-specific data for the Windows embedded in this driver.
	struct WindowPlatformData {
		HWND window;
	};

	// D3D12-only methods.
	struct Surface {
		HWND hwnd = nullptr;
		uint32_t width = 0;
		uint32_t height = 0;
		DisplaySystemEnums::VSyncMode vsync_mode = DisplaySystemEnums::VSYNC_ENABLED;
		bool needs_resize = false;

		bool hdr_output = false;
		// BT.2408 recommendation of 203 nits for HDR Reference White, rounded to 200
		// to be a more pleasant player-facing value. This value is used by Steam
		// Deck and other Windows emulation that does not provide an SDRWhiteLevel.
		float hdr_reference_luminance = 200.0f;
		float hdr_max_luminance = 1000.0f;
		float hdr_linear_luminance_scale = 80.0f;

#ifdef DCOMP_ENABLED
		Microsoft::WRL::ComPtr<IDCompositionDevice> composition_device;
		Microsoft::WRL::ComPtr<IDCompositionTarget> composition_target;
		Microsoft::WRL::ComPtr<IDCompositionVisual> composition_visual;
#endif
	};

	HMODULE lib_d3d12 = nullptr;
	HMODULE lib_dxgi = nullptr;
#ifdef DCOMP_ENABLED
	HMODULE lib_dcomp = nullptr;
#endif

	IDXGIAdapter1 *create_adapter(uint32_t p_adapter_index) const;
	ID3D12DeviceFactory *device_factory_get() const;
	IDXGIFactory2 *dxgi_factory_get();
	bool get_tearing_supported() const;
	bool use_validation_layers() const;

	RenderingContextDriverD3D12();
	virtual ~RenderingContextDriverD3D12() override;
};
