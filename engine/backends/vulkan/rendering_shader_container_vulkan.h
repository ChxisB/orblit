/**************************************************************************/
/*  rendering_shader_container_vulkan.h                                   */

#pragma once

#include "systems/rendering/rendering_shader_container.h"

class RenderingShaderContainerVulkan : public RenderingShaderContainer {
	ORBLIT_SOFTCLASS(RenderingShaderContainerVulkan, RenderingShaderContainer);

public:
	static const uint32_t FORMAT_VERSION;

	enum CompressionFlagsVulkan {
		COMPRESSION_FLAG_SMOLV = 0x10000,
	};

	bool debug_info_enabled = false;

protected:
	virtual uint32_t _format() const override;
	virtual uint32_t _format_version() const override;
	virtual bool _set_code_from_spirv(const ReflectShader &p_shader) override;

public:
	RenderingShaderContainerVulkan(bool p_debug_info_enabled);
};

class RenderingShaderContainerFormatVulkan : public RenderingShaderContainerFormat {
private:
	bool debug_info_enabled = false;

public:
	virtual Ref<RenderingShaderContainer> create_container() const override;
	virtual ShaderLanguageVersion get_shader_language_version() const override;
	virtual ShaderSpirvVersion get_shader_spirv_version(ShaderStage p_stage) const override;
	void set_debug_info_enabled(bool p_debug_info_enabled);
	bool get_debug_info_enabled() const;
	RenderingShaderContainerFormatVulkan();
	virtual ~RenderingShaderContainerFormatVulkan();
};
