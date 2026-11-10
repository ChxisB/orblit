/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "shader_graph.h"
#include "visual_shader.h"
#include "visual_shader_group.h"
#include "vs_nodes/visual_shader_nodes.h"
#include "vs_nodes/visual_shader_particle_nodes.h"
#include "vs_nodes/visual_shader_sdf_nodes.h"

#include "core/object/class_registry.h"

#ifdef TOOLS_ENABLED
#include "editor/editor_node.h"
#include "editor/visual_shader_editor_plugin.h"
#include "editor/visual_shader_language_plugin.h"

static void _editor_init() {
	Ref<EditorInspectorVisualShaderModePlugin> visual_shader_mode_plugin;
	visual_shader_mode_plugin.instantiate();
	EditorInspector::add_inspector_plugin(visual_shader_mode_plugin);

	Ref<VisualShaderConversionPlugin> visual_shader_convert;
	visual_shader_convert.instantiate();
	EditorNode::get_singleton()->add_resource_conversion_plugin(visual_shader_convert);

	Ref<VisualShaderLanguagePlugin> visual_shader_lang;
	visual_shader_lang.instantiate();
	EditorShaderLanguagePlugin::register_shader_language(visual_shader_lang);

	VisualShaderEditor::register_editor();
}
#endif // TOOLS_ENABLED

void initialize_visual_shader_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_SCENE) {
		ORBLIT_REGISTER_INTERNAL_CLASS(ShaderGraph);
		ORBLIT_REGISTER_CLASS(VisualShader);
		ORBLIT_REGISTER_CLASS(VisualShaderGroup);
		ORBLIT_REGISTER_ABSTRACT_CLASS(VisualShaderNode);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeCustom);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeInput);
		ORBLIT_REGISTER_ABSTRACT_CLASS(VisualShaderNodeOutput);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeGroupInput);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeGroupOutput);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeGroup);
		ORBLIT_REGISTER_ABSTRACT_CLASS(VisualShaderNodeResizableBase);
		ORBLIT_REGISTER_ABSTRACT_CLASS(VisualShaderNodeGroupBase);
		ORBLIT_REGISTER_ABSTRACT_CLASS(VisualShaderNodeConstant);
		ORBLIT_REGISTER_ABSTRACT_CLASS(VisualShaderNodeVectorBase);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeFrame);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeFloatConstant);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeIntConstant);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeUIntConstant);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeBooleanConstant);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeColorConstant);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeVec2Constant);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeVec3Constant);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeVec4Constant);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTransformConstant);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeFloatOp);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeIntOp);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeUIntOp);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeVectorOp);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeColorOp);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTransformOp);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTransformVecMult);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeFloatFunc);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeIntFunc);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeUIntFunc);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeVectorFunc);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeColorFunc);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTransformFunc);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeUVFunc);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeUVPolarCoord);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeDotProduct);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeVectorLen);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeDeterminant);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeDerivativeFunc);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeClamp);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeFaceForward);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeOuterProduct);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeSmoothStep);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeStep);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeVectorDistance);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeVectorRefract);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeMix);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeVectorCompose);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTransformCompose);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeVectorDecompose);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTransformDecompose);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTexture);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeCurveTexture);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeCurveXYZTexture);
		ORBLIT_REGISTER_ABSTRACT_CLASS(VisualShaderNodeSample3D);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTexture2DArray);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTexture3D);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeCubemap);
		ORBLIT_REGISTER_ABSTRACT_CLASS(VisualShaderNodeParameter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeParameterRef);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeFloatParameter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeIntParameter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeUIntParameter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeBooleanParameter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeColorParameter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeVec2Parameter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeVec3Parameter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeVec4Parameter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTransformParameter);
		ORBLIT_REGISTER_ABSTRACT_CLASS(VisualShaderNodeTextureParameter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTexture2DParameter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTextureParameterTriplanar);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTexture2DArrayParameter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTexture3DParameter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeCubemapParameter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeLinearSceneDepth);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeWorldPositionFromDepth);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeScreenNormalWorldSpace);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeIf);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeSwitch);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeFresnel);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeExpression);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeGlobalExpression);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeIs);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeCompare);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeMultiplyAdd);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeBillboard);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeDistanceFade);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeProximityFade);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeRandomRange);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeRemap);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeRotationByAxis);
		ORBLIT_REGISTER_ABSTRACT_CLASS(VisualShaderNodeVarying);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeVaryingSetter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeVaryingGetter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeReroute);

		ORBLIT_REGISTER_CLASS(VisualShaderNodeSDFToScreenUV);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeScreenUVToSDF);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTextureSDF);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeTextureSDFNormal);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeSDFRaymarch);

		ORBLIT_REGISTER_CLASS(VisualShaderNodeParticleOutput);
		ORBLIT_REGISTER_ABSTRACT_CLASS(VisualShaderNodeParticleEmitter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeParticleSphereEmitter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeParticleBoxEmitter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeParticleRingEmitter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeParticleMeshEmitter);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeParticleMultiplyByAxisAngle);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeParticleConeVelocity);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeParticleRandomness);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeParticleAccelerator);
		ORBLIT_REGISTER_CLASS(VisualShaderNodeParticleEmit);
#ifdef TOOLS_ENABLED
	} else if (p_level == MODULE_INITIALIZATION_LEVEL_EDITOR) {
		EditorNode::add_init_callback(_editor_init);
#endif // TOOLS_ENABLED
	}
}

void uninitialize_visual_shader_module(ModuleInitializationLevel p_level) {}
