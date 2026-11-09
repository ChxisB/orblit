/**************************************************************************/
/*  OrblitEditor.kt                                                       */

package org.orblitengine.editor

/**
 * Primary window of the Orblit Editor.
 *
 * This is the implementation of the editor used when running on HorizonOS devices.
 */
open class OrblitEditor : BaseOrblitEditor() {

	override fun getExcludedPermissions(): MutableSet<String> {
		val excludedPermissions = super.getExcludedPermissions().apply {
			// The AVATAR_CAMERA and HEADSET_CAMERA permissions are requested when `CameraFeed.feed_is_active`
			// is enabled.
			add("horizonos.permission.AVATAR_CAMERA")
			add("horizonos.permission.HEADSET_CAMERA")
		}
		return excludedPermissions
	}

	override fun getXRRuntimePermissions(): MutableSet<String> {
		val xrRuntimePermissions = super.getXRRuntimePermissions()
		xrRuntimePermissions.add("com.oculus.permission.USE_SCENE")
		xrRuntimePermissions.add("horizonos.permission.USE_SCENE")
		return xrRuntimePermissions
	}
}
