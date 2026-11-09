/*************************************************************************/
/*  OrblitXRGame.kt                                                      */
/*************************************************************************/

package org.orblitengine.editor

import androidx.annotation.CallSuper
import org.orblitengine.orblit.OrblitLib
import org.orblitengine.orblit.xr.XRMode
import org.orblitengine.openxr.vendors.utils.HYBRID_APP_SPATIAL_FEATURE
import org.orblitengine.openxr.vendors.utils.isHybridAppEnabled

/**
 * Provide support for running XR apps / games from the editor window.
 */
open class OrblitXRGame: BaseOrblitGame() {

	override fun overrideOrientationRequest() = true

	override fun getCommandLine(): MutableList<String> {
		val updatedArgs = super.getCommandLine()
		if (!updatedArgs.contains(XRMode.OPENXR.cmdLineArg)) {
			updatedArgs.add(XRMode.OPENXR.cmdLineArg)
		}
		if (!updatedArgs.contains(XR_MODE_ARG)) {
			updatedArgs.add(XR_MODE_ARG)
			updatedArgs.add("on")
		}
		return updatedArgs
	}

	override fun getEditorWindowInfo() = XR_RUN_GAME_INFO

	override fun getOrblitAppLayout() = R.layout.xr_game_layout

	override fun getProjectPermissionsToEnable(): MutableList<String> {
		val permissionsToEnable = super.getProjectPermissionsToEnable()

		val xrRuntimePermission = getXRRuntimePermissions()
		if (xrRuntimePermission.isNotEmpty() && OrblitLib.getGlobal("xr/openxr/enabled").toBoolean()) {
			// We only request permissions when the `automatically_request_runtime_permissions`
			// project setting is enabled.
			// If the project setting is not defined, we fall-back to the default behavior which is
			// to automatically request permissions.
			val automaticallyRequestPermissionsSetting = OrblitLib.getGlobal("xr/openxr/extensions/automatically_request_runtime_permissions")
			val automaticPermissionsRequestEnabled = automaticallyRequestPermissionsSetting.isNullOrEmpty() ||
				automaticallyRequestPermissionsSetting.toBoolean()
			if (automaticPermissionsRequestEnabled) {
				permissionsToEnable.addAll(xrRuntimePermission)
			}
		}

		return permissionsToEnable
	}

	@CallSuper
	override fun supportsFeature(featureTag: String): Boolean {
		if (HYBRID_APP_SPATIAL_FEATURE == featureTag) {
			// Check if hybrid is enabled
			return isHybridAppEnabled()
		}

		return super.supportsFeature(featureTag)
	}
}
