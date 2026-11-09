/**************************************************************************/
/*  image_compress_betsy.h                                                */

#pragma once

#include "core/io/image.h"
#include "core/object/worker_thread_pool.h"
#include "core/containers/command_queue_mt.h"

class RDShaderFile;
class RenderingDevice;
class RenderingContextDriver;

enum BetsyFormat {
	BETSY_FORMAT_BC1,
	BETSY_FORMAT_BC1_DITHER,
	BETSY_FORMAT_BC3,
	BETSY_FORMAT_BC4_SIGNED,
	BETSY_FORMAT_BC4_UNSIGNED,
	BETSY_FORMAT_BC5_SIGNED,
	BETSY_FORMAT_BC5_UNSIGNED,
	BETSY_FORMAT_BC6_SIGNED,
	BETSY_FORMAT_BC6_UNSIGNED,
	BETSY_FORMAT_MAX,
};

enum BetsyShaderType {
	BETSY_SHADER_BC1_STANDARD,
	BETSY_SHADER_BC1_DITHER,
	BETSY_SHADER_BC4_SIGNED,
	BETSY_SHADER_BC4_UNSIGNED,
	BETSY_SHADER_BC6_SIGNED,
	BETSY_SHADER_BC6_UNSIGNED,
	BETSY_SHADER_ALPHA_STITCH,
	BETSY_SHADER_RGB_TO_RGBA_FLOAT,
	BETSY_SHADER_RGB_TO_RGBA_HALF,
	BETSY_SHADER_RGB_TO_RGBA_UNORM8,
	BETSY_SHADER_RGB_TO_RGBA_UNORM16,
	BETSY_SHADER_MAX,
};

struct BC6PushConstant {
	float sizeX;
	float sizeY;
	uint32_t padding[2] = { 0 };
};

struct BC1PushConstant {
	uint32_t num_refines;
	uint32_t padding[3] = { 0 };
};

struct BC4PushConstant {
	uint32_t channel_idx;
	uint32_t padding[3] = { 0 };
};

struct RGBToRGBAPushConstant {
	uint32_t width;
	uint32_t height;
	uint32_t padding[2];
};

void free_device();

Error _betsy_compress_bptc(Image *r_img, Image::UsedChannels p_channels, Image::BPTCFormat p_bptc_format);
Error _betsy_compress_s3tc(Image *r_img, Image::UsedChannels p_channels);

class BetsyCompressor : public Object {
	ORBLIT_SOFTCLASS(BetsyCompressor, Object);

	mutable CommandQueueMT command_queue;
	bool exit = false;
	WorkerThreadPool::TaskID task_id = WorkerThreadPool::INVALID_TASK_ID;

	struct BetsyShader {
		RID compiled;
		RID pipeline;
	};

	// Resources shared by all compression formats.
	RenderingDevice *compress_rd = nullptr;
	RenderingContextDriver *compress_rcd = nullptr;
	BetsyShader cached_shaders[BETSY_SHADER_MAX];
	RID src_sampler;

	// Format-specific resources.
	RID dxt1_encoding_table_buffer;

	void _init();
	void _assign_mt_ids(WorkerThreadPool::TaskID p_pump_task_id);
	void _thread_loop();
	void _thread_exit();

	Error _get_shader(BetsyFormat p_format, const String &p_version, BetsyShader &r_shader);
	Error _compress(BetsyFormat p_format, Image *r_img);

public:
	void init();
	void finish();

	Error compress(BetsyFormat p_format, Image *r_img) {
		Error err;
		command_queue.push_and_ret(this, &BetsyCompressor::_compress, &err, p_format, r_img);
		return err;
	}
};
