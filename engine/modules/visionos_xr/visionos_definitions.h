/**************************************************************************/
/*  visionos_definitions.h                                                */

#pragma once

#ifdef VISIONOS_ENABLED

#include "backends/metal/metal_objects_shared.h"
#include "backends/metal/rendering_context_driver_metal.h"
#include "backends/metal/rendering_device_driver_metal.h"
#include "core/containers/safe_refcount.h"
#include "core/os/mutex.h"
#include "systems/rendering/renderer_compositor.h"
#include "systems/rendering/rendering_device.h"
#include "systems/rendering/rendering_system.h"
#include "systems/xr/xr_controller_tracker.h"
#include "systems/xr/xr_hand_tracker.h"
#include "systems/xr/xr_interface.h"
#include "systems/xr/xr_positional_tracker.h"
#include "systems/xr/xr_vrs.h"

#ifdef __OBJC__
// When compiling as Objective-C++, include the actual headers
#import <ARKit/ARKit.h>
#else // __OBJC__
// When compiling as C++, use forward declarations for ARKit and CompositorServices types (opaque pointers)
typedef struct ar_world_tracking_provider *ar_world_tracking_provider_t;
typedef struct ar_hand_tracking_provider *ar_hand_tracking_provider_t;
typedef struct ar_accessory_tracking_provider *ar_accessory_tracking_provider_t;
typedef struct ar_data_providers *ar_data_providers_t;
typedef struct ar_data_provider *ar_data_provider_t;
;
typedef struct ar_authorization_results *ar_authorization_results_t;
typedef struct ar_session *ar_session_t;
typedef struct ar_device_anchor *ar_device_anchor_t;
typedef struct ar_hand_anchor *ar_hand_anchor_t;
typedef struct ar_accessories *ar_accessories_t;
typedef struct ar_accessory_anchor *ar_accessory_anchor_t;
#endif // __OBJC__

// Equivalent to ARKit's `ar_authorization_status`
enum class VisionOSAuthorizationStatus {
	NOT_DETERMINED,
	ALLOWED,
	DENIED,
};

// Trackers used by both VisionOSControllerTracking and VisionOSSpatialEventTracking.
struct VisionOSSharedController {
	Ref<XRControllerTracker> tracker;

	enum class Source {
		None,
		SpatialEvent,
		Controller
	};

	// Spatial Events override controllers, when active.
	Source source = Source::None;
};

#endif // VISIONOS_ENABLED
