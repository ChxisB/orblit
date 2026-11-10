/**************************************************************************/
/*  rendering_shader_library.h                                            */

#pragma once

class String;
template <typename T>
class BitField;

class RenderingShaderLibrary {
public:
	enum FeatureBits {
		FEATURE_ADVANCED_BIT = 1U << 0U,
		FEATURE_MULTIVIEW_BIT = 1U << 1U,
		FEATURE_VRS_BIT = 1U << 2U,
		FEATURE_FP16_BIT = 1U << 3U,
		FEATURE_FP32_BIT = 1U << 4U,
	};

	// Used by the shader baker to globally enable features on all the shaders that will be exported.
	virtual void enable_features(BitField<FeatureBits> p_feature_bits) = 0;

	// Used by the shader baker to reference by name the library.
	virtual String get_name() const = 0;

	virtual ~RenderingShaderLibrary() {}
};
