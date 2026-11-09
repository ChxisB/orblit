/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "action_map/openxr_action.h"
#include "action_map/openxr_action_map.h"
#include "action_map/openxr_action_set.h"
#include "action_map/openxr_haptic_feedback.h"
#include "action_map/openxr_interaction_profile.h"
#include "action_map/openxr_interaction_profile_metadata.h"
#include "extensions/openxr_android_thread_settings_extension.h"
#include "extensions/openxr_composition_layer_depth_extension.h"
#include "extensions/openxr_composition_layer_extension.h"
#include "extensions/openxr_debug_utils_extension.h"
#include "extensions/openxr_dpad_binding_extension.h"
#include "extensions/openxr_eye_gaze_interaction.h"
#include "extensions/openxr_fb_display_refresh_rate_extension.h"
#include "extensions/openxr_foveated_inset_extension.h"
#include "extensions/openxr_frame_synthesis_extension.h"
#include "extensions/openxr_future_extension.h"
#include "extensions/openxr_hand_interaction_extension.h"
#include "extensions/openxr_hand_tracking_extension.h"
#include "extensions/openxr_htc_controller_extension.h"
#include "extensions/openxr_htc_vive_tracker_extension.h"
#include "extensions/openxr_huawei_controller_extension.h"
#include "extensions/openxr_khr_generic_controller_extension.h"
#include "extensions/openxr_local_floor_extension.h"
#include "extensions/openxr_meta_controller_extension.h"
#include "extensions/openxr_ml2_controller_extension.h"
#include "extensions/openxr_mxink_extension.h"
#include "extensions/openxr_palm_pose_extension.h"
#include "extensions/openxr_performance_settings_extension.h"
#include "extensions/openxr_pico_controller_extension.h"
#include "extensions/openxr_user_presence_extension.h"
#include "extensions/openxr_valve_analog_threshold_extension.h"
#include "extensions/openxr_valve_controller_extension.h"
#include "extensions/openxr_visibility_mask_extension.h"
#include "extensions/openxr_wmr_controller_extension.h"
#include "extensions/spatial_container/openxr_spatial_container_extension.h"
#include "extensions/spatial_container/openxr_spatial_container_self_rendering_extension.h"
#include "extensions/spatial_container/openxr_spatial_container_state.h"
#include "extensions/spatial_entities/openxr_spatial_anchor.h"
#include "extensions/spatial_entities/openxr_spatial_entity_extension.h"
#include "extensions/spatial_entities/openxr_spatial_marker_tracking.h"
#include "extensions/spatial_entities/openxr_spatial_plane_tracking.h"
#include "openxr_api_extension.h"
#include "openxr_interface.h"

#include "core/config/engine.h"
#include "core/config/project_settings.h"
#include "core/object/class_registry.h"
#include "core/os/os.h"
#include "scene/openxr_composition_layer.h"
#include "scene/openxr_composition_layer_cylinder.h"
#include "scene/openxr_composition_layer_equirect.h"
#include "scene/openxr_composition_layer_quad.h"
#include "scene/openxr_foveated_inset_viewport.h"
#include "scene/openxr_visibility_mask.h"

#include "modules/modules_enabled.gen.h"

#ifdef MODULE_GLTF_ENABLED
#include "extensions/openxr_render_model_extension.h"

#include "scene/openxr_render_model.h"
#include "scene/openxr_render_model_manager.h"
#endif

#ifdef TOOLS_ENABLED
#include "editor/openxr_editor_plugin.h"
#endif

#ifdef ANDROID_ENABLED
#include "extensions/platform/openxr_android_extension.h"
#endif

#ifdef TOOLS_ENABLED
#include "editor/openxr_binding_modifier_editor.h"
#include "editor/openxr_interaction_profile_editor.h"
//
#include "editor/editor_node.h"
#endif

static OpenXRAPI *openxr_api = nullptr;
static OpenXRInteractionProfileMetadata *openxr_interaction_profile_metadata = nullptr;
static Ref<OpenXRInterface> openxr_interface;

struct ExtensionSingleton {
	String class_name;
	OpenXRExtensionWrapper *extension;
	bool registered_with_openxr;
	bool registered_as_singleton;
};

static Vector<ExtensionSingleton> extensions_singletons;
static inline void _register_extension(OpenXRExtensionWrapper *p_extension, bool p_register_with_openxr = true, bool p_register_as_singleton = false) {
	String class_name = p_extension->get_class();
	if (p_register_with_openxr) {
		OpenXRAPI::register_extension_wrapper(p_extension);
	}
	if (p_register_as_singleton) {
		Engine::get_singleton()->add_singleton(Engine::Singleton(class_name, p_extension));
	}
	extensions_singletons.push_back({ class_name, p_extension, p_register_with_openxr, p_register_as_singleton });
}

#ifdef TOOLS_ENABLED
static void _editor_init() {
	if (OpenXRAPI::openxr_is_enabled(false)) {
		if (openxr_interaction_profile_metadata == nullptr) {
			// If we didn't initialize our actionmap metadata at startup, we initialize it now.
			openxr_interaction_profile_metadata = memnew(OpenXRInteractionProfileMetadata);
			ERR_FAIL_NULL(openxr_interaction_profile_metadata);
		}
	}

	OpenXREditorPlugin *openxr_plugin = memnew(OpenXREditorPlugin());
	EditorNode::get_singleton()->add_editor_plugin(openxr_plugin);
}
#endif

void initialize_openxr_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_CORE) {
		ORBLIT_REGISTER_VIRTUAL_CLASS(OpenXRExtensionWrapper);
		ORBLIT_REGISTER_CLASS(OpenXRInteractionProfileMetadata);
		ORBLIT_REGISTER_ABSTRACT_CLASS(OpenXRFutureResult); // Declared abstract, should never be instantiated by a user (Q or should this be internal?)
		ORBLIT_REGISTER_CLASS(OpenXRFrameSynthesisExtension);
		ORBLIT_REGISTER_CLASS(OpenXRFutureExtension);
		ORBLIT_REGISTER_CLASS(OpenXRAPIExtension);
#ifdef MODULE_GLTF_ENABLED
		ORBLIT_REGISTER_CLASS(OpenXRRenderModelExtension);
#endif
		ORBLIT_REGISTER_CLASS(OpenXRAndroidThreadSettingsExtension);

		// Note, we're not registering all wrapper classes here, there is no point in exposing them
		// if there isn't specific logic to expose.
	}

	if (p_level == MODULE_INITIALIZATION_LEVEL_SYSTEMS) {
		if (OpenXRAPI::openxr_is_enabled(false)) {
			// Always register our extension wrappers even if we don't initialize OpenXR.
			// Some of these wrappers will add functionality to our editor.
#ifdef ANDROID_ENABLED
			_register_extension(memnew(OpenXRAndroidExtension));
#endif

			// register our other extensions
			_register_extension(memnew(OpenXRPalmPoseExtension));
			_register_extension(memnew(OpenXRLocalFloorExtension));
			_register_extension(memnew(OpenXRPicoControllerExtension));
			_register_extension(memnew(OpenXRCompositionLayerDepthExtension));
			_register_extension(memnew(OpenXRCompositionLayerExtension));
			_register_extension(memnew(OpenXRHTCControllerExtension));
			_register_extension(memnew(OpenXRHTCViveTrackerExtension));
			_register_extension(memnew(OpenXRHuaweiControllerExtension));
			_register_extension(memnew(OpenXRDisplayRefreshRateExtension));
			_register_extension(memnew(OpenXRWMRControllerExtension));
			_register_extension(memnew(OpenXRML2ControllerExtension));
			_register_extension(memnew(OpenXRMetaControllerExtension));
			_register_extension(memnew(OpenXREyeGazeInteractionExtension));
			_register_extension(memnew(OpenXRHandInteractionExtension));
			_register_extension(memnew(OpenXRMxInkExtension));
			_register_extension(memnew(OpenXRVisibilityMaskExtension));
			_register_extension(memnew(OpenXRPerformanceSettingsExtension));
			_register_extension(memnew(OpenXRValveControllerExtension));
			_register_extension(memnew(OpenXRKHRGenericController));
			_register_extension(memnew(OpenXRFoveatedInsetExtension));

			// Futures extension has to be registered as a singleton so extensions can access it.
			_register_extension(memnew(OpenXRFutureExtension), true, true);

			// Register render model extension as a singleton.
#ifdef MODULE_GLTF_ENABLED
			_register_extension(memnew(OpenXRRenderModelExtension), true, true);
#endif

			// Register spatial entity extensions
			_register_extension(memnew(OpenXRSpatialEntityExtension), true, true);
			_register_extension(memnew(OpenXRSpatialAnchorCapability), true, true);
			_register_extension(memnew(OpenXRSpatialPlaneTrackingCapability), true, true);
			_register_extension(memnew(OpenXRSpatialMarkerTrackingCapability), true, true);

			// Register frame synthesis extension as a singleton.
			_register_extension(memnew(OpenXRFrameSynthesisExtension), true, true);

			// Register android thread settings extension as a singleton.
			_register_extension(memnew(OpenXRAndroidThreadSettingsExtension), true, true);

			// Register user presence extension as a singleton
			_register_extension(memnew(OpenXRUserPresenceExtension), true, true);

			// register gated extensions
			_register_extension(memnew(OpenXRDebugUtilsExtension), int(GLOBAL_GET("xr/openxr/extensions/debug_utils")) > 0);
			_register_extension(memnew(OpenXRHandTrackingExtension), GLOBAL_GET("xr/openxr/extensions/hand_tracking"));

			// Spatial container extensions
			bool spatial_container_enabled = GLOBAL_GET("xr/openxr/extensions/spatial_container/enabled");
			_register_extension(memnew(OpenXRSpatialContainerExtension), spatial_container_enabled, true);
			// For now we gate the self rendering extension under the same preference as the spatial_container extension.
			// TODO: When we add support for system rendering, gate self rendering under a different project setting.
			_register_extension(memnew(OpenXRSpatialContainerSelfRenderingExtension), spatial_container_enabled, true);

			// register gated binding modifiers
			_register_extension(memnew(OpenXRValveAnalogThresholdExtension), GLOBAL_GET("xr/openxr/binding_modifiers/analog_threshold"));
			_register_extension(memnew(OpenXRDPadBindingExtension), GLOBAL_GET("xr/openxr/binding_modifiers/dpad_binding"));
		}

		if (OpenXRAPI::openxr_is_enabled()) {
			openxr_interaction_profile_metadata = memnew(OpenXRInteractionProfileMetadata);
			ERR_FAIL_NULL(openxr_interaction_profile_metadata);
			openxr_api = memnew(OpenXRAPI);
			ERR_FAIL_NULL(openxr_api);

			if (!openxr_api->initialize(OS::get_singleton()->get_current_rendering_driver_name())) {
				const char *init_error_message =
						"OpenXR was requested but failed to start.\n"
						"HMD was not detected or a required feature was not supported.\n\n"
#ifdef TOOLS_ENABLED
						// Editor only message - this is useful for app developer, but not user
						"Check logged errors in debugger for more details.\n\n"
#endif
#ifdef WINDOWS_ENABLED
						"When using Windows Mixed Reality, note that WMR only has DirectX support. Make sure SteamVR is your default OpenXR runtime.\n\n"
#endif
						"Orblit will start in normal mode.\n";

				WARN_PRINT(init_error_message);

				bool init_show_startup_alert = GLOBAL_GET("xr/openxr/startup_alert");
				if (init_show_startup_alert) {
					OS::get_singleton()->alert(init_error_message);
				}

				memdelete(openxr_api);
				openxr_api = nullptr;
				return;
			}
		}
	}

	if (p_level == MODULE_INITIALIZATION_LEVEL_SCENE) {
		ORBLIT_REGISTER_CLASS(OpenXRInterface);

		ORBLIT_REGISTER_CLASS(OpenXRAction);
		ORBLIT_REGISTER_CLASS(OpenXRActionSet);
		ORBLIT_REGISTER_CLASS(OpenXRActionMap);
		ORBLIT_REGISTER_CLASS(OpenXRIPBinding);
		ORBLIT_REGISTER_CLASS(OpenXRInteractionProfile);

		ORBLIT_REGISTER_ABSTRACT_CLASS(OpenXRBindingModifier);
		ORBLIT_REGISTER_VIRTUAL_CLASS(OpenXRIPBindingModifier);
		ORBLIT_REGISTER_VIRTUAL_CLASS(OpenXRActionBindingModifier);
		ORBLIT_REGISTER_CLASS(OpenXRAnalogThresholdModifier);
		ORBLIT_REGISTER_CLASS(OpenXRDpadBindingModifier);

		ORBLIT_REGISTER_ABSTRACT_CLASS(OpenXRHapticBase);
		ORBLIT_REGISTER_CLASS(OpenXRHapticVibration);

		ORBLIT_REGISTER_ABSTRACT_CLASS(OpenXRCompositionLayer);
		ORBLIT_REGISTER_CLASS(OpenXRCompositionLayerEquirect);
		ORBLIT_REGISTER_CLASS(OpenXRCompositionLayerCylinder);
		ORBLIT_REGISTER_CLASS(OpenXRCompositionLayerQuad);

		ORBLIT_REGISTER_CLASS(OpenXRVisibilityMask);
#ifdef MODULE_GLTF_ENABLED
		ORBLIT_REGISTER_CLASS(OpenXRRenderModel);
		ORBLIT_REGISTER_CLASS(OpenXRRenderModelManager);
#endif
		ORBLIT_REGISTER_CLASS(OpenXRFoveatedInsetViewport);

		ORBLIT_REGISTER_CLASS(OpenXRSpatialContainerExtension);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialContainerSelfRenderingExtension);
		ORBLIT_REGISTER_VIRTUAL_CLASS(OpenXRSpatialContainerState);

		ORBLIT_REGISTER_CLASS(OpenXRSpatialEntityExtension);
		ORBLIT_REGISTER_VIRTUAL_CLASS(OpenXRSpatialEntityTracker);
		ORBLIT_REGISTER_CLASS(OpenXRAnchorTracker);
		ORBLIT_REGISTER_CLASS(OpenXRPlaneTracker);
		ORBLIT_REGISTER_CLASS(OpenXRMarkerTracker);

		ORBLIT_REGISTER_VIRTUAL_CLASS(OpenXRStructureBase);

		ORBLIT_REGISTER_VIRTUAL_CLASS(OpenXRSpatialCapabilityConfigurationBaseHeader);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialCapabilityConfigurationAnchor);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialCapabilityConfigurationQrCode);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialCapabilityConfigurationMicroQrCode);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialCapabilityConfigurationAruco);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialCapabilityConfigurationAprilTag);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialContextPersistenceConfig);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialCapabilityConfigurationPlaneTracking);
		ORBLIT_REGISTER_VIRTUAL_CLASS(OpenXRSpatialComponentData);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialComponentBounded2DList);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialComponentBounded3DList);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialComponentParentList);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialComponentMesh2DList);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialComponentMesh3DList);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialComponentPlaneAlignmentList);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialComponentPolygon2DList);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialComponentPlaneSemanticLabelList);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialComponentMarkerList);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialQueryResultData);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialComponentAnchorList);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialComponentPersistenceList);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialAnchorCapability);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialPlaneTrackingCapability);
		ORBLIT_REGISTER_CLASS(OpenXRSpatialMarkerTrackingCapability);

		XRSystem *xr_system = XRSystem::get_singleton();
		if (xr_system) {
			openxr_interface.instantiate();
			xr_system->add_interface(openxr_interface);

			if (openxr_interface->initialize_on_startup()) {
				openxr_interface->initialize();
			}
		}
	}

#ifdef TOOLS_ENABLED
	if (p_level == MODULE_INITIALIZATION_LEVEL_EDITOR) {
		ORBLIT_REGISTER_ABSTRACT_CLASS(OpenXRInteractionProfileEditorBase);
		ORBLIT_REGISTER_VIRTUAL_CLASS(OpenXRInteractionProfileEditor);
		ORBLIT_REGISTER_VIRTUAL_CLASS(OpenXRBindingModifierEditor);

		EditorNode::add_init_callback(_editor_init);
	}
#endif
}

void uninitialize_openxr_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	for (const ExtensionSingleton &extension_singleton : extensions_singletons) {
		if (extension_singleton.registered_as_singleton) {
			Engine::get_singleton()->remove_singleton(extension_singleton.class_name);
		}
		if (!extension_singleton.registered_with_openxr) {
			// OpenXRAPI handles the cleanup for registered extensions.
			memdelete(extension_singleton.extension);
		}
	}
	extensions_singletons.clear();

	if (openxr_interface.is_valid()) {
		// uninitialize just in case
		if (openxr_interface->is_initialized()) {
			openxr_interface->uninitialize();
		}

		// unregister our interface from the XR server
		XRSystem *xr_system = XRSystem::get_singleton();
		if (xr_system) {
			if (xr_system->get_primary_interface() == openxr_interface) {
				xr_system->set_primary_interface(Ref<XRInterface>());
			}
			xr_system->remove_interface(openxr_interface);
		}

		// and release
		openxr_interface.unref();
	}

	if (openxr_api) {
		openxr_api->finish();

		memdelete(openxr_api);
		openxr_api = nullptr;
	}

	if (openxr_interaction_profile_metadata) {
		memdelete(openxr_interaction_profile_metadata);
		openxr_interaction_profile_metadata = nullptr;
	}

	// cleanup our extension wrappers
	OpenXRAPI::cleanup_extension_wrappers();
}
