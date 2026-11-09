/**************************************************************************/
/*  noise_texture_spatial.h                                                    */

#pragma once

#include "noise.h"

#include "core/object/ref_counted.h"
#include "core/object/worker_thread_pool.h"
#include "scene/resources/gradient.h"
#include "scene/resources/texture.h"

class NoiseTexture3D : public Texture3D {
	ORBLIT_CLASS(NoiseTexture3D, Texture3D);

private:
	WorkerThreadPool::TaskID current_task_id = WorkerThreadPool::INVALID_TASK_ID;
	bool first_time = true;
	bool update_queued = false;
	bool regen_queued = false;

	mutable RID texture;
	uint32_t flags = 0;

	int width = 64;
	int height = 64;
	int depth = 64;
	bool invert = false;
	bool seamless = false;
	real_t seamless_blend_skirt = 0.1;
	bool normalize = true;

	Ref<Gradient> color_ramp;
	Ref<Noise> noise;

	Image::Format format = Image::FORMAT_L8;

	void _thread_done(const TypedArray<Image> &p_data);
	void _thread_function();

	void _queue_update();
	TypedArray<Image> _generate_texture();
	void _update_texture();
	void _set_texture_data(const TypedArray<Image> &p_data);

	Ref<Image> _modulate_with_gradient(Ref<Image> p_image, Ref<Gradient> p_gradient);

protected:
	static void register_members();
	void _validate_property(PropertyInfo &p_property) const;

public:
	void set_noise(Ref<Noise> p_noise);
	Ref<Noise> get_noise();

	void set_width(int p_width);
	void set_height(int p_height);
	void set_depth(int p_depth);

	void set_invert(bool p_invert);
	bool get_invert() const;

	void set_seamless(bool p_seamless);
	bool get_seamless();

	void set_seamless_blend_skirt(real_t p_blend_skirt);
	real_t get_seamless_blend_skirt();

	void set_normalize(bool p_normalize);
	bool is_normalized() const;

	void set_color_ramp(const Ref<Gradient> &p_gradient);
	Ref<Gradient> get_color_ramp() const;

	virtual int get_width() const override;
	virtual int get_height() const override;
	virtual int get_depth() const override;

	virtual bool has_mipmaps() const override;

	virtual RID get_rid() const override;

	virtual Vector<Ref<Image>> get_data() const override;
	virtual Image::Format get_format() const override;

	NoiseTexture3D();
	virtual ~NoiseTexture3D();
};
