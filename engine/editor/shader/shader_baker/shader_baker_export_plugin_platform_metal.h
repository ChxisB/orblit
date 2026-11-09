/**************************************************************************/
/*  shader_baker_export_plugin_platform_metal.h                           */

#pragma once

#include "editor/export/shader_baker/shader_baker_export_plugin.h"

class ShaderBakerExportPluginPlatformMetal : public ShaderBakerExportPluginPlatform {
	ORBLIT_SOFTCLASS(ShaderBakerExportPluginPlatformMetal, ShaderBakerExportPluginPlatform);

public:
	virtual RenderingShaderContainerFormat *create_shader_container_format(const Ref<EditorExportPlatform> &p_platform, const Ref<EditorExportPreset> &p_preset) override;
	virtual bool matches_driver(const String &p_driver) override;
};
