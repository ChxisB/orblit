/**************************************************************************/
/*  shader_baker_export_plugin_platform_d3d12.h                           */

#pragma once

#include "editor/export/shader_baker/shader_baker_export_plugin.h"

class ShaderBakerExportPluginPlatformD3D12 : public ShaderBakerExportPluginPlatform {
	ORBLIT_CLASS(ShaderBakerExportPluginPlatformD3D12, ShaderBakerExportPluginPlatform);

private:
	void *lib_d3d12 = nullptr;

public:
	virtual RenderingShaderContainerFormat *create_shader_container_format(const Ref<EditorExportPlatform> &p_platform, const Ref<EditorExportPreset> &p_preset) override;
	virtual bool matches_driver(const String &p_driver) override;
	virtual ~ShaderBakerExportPluginPlatformD3D12() override;
};
