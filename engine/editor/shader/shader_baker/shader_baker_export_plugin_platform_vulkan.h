/**************************************************************************/
/*  shader_baker_export_plugin_platform_vulkan.h                          */

#pragma once

#include "editor/export/shader_baker/shader_baker_export_plugin.h"

class ShaderBakerExportPluginPlatformVulkan : public ShaderBakerExportPluginPlatform {
	ORBLIT_CLASS(ShaderBakerExportPluginPlatformVulkan, ShaderBakerExportPluginPlatform);

public:
	virtual RenderingShaderContainerFormat *create_shader_container_format(const Ref<EditorExportPlatform> &p_platform, const Ref<EditorExportPreset> &p_preset) override;
	virtual bool matches_driver(const String &p_driver) override;
};
