/**************************************************************************/
/*  BaseOrblitGame.kt                                                     */

package org.orblitengine.editor

import android.Manifest
import android.util.Log
import androidx.annotation.CallSuper
import org.orblitengine.orblit.Orblit
import org.orblitengine.orblit.OrblitLib
import org.orblitengine.orblit.editor.utils.GameMenuUtils
import org.orblitengine.orblit.utils.PermissionsUtil
import org.orblitengine.orblit.utils.ProcessPhoenix
import org.godotengine.openxr.vendors.utils.*

/**
 * Base class for the Orblit play windows.
 */
abstract class BaseOrblitGame: OrblitEditor() {
	companion object {
		private val TAG = BaseOrblitGame::class.java.simpleName
	}

	override fun overrideVolumeButtons() = java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("input_devices/pointing/android/override_volume_buttons"))

	override fun enableRightClickEmulation() = java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("input_devices/pointing/android/enable_long_press_as_right_click"))

	override fun enablePanAndScaleGestures() = java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("input_devices/pointing/android/enable_pan_and_scale_gestures"))

	override fun disableScrollDeadzone() = java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("input_devices/pointing/android/disable_scroll_deadzone"))

	override fun onOrblitSetupCompleted() {
		super.onOrblitSetupCompleted()
		Log.v(TAG, "OnOrblitSetupCompleted")

		// Check if we should be running in XR instead (if available) as it's possible we were
		// launched from the project manager which doesn't have that information.
		val launchingArgs = retrieveCommandLineParamsFromLaunchIntent()
		val editorWindowInfo = retrieveEditorWindowInfo(launchingArgs, getEditorGameEmbedMode())
		if (editorWindowInfo != getEditorWindowInfo()) {
			val relaunchIntent = getNewOrblitInstanceIntent(editorWindowInfo, launchingArgs)
			relaunchIntent.putExtra(EXTRA_NEW_LAUNCH, true)
				.putExtra(EditorMessageDispatcher.EXTRA_MSG_DISPATCHER_PAYLOAD, intent.getBundleExtra(EditorMessageDispatcher.EXTRA_MSG_DISPATCHER_PAYLOAD))

			Log.d(TAG, "Relaunching XR project using ${editorWindowInfo.windowClassName} with parameters ${launchingArgs.contentToString()}")
			Orblit.getInstance(applicationContext).destroyAndKillProcess {
				ProcessPhoenix.triggerRebirth(this, relaunchIntent)
			}
			return
		}

		// Request project runtime permissions if necessary.
		val permissionsToEnable = getProjectPermissionsToEnable()
		if (permissionsToEnable.isNotEmpty()) {
			PermissionsUtil.requestPermissions(this, permissionsToEnable)
		}
	}

	/**
	 * Check for project permissions to enable.
	 */
	@CallSuper
	protected open fun getProjectPermissionsToEnable(): MutableList<String> {
		val permissionsToEnable = mutableListOf<String>()

		// Check for RECORD_AUDIO permission.
		val audioInputEnabled = java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("audio/driver/enable_input"))
		if (audioInputEnabled) {
			permissionsToEnable.add(Manifest.permission.RECORD_AUDIO)
		}

		return permissionsToEnable
	}

	protected open fun getEditorGameEmbedMode() = GameMenuUtils.GameEmbedMode.AUTO

	@CallSuper
	override fun supportsFeature(featureTag: String): Boolean {
		if (HYBRID_APP_FEATURE == featureTag) {
			// Check if hybrid is enabled.
			return orblit?.isXrRuntime == true && isHybridAppEnabled()
		}

		return super.supportsFeature(featureTag)
	}
}
