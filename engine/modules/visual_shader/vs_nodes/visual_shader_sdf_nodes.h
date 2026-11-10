/**************************************************************************/
/*  visual_shader_sdf_nodes.h                                             */

#pragma once

#include "../visual_shader.h"

class VisualShaderNodeSDFToScreenUV : public VisualShaderNode {
	ORBLIT_CLASS(VisualShaderNodeSDFToScreenUV, VisualShaderNode);

public:
	virtual String get_caption() const override;

	virtual int get_input_port_count() const override;
	virtual PortType get_input_port_type(int p_port) const override;
	virtual String get_input_port_name(int p_port) const override;

	virtual int get_output_port_count() const override;
	virtual PortType get_output_port_type(int p_port) const override;
	virtual String get_output_port_name(int p_port) const override;

	virtual String generate_code(Shader::Mode p_mode, VisualShader::Type p_type, int p_id, const String *p_input_vars, const String *p_output_vars, bool p_for_preview = false) const override;

	virtual Category get_category() const override { return CATEGORY_TEXTURES; }
	virtual bool is_available(Shader::Mode p_mode, VisualShader::Type p_type) const override { return p_mode == Shader::MODE_CANVAS_ITEM; }
};

class VisualShaderNodeScreenUVToSDF : public VisualShaderNode {
	ORBLIT_CLASS(VisualShaderNodeScreenUVToSDF, VisualShaderNode);

public:
	virtual String get_caption() const override;

	virtual int get_input_port_count() const override;
	virtual PortType get_input_port_type(int p_port) const override;
	virtual String get_input_port_name(int p_port) const override;

	virtual int get_output_port_count() const override;
	virtual PortType get_output_port_type(int p_port) const override;
	virtual String get_output_port_name(int p_port) const override;

	virtual bool is_input_port_default(int p_port, Shader::Mode p_mode) const override;
	virtual String generate_code(Shader::Mode p_mode, VisualShader::Type p_type, int p_id, const String *p_input_vars, const String *p_output_vars, bool p_for_preview = false) const override;

	virtual Category get_category() const override { return CATEGORY_TEXTURES; }
	virtual bool is_available(Shader::Mode p_mode, VisualShader::Type p_type) const override { return p_mode == Shader::MODE_CANVAS_ITEM; }
};

class VisualShaderNodeTextureSDF : public VisualShaderNode {
	ORBLIT_CLASS(VisualShaderNodeTextureSDF, VisualShaderNode);

public:
	virtual String get_caption() const override;

	virtual int get_input_port_count() const override;
	virtual PortType get_input_port_type(int p_port) const override;
	virtual String get_input_port_name(int p_port) const override;

	virtual int get_output_port_count() const override;
	virtual PortType get_output_port_type(int p_port) const override;
	virtual String get_output_port_name(int p_port) const override;

	virtual String generate_code(Shader::Mode p_mode, VisualShader::Type p_type, int p_id, const String *p_input_vars, const String *p_output_vars, bool p_for_preview = false) const override;

	virtual Category get_category() const override { return CATEGORY_TEXTURES; }
	virtual bool is_available(Shader::Mode p_mode, VisualShader::Type p_type) const override { return p_mode == Shader::MODE_CANVAS_ITEM; }
};

class VisualShaderNodeTextureSDFNormal : public VisualShaderNode {
	ORBLIT_CLASS(VisualShaderNodeTextureSDFNormal, VisualShaderNode);

public:
	virtual String get_caption() const override;

	virtual int get_input_port_count() const override;
	virtual PortType get_input_port_type(int p_port) const override;
	virtual String get_input_port_name(int p_port) const override;

	virtual int get_output_port_count() const override;
	virtual PortType get_output_port_type(int p_port) const override;
	virtual String get_output_port_name(int p_port) const override;

	virtual String generate_code(Shader::Mode p_mode, VisualShader::Type p_type, int p_id, const String *p_input_vars, const String *p_output_vars, bool p_for_preview = false) const override;

	virtual Category get_category() const override { return CATEGORY_TEXTURES; }
	virtual bool is_available(Shader::Mode p_mode, VisualShader::Type p_type) const override { return p_mode == Shader::MODE_CANVAS_ITEM; }
};

class VisualShaderNodeSDFRaymarch : public VisualShaderNode {
	ORBLIT_CLASS(VisualShaderNodeSDFRaymarch, VisualShaderNode);

public:
	virtual String get_caption() const override;

	virtual int get_input_port_count() const override;
	virtual PortType get_input_port_type(int p_port) const override;
	virtual String get_input_port_name(int p_port) const override;

	virtual int get_output_port_count() const override;
	virtual PortType get_output_port_type(int p_port) const override;
	virtual String get_output_port_name(int p_port) const override;

	virtual String generate_code(Shader::Mode p_mode, VisualShader::Type p_type, int p_id, const String *p_input_vars, const String *p_output_vars, bool p_for_preview = false) const override;

	virtual Category get_category() const override { return CATEGORY_TEXTURES; }
	virtual bool is_available(Shader::Mode p_mode, VisualShader::Type p_type) const override { return p_mode == Shader::MODE_CANVAS_ITEM; }

	VisualShaderNodeSDFRaymarch();
};
