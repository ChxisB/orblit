/**************************************************************************/
/*  register_system_types.cpp                                             */

#include "register_system_types.h"

#include "core/config/engine.h"
#include "core/config/project_settings.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "core/os/os.h"
#include "systems/audio/audio_effect.h"
#include "systems/audio/audio_frame.h"
#include "systems/audio/audio_system.h"
#include "systems/audio/effects/audio_effect_amplify.h"
#include "systems/audio/effects/audio_effect_capture.h"
#include "systems/audio/effects/audio_effect_chorus.h"
#include "systems/audio/effects/audio_effect_compressor.h"
#include "systems/audio/effects/audio_effect_delay.h"
#include "systems/audio/effects/audio_effect_distortion.h"
#include "systems/audio/effects/audio_effect_eq.h"
#include "systems/audio/effects/audio_effect_filter.h"
#include "systems/audio/effects/audio_effect_hard_limiter.h"
#include "systems/audio/effects/audio_effect_panner.h"
#include "systems/audio/effects/audio_effect_phaser.h"
#include "systems/audio/effects/audio_effect_pitch_shift.h"
#include "systems/audio/effects/audio_effect_record.h"
#include "systems/audio/effects/audio_effect_reverb.h"
#include "systems/audio/effects/audio_effect_spectrum_analyzer.h"
#include "systems/audio/effects/audio_effect_stereo_enhance.h"
#include "systems/camera/camera_feed.h"
#include "systems/camera/camera_system.h"
#include "systems/debugger/systems_debugger.h"
#include "systems/display/accessibility_system.h"
#include "systems/display/display_system.h"
#include "systems/display/native_menu.h"
#include "systems/movie_writer/movie_writer.h"
#include "systems/movie_writer/movie_writer_pngwav.h"
#ifdef RD_ENABLED
#include "systems/rendering/renderer_rd/framebuffer_cache_rd.h"
#include "systems/rendering/renderer_rd/storage_rd/render_data_rd.h"
#include "systems/rendering/renderer_rd/storage_rd/render_scene_buffers_rd.h"
#include "systems/rendering/renderer_rd/storage_rd/render_scene_data_rd.h"
#include "systems/rendering/renderer_rd/uniform_set_cache_rd.h"
#include "systems/rendering/rendering_device.h"
#include "systems/rendering/rendering_device_binds.h"
#endif // RD_ENABLED
#include "systems/rendering/rendering_system.h"
#include "systems/rendering/shader_include_db.h"
#include "systems/rendering/shader_types.h"
#include "systems/rendering/storage/render_data.h"
#include "systems/rendering/storage/render_data_extension.h"
#include "systems/rendering/storage/render_scene_buffers.h"
#include "systems/rendering/storage/render_scene_data.h"
#include "systems/text/text_system.h"
#include "systems/text/text_system_dummy.h"
#include "systems/text/text_system_extension.h"

// 2D physics and navigation.
#ifndef NAVIGATION_PLANAR_DISABLED
#include "systems/navigation_planar/navigation_path_query_parameters_planar.h"
#include "systems/navigation_planar/navigation_path_query_result_planar.h"
#include "systems/navigation_planar/navigation_system_planar.h"
#include "systems/navigation_planar/navigation_system_planar_manager.h"
#endif // NAVIGATION_PLANAR_DISABLED
#ifndef PHYSICS_PLANAR_DISABLED
#include "systems/physics_planar/physics_system_planar.h"
#include "systems/physics_planar/physics_system_planar_dummy.h"
#include "systems/physics_planar/physics_system_planar_extension.h"
#include "systems/physics_planar/physics_system_planar_manager.h"
#endif // PHYSICS_PLANAR_DISABLED

// 3D physics and navigation.
#ifndef NAVIGATION_SPATIAL_DISABLED
#include "systems/navigation_spatial/navigation_path_query_parameters_spatial.h"
#include "systems/navigation_spatial/navigation_path_query_result_spatial.h"
#include "systems/navigation_spatial/navigation_system_spatial.h"
#include "systems/navigation_spatial/navigation_system_spatial_manager.h"
#endif // NAVIGATION_SPATIAL_DISABLED
#ifndef PHYSICS_SPATIAL_DISABLED
#include "systems/physics_spatial/physics_system_spatial.h"
#include "systems/physics_spatial/physics_system_spatial_dummy.h"
#include "systems/physics_spatial/physics_system_spatial_extension.h"
#include "systems/physics_spatial/physics_system_spatial_manager.h"
#endif // PHYSICS_SPATIAL_DISABLED

// XR
#ifndef XR_DISABLED
#include "systems/xr/xr_body_tracker.h"
#include "systems/xr/xr_controller_tracker.h"
#include "systems/xr/xr_face_tracker.h"
#include "systems/xr/xr_hand_tracker.h"
#include "systems/xr/xr_interface.h"
#include "systems/xr/xr_interface_extension.h"
#include "systems/xr/xr_positional_tracker.h"
#include "systems/xr/xr_system.h"
#include "systems/xr/xr_vrs.h"
#endif // XR_DISABLED

ShaderTypes *shader_types = nullptr;

#ifndef PHYSICS_PLANAR_DISABLED
static PhysicsSystem2D *_create_dummy_physics_system_2d() {
	return memnew(PhysicsSystem2DDummy);
}
#endif // PHYSICS_PLANAR_DISABLED

#ifndef PHYSICS_SPATIAL_DISABLED
static PhysicsSystem3D *_create_dummy_physics_system_3d() {
	return memnew(PhysicsSystem3DDummy);
}
#endif // PHYSICS_SPATIAL_DISABLED

static bool has_server_feature_callback(const String &p_feature) {
	if (RenderingSystem::get_singleton()) {
		if (RenderingSystem::get_singleton()->has_os_feature(p_feature)) {
			return true;
		}
	}

	return false;
}

static MovieWriterPNGWAV *writer_pngwav = nullptr;

void register_system_types() {
	OS::get_singleton()->benchmark_begin_measure("Systems", "Register Extensions");

	shader_types = memnew(ShaderTypes);

	ORBLIT_REGISTER_CLASS(TextSystemManager);
	ORBLIT_REGISTER_ABSTRACT_CLASS(TextSystem);
	ORBLIT_REGISTER_CLASS(TextSystemExtension);
	ORBLIT_REGISTER_CLASS(TextSystemDummy);

	ORBLIT_REGISTER_NATIVE_STRUCT(Glyph, "int start = -1;int end = -1;uint8_t count = 0;uint8_t repeat = 1;uint16_t flags = 0;float x_off = 0.f;float y_off = 0.f;float advance = 0.f;RID font_rid;int font_size = 0;int32_t index = 0;int span_index = -1");
	ORBLIT_REGISTER_NATIVE_STRUCT(CaretInfo, "Rect2 leading_caret;Rect2 trailing_caret;TextSystem::Direction leading_direction;TextSystem::Direction trailing_direction");

	Engine::get_singleton()->add_singleton(Engine::Singleton("TextSystemManager", TextSystemManager::get_singleton(), "TextSystemManager"));

	OS::get_singleton()->set_has_server_feature_callback(has_server_feature_callback);

	ORBLIT_REGISTER_ABSTRACT_CLASS(AccessibilitySystem);
	ORBLIT_REGISTER_ABSTRACT_CLASS(DisplaySystem);
	ORBLIT_REGISTER_ABSTRACT_CLASS(RenderingSystem);

	ORBLIT_REGISTER_CLASS(AudioSystem);
	ORBLIT_REGISTER_NATIVE_STRUCT(AudioFrame, "float left;float right");

	ORBLIT_REGISTER_CLASS(NativeMenu);

	ORBLIT_REGISTER_CLASS(CameraSystem);

	ORBLIT_REGISTER_CLASS(AudioSample);
	ORBLIT_REGISTER_CLASS(AudioSamplePlayback);
	ORBLIT_REGISTER_VIRTUAL_CLASS(AudioEffect);
	ORBLIT_REGISTER_VIRTUAL_CLASS(AudioEffectInstance);
	ORBLIT_REGISTER_CLASS(AudioEffectEQ);
	ORBLIT_REGISTER_CLASS(AudioEffectFilter);
	ORBLIT_REGISTER_CLASS(AudioBusLayout);

	{
		//audio effects
		ORBLIT_REGISTER_CLASS(AudioEffectAmplify);

		ORBLIT_REGISTER_CLASS(AudioEffectReverb);

		ORBLIT_REGISTER_CLASS(AudioEffectLowPassFilter);
		ORBLIT_REGISTER_CLASS(AudioEffectHighPassFilter);
		ORBLIT_REGISTER_CLASS(AudioEffectBandPassFilter);
		ORBLIT_REGISTER_CLASS(AudioEffectNotchFilter);
		ORBLIT_REGISTER_CLASS(AudioEffectBandLimitFilter);
		ORBLIT_REGISTER_CLASS(AudioEffectLowShelfFilter);
		ORBLIT_REGISTER_CLASS(AudioEffectHighShelfFilter);

		ORBLIT_REGISTER_CLASS(AudioEffectEQ6);
		ORBLIT_REGISTER_CLASS(AudioEffectEQ10);
		ORBLIT_REGISTER_CLASS(AudioEffectEQ21);

		ORBLIT_REGISTER_CLASS(AudioEffectDistortion);

		ORBLIT_REGISTER_CLASS(AudioEffectStereoEnhance);

		ORBLIT_REGISTER_CLASS(AudioEffectPanner);
		ORBLIT_REGISTER_CLASS(AudioEffectChorus);
		ORBLIT_REGISTER_CLASS(AudioEffectDelay);
		ORBLIT_REGISTER_CLASS(AudioEffectCompressor);
		ORBLIT_REGISTER_CLASS(AudioEffectHardLimiter);
		ORBLIT_REGISTER_CLASS(AudioEffectPitchShift);
		ORBLIT_REGISTER_CLASS(AudioEffectPhaser);
		ORBLIT_REGISTER_CLASS(AudioEffectRecord);
		ORBLIT_REGISTER_CLASS(AudioEffectSpectrumAnalyzer);
		ORBLIT_REGISTER_ABSTRACT_CLASS(AudioEffectSpectrumAnalyzerInstance);

		ORBLIT_REGISTER_CLASS(AudioEffectCapture);
	}

	ORBLIT_REGISTER_ABSTRACT_CLASS(RenderData);
	ORBLIT_REGISTER_CLASS(RenderDataExtension);
	ORBLIT_REGISTER_ABSTRACT_CLASS(RenderSceneData);
	ORBLIT_REGISTER_CLASS(RenderSceneDataExtension);

	ORBLIT_REGISTER_CLASS(RenderSceneBuffersConfiguration);
	ORBLIT_REGISTER_ABSTRACT_CLASS(RenderSceneBuffers);
	ORBLIT_REGISTER_CLASS(RenderSceneBuffersExtension);

	ORBLIT_REGISTER_CLASS(ShaderIncludeDB);

#ifdef RD_ENABLED
	ORBLIT_REGISTER_ABSTRACT_CLASS(RenderingDevice);
	ORBLIT_REGISTER_CLASS(RDTextureFormat);
	ORBLIT_REGISTER_CLASS(RDTextureView);
	ORBLIT_REGISTER_CLASS(RDAttachmentFormat);
	ORBLIT_REGISTER_CLASS(RDFramebufferPass);
	ORBLIT_REGISTER_CLASS(RDSamplerState);
	ORBLIT_REGISTER_CLASS(RDVertexAttribute);
	ORBLIT_REGISTER_CLASS(RDUniform);
	ORBLIT_REGISTER_CLASS(RDPipelineRasterizationState);
	ORBLIT_REGISTER_CLASS(RDPipelineMultisampleState);
	ORBLIT_REGISTER_CLASS(RDPipelineDepthStencilState);
	ORBLIT_REGISTER_CLASS(RDPipelineColorBlendStateAttachment);
	ORBLIT_REGISTER_CLASS(RDPipelineColorBlendState);
	ORBLIT_REGISTER_CLASS(RDShaderSource);
	ORBLIT_REGISTER_CLASS(RDShaderSPIRV);
	ORBLIT_REGISTER_CLASS(RDShaderFile);
	ORBLIT_REGISTER_CLASS(RDPipelineSpecializationConstant);
	ORBLIT_REGISTER_CLASS(RDAccelerationStructureGeometry);
	ORBLIT_REGISTER_CLASS(RDAccelerationStructureInstance);
	ORBLIT_REGISTER_CLASS(RDPipelineShader);
	ORBLIT_REGISTER_CLASS(RDHitGroup);

	ORBLIT_REGISTER_CLASS(RenderDataRD);
	ORBLIT_REGISTER_CLASS(RenderSceneDataRD);

	ORBLIT_REGISTER_CLASS(RenderSceneBuffersRD);
	ORBLIT_REGISTER_CLASS(FramebufferCacheRD);
	ORBLIT_REGISTER_CLASS(UniformSetCacheRD);
#endif // RD_ENABLED

	ORBLIT_REGISTER_CLASS(CameraFeed);

	ORBLIT_REGISTER_VIRTUAL_CLASS(MovieWriter);

	SystemsDebugger::initialize();

#ifndef NAVIGATION_PLANAR_DISABLED
	ORBLIT_REGISTER_CLASS(NavigationSystem2DManager);
	Engine::get_singleton()->add_singleton(Engine::Singleton("NavigationSystem2DManager", NavigationSystem2DManager::get_singleton(), "NavigationSystem2DManager"));

	ORBLIT_REGISTER_ABSTRACT_CLASS(NavigationSystem2D);
	ORBLIT_REGISTER_CLASS(NavigationPathQueryParameters2D);
	ORBLIT_REGISTER_CLASS(NavigationPathQueryResult2D);

	GLOBAL_DEF(PropertyInfo(Variant::STRING, NavigationSystem2DManager::setting_property_name, PROPERTY_HINT_ENUM, "DEFAULT"), "DEFAULT");

	NavigationSystem2DManager::get_singleton()->register_system("Dummy", callable_mp_static(NavigationSystem2DManager::create_dummy_system_callback));
#endif // NAVIGATION_PLANAR_DISABLED

#ifndef PHYSICS_PLANAR_DISABLED
	// Physics 2D
	ORBLIT_REGISTER_CLASS(PhysicsSystem2DManager);
	Engine::get_singleton()->add_singleton(Engine::Singleton("PhysicsSystem2DManager", PhysicsSystem2DManager::get_singleton(), "PhysicsSystem2DManager"));

	ORBLIT_REGISTER_ABSTRACT_CLASS(PhysicsSystem2D);
	ORBLIT_REGISTER_VIRTUAL_CLASS(PhysicsSystem2DExtension);
	ORBLIT_REGISTER_ABSTRACT_CLASS(PhysicsDirectBodyState2D);
	ORBLIT_REGISTER_VIRTUAL_CLASS(PhysicsDirectBodyState2DExtension);
	ORBLIT_REGISTER_ABSTRACT_CLASS(PhysicsDirectSpaceState2D);
	ORBLIT_REGISTER_VIRTUAL_CLASS(PhysicsDirectSpaceState2DExtension);

	ORBLIT_REGISTER_NATIVE_STRUCT(PhysicsSystem2DExtensionRayResult, "Vector2 position;Vector2 normal;RID rid;ObjectID collider_id;Object *collider;int shape");
	ORBLIT_REGISTER_NATIVE_STRUCT(PhysicsSystem2DExtensionShapeResult, "RID rid;ObjectID collider_id;Object *collider;int shape");
	ORBLIT_REGISTER_NATIVE_STRUCT(PhysicsSystem2DExtensionShapeRestInfo, "Vector2 point;Vector2 normal;RID rid;ObjectID collider_id;int shape;Vector2 linear_velocity");
	ORBLIT_REGISTER_NATIVE_STRUCT(PhysicsSystem2DExtensionMotionResult, "Vector2 travel;Vector2 remainder;Vector2 collision_point;Vector2 collision_normal;Vector2 collider_velocity;real_t collision_depth;real_t collision_safe_fraction;real_t collision_unsafe_fraction;int collision_local_shape;ObjectID collider_id;RID collider;int collider_shape");

	ORBLIT_REGISTER_CLASS(PhysicsRayQueryParameters2D);
	ORBLIT_REGISTER_CLASS(PhysicsIntersectRayResult2D);
	ORBLIT_REGISTER_CLASS(PhysicsPointQueryParameters2D);
	ORBLIT_REGISTER_CLASS(PhysicsIntersectPointResult2D);
	ORBLIT_REGISTER_CLASS(PhysicsShapeQueryParameters2D);
	ORBLIT_REGISTER_CLASS(PhysicsIntersectShapeResult2D);
	ORBLIT_REGISTER_CLASS(PhysicsCastMotionResult2D);
	ORBLIT_REGISTER_CLASS(PhysicsCollideShapeResult2D);
	ORBLIT_REGISTER_CLASS(PhysicsGetRestInfoResult2D);
	ORBLIT_REGISTER_CLASS(PhysicsTestMotionParameters2D);
	ORBLIT_REGISTER_CLASS(PhysicsTestMotionResult2D);

	GLOBAL_DEF(PropertyInfo(Variant::STRING, PhysicsSystem2DManager::setting_property_name, PROPERTY_HINT_ENUM, "DEFAULT"), "DEFAULT");

	PhysicsSystem2DManager::get_singleton()->register_system("Dummy", callable_mp_static(_create_dummy_physics_system_2d));
#endif // PHYSICS_PLANAR_DISABLED

#ifndef NAVIGATION_SPATIAL_DISABLED
	ORBLIT_REGISTER_CLASS(NavigationSystem3DManager);
	Engine::get_singleton()->add_singleton(Engine::Singleton("NavigationSystem3DManager", NavigationSystem3DManager::get_singleton(), "NavigationSystem3DManager"));

	ORBLIT_REGISTER_ABSTRACT_CLASS(NavigationSystem3D);
	ORBLIT_REGISTER_CLASS(NavigationPathQueryParameters3D);
	ORBLIT_REGISTER_CLASS(NavigationPathQueryResult3D);

	GLOBAL_DEF(PropertyInfo(Variant::STRING, NavigationSystem3DManager::setting_property_name, PROPERTY_HINT_ENUM, "DEFAULT"), "DEFAULT");

	NavigationSystem3DManager::get_singleton()->register_system("Dummy", callable_mp_static(NavigationSystem3DManager::create_dummy_system_callback));
#endif // NAVIGATION_SPATIAL_DISABLED

#ifndef PHYSICS_SPATIAL_DISABLED
	// Physics 3D
	ORBLIT_REGISTER_CLASS(PhysicsSystem3DManager);
	Engine::get_singleton()->add_singleton(Engine::Singleton("PhysicsSystem3DManager", PhysicsSystem3DManager::get_singleton(), "PhysicsSystem3DManager"));

	ORBLIT_REGISTER_ABSTRACT_CLASS(PhysicsSystem3D);
	ORBLIT_REGISTER_VIRTUAL_CLASS(PhysicsSystem3DExtension);
	ORBLIT_REGISTER_ABSTRACT_CLASS(PhysicsDirectBodyState3D);
	ORBLIT_REGISTER_VIRTUAL_CLASS(PhysicsDirectBodyState3DExtension);
	ORBLIT_REGISTER_ABSTRACT_CLASS(PhysicsDirectSpaceState3D);
	ORBLIT_REGISTER_VIRTUAL_CLASS(PhysicsDirectSpaceState3DExtension)
	ORBLIT_REGISTER_VIRTUAL_CLASS(PhysicsSystem3DRenderingSystemHandler)

	ORBLIT_REGISTER_NATIVE_STRUCT(PhysicsSystem3DExtensionRayResult, "Vector3 position;Vector3 normal;RID rid;ObjectID collider_id;Object *collider;int shape;int face_index");
	ORBLIT_REGISTER_NATIVE_STRUCT(PhysicsSystem3DExtensionShapeResult, "RID rid;ObjectID collider_id;Object *collider;int shape");
	ORBLIT_REGISTER_NATIVE_STRUCT(PhysicsSystem3DExtensionShapeRestInfo, "Vector3 point;Vector3 normal;RID rid;ObjectID collider_id;int shape;Vector3 linear_velocity");
	ORBLIT_REGISTER_NATIVE_STRUCT(PhysicsSystem3DExtensionMotionCollision, "Vector3 position;Vector3 normal;Vector3 collider_velocity;Vector3 collider_angular_velocity;real_t depth;int local_shape;ObjectID collider_id;RID collider;int collider_shape");
	ORBLIT_REGISTER_NATIVE_STRUCT(PhysicsSystem3DExtensionMotionResult, "Vector3 travel;Vector3 remainder;real_t collision_depth;real_t collision_safe_fraction;real_t collision_unsafe_fraction;PhysicsSystem3DExtensionMotionCollision collisions[32];int collision_count");

	ORBLIT_REGISTER_CLASS(PhysicsRayQueryParameters3D);
	ORBLIT_REGISTER_CLASS(PhysicsIntersectRayResult3D);
	ORBLIT_REGISTER_CLASS(PhysicsPointQueryParameters3D);
	ORBLIT_REGISTER_CLASS(PhysicsIntersectPointResult3D);
	ORBLIT_REGISTER_CLASS(PhysicsShapeQueryParameters3D);
	ORBLIT_REGISTER_CLASS(PhysicsIntersectShapeResult3D);
	ORBLIT_REGISTER_CLASS(PhysicsCastMotionResult3D);
	ORBLIT_REGISTER_CLASS(PhysicsCollideShapeResult3D);
	ORBLIT_REGISTER_CLASS(PhysicsGetRestInfoResult3D);
	ORBLIT_REGISTER_CLASS(PhysicsTestMotionParameters3D);
	ORBLIT_REGISTER_CLASS(PhysicsTestMotionResult3D);

	GLOBAL_DEF(PropertyInfo(Variant::STRING, PhysicsSystem3DManager::setting_property_name, PROPERTY_HINT_ENUM, "DEFAULT"), "DEFAULT");

	PhysicsSystem3DManager::get_singleton()->register_system("Dummy", callable_mp_static(_create_dummy_physics_system_3d));
#endif // PHYSICS_SPATIAL_DISABLED

#ifndef XR_DISABLED
	ORBLIT_REGISTER_ABSTRACT_CLASS(XRInterface);
	ORBLIT_REGISTER_ABSTRACT_CLASS(XRTracker);
	ORBLIT_REGISTER_CLASS(XRVRS);
	ORBLIT_REGISTER_CLASS(XRPositionalTracker);
	ORBLIT_REGISTER_CLASS(XRBodyTracker);
	ORBLIT_REGISTER_CLASS(XRControllerTracker);
	ORBLIT_REGISTER_CLASS(XRFaceTracker);
	ORBLIT_REGISTER_CLASS(XRHandTracker);
	ORBLIT_REGISTER_CLASS(XRInterfaceExtension); // can't register this as virtual because we need a creation function for our extensions.
	ORBLIT_REGISTER_CLASS(XRPose);
	ORBLIT_REGISTER_CLASS(XRSystem);
#endif // XR_DISABLED

	if constexpr (ORBLIT_IS_CLASS_ENABLED(MovieWriterPNGWAV)) {
		writer_pngwav = memnew(MovieWriterPNGWAV);
		MovieWriter::add_writer(writer_pngwav);
	}

	OS::get_singleton()->benchmark_end_measure("Systems", "Register Extensions");
}

void unregister_system_types() {
	OS::get_singleton()->benchmark_begin_measure("Systems", "Unregister Extensions");

	SystemsDebugger::deinitialize();
	memdelete(shader_types);
	if constexpr (ORBLIT_IS_CLASS_ENABLED(MovieWriterPNGWAV)) {
		memdelete(writer_pngwav);
	}

	OS::get_singleton()->benchmark_end_measure("Systems", "Unregister Extensions");
}

void register_system_singletons() {
	OS::get_singleton()->benchmark_begin_measure("Systems", "Register Singletons");

	Engine::get_singleton()->add_singleton(Engine::Singleton("AccessibilitySystem", AccessibilitySystem::get_singleton(), "AccessibilitySystem"));
	Engine::get_singleton()->add_singleton(Engine::Singleton("AudioSystem", AudioSystem::get_singleton(), "AudioSystem"));
	Engine::get_singleton()->add_singleton(Engine::Singleton("CameraSystem", CameraSystem::get_singleton(), "CameraSystem"));
	Engine::get_singleton()->add_singleton(Engine::Singleton("DisplaySystem", DisplaySystem::get_singleton(), "DisplaySystem"));
	Engine::get_singleton()->add_singleton(Engine::Singleton("NativeMenu", NativeMenu::get_singleton(), "NativeMenu"));
	Engine::get_singleton()->add_singleton(Engine::Singleton("RenderingSystem", RenderingSystem::get_singleton(), "RenderingSystem"));
#ifndef NAVIGATION_PLANAR_DISABLED
	Engine::get_singleton()->add_singleton(Engine::Singleton("NavigationSystem2D", NavigationSystem2D::get_singleton(), "NavigationSystem2D"));
#endif // NAVIGATION_PLANAR_DISABLED
#ifndef NAVIGATION_SPATIAL_DISABLED
	Engine::get_singleton()->add_singleton(Engine::Singleton("NavigationSystem3D", NavigationSystem3D::get_singleton(), "NavigationSystem3D"));
#endif // NAVIGATION_SPATIAL_DISABLED
#ifndef PHYSICS_PLANAR_DISABLED
	Engine::get_singleton()->add_singleton(Engine::Singleton("PhysicsSystem2D", PhysicsSystem2D::get_singleton(), "PhysicsSystem2D"));
#endif // PHYSICS_PLANAR_DISABLED
#ifndef PHYSICS_SPATIAL_DISABLED
	Engine::get_singleton()->add_singleton(Engine::Singleton("PhysicsSystem3D", PhysicsSystem3D::get_singleton(), "PhysicsSystem3D"));
#endif // PHYSICS_SPATIAL_DISABLED
#ifndef XR_DISABLED
	Engine::get_singleton()->add_singleton(Engine::Singleton("XRSystem", XRSystem::get_singleton(), "XRSystem"));
#endif // XR_DISABLED

	OS::get_singleton()->benchmark_end_measure("Systems", "Register Singletons");
}
