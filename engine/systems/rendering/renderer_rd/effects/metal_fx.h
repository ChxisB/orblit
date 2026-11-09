/**************************************************************************/
/*  metal_fx.h                                                            */

#pragma once

#if defined(METAL_ENABLED) && !defined(VISIONOS_ENABLED)
#define METAL_MFXTEMPORAL_ENABLED
#endif

#ifdef METAL_ENABLED

#include "core/containers/paged_allocator.h"
#include "systems/rendering/renderer_rd/effects/spatial_upscaler.h"
#include "systems/rendering/renderer_scene_render.h"
#include "systems/rendering/rendering_device_driver.h"

namespace MTLFX {
class SpatialScalerBase;
class TemporalScalerBase;
} //namespace MTLFX

namespace RendererRD {

struct MFXSpatialContext {
	MTLFX::SpatialScalerBase *scaler = nullptr;
	MFXSpatialContext() = default;
	~MFXSpatialContext();
};

class MFXSpatialEffect : public SpatialUpscaler {
	struct CallbackArgs {
		MFXSpatialEffect *owner = nullptr;
		MTLFX::SpatialScalerBase *scaler = nullptr;
		RDD::TextureID src;
		RDD::TextureID dst;

		CallbackArgs(MFXSpatialEffect *p_owner, RDD::TextureID p_src, RDD::TextureID p_dst, const MFXSpatialContext &p_ctx) :
				owner(p_owner), scaler(p_ctx.scaler), src(p_src), dst(p_dst) {}

		static void free(CallbackArgs **p_args) {
			(*p_args)->owner->args_allocator.free(*p_args);
			*p_args = nullptr;
		}
	};

	PagedAllocator<CallbackArgs, true, 16> args_allocator;
	static void callback(RDD *p_driver, RDD::CommandBufferID p_command_buffer, CallbackArgs *p_userdata);

public:
	virtual const Span<char> get_label() const final { return "MetalFX Spatial Upscale"; }
	virtual void ensure_context(Ref<RenderSceneBuffersRD> p_render_buffers) final;
	virtual void process(Ref<RenderSceneBuffersRD> p_render_buffers, RID p_src, RID p_dst) final;

	struct CreateParams {
		Vector2i input_size;
		Vector2i output_size;
		RDD::DataFormat input_format;
		RDD::DataFormat output_format;
	};

	MFXSpatialContext *create_context(CreateParams p_params) const;

	MFXSpatialEffect();
	~MFXSpatialEffect();
};

#ifdef METAL_MFXTEMPORAL_ENABLED

struct MFXTemporalContext {
	MTLFX::TemporalScalerBase *scaler = nullptr;
	MFXTemporalContext() = default;
	~MFXTemporalContext();
};

class MFXTemporalEffect {
	struct CallbackArgs {
		MFXTemporalEffect *owner = nullptr;
		MTLFX::TemporalScalerBase *scaler = nullptr;
		RDD::TextureID src;
		RDD::TextureID depth;
		RDD::TextureID motion;
		RDD::TextureID exposure;
		Vector2 jitter_offset;
		RDD::TextureID dst;
		bool reset = false;

		CallbackArgs(
				MFXTemporalEffect *p_owner,
				RDD::TextureID p_src,
				RDD::TextureID p_depth,
				RDD::TextureID p_motion,
				RDD::TextureID p_exposure,
				Vector2 p_jitter_offset,
				RDD::TextureID p_dst,
				const MFXTemporalContext &p_ctx,
				bool p_reset) :
				owner(p_owner),
				scaler(p_ctx.scaler),
				src(p_src),
				depth(p_depth),
				motion(p_motion),
				exposure(p_exposure),
				jitter_offset(p_jitter_offset),
				dst(p_dst),
				reset(p_reset) {}

		static void free(CallbackArgs **p_args) {
			(*p_args)->owner->args_allocator.free(*p_args);
			*p_args = nullptr;
		}
	};

	PagedAllocator<CallbackArgs, true, 16> args_allocator;

	static void callback(RDD *p_driver, RDD::CommandBufferID p_command_buffer, CallbackArgs *p_userdata);

public:
	MFXTemporalEffect();
	~MFXTemporalEffect();

	struct CreateParams {
		Vector2i input_size;
		Vector2i output_size;
		RDD::DataFormat input_format;
		RDD::DataFormat depth_format;
		RDD::DataFormat motion_format;
		RDD::DataFormat reactive_format;
		RDD::DataFormat output_format;
		Vector2 motion_vector_scale;
	};

	MFXTemporalContext *create_context(CreateParams p_params) const;

	struct Params {
		RID src;
		RID depth;
		RID motion;
		RID exposure;
		RID dst;
		Vector2 jitter_offset;
		bool reset = false;
	};

	void process(MFXTemporalContext *p_ctx, Params p_params);
};

#endif

} //namespace RendererRD

#endif // METAL_ENABLED
