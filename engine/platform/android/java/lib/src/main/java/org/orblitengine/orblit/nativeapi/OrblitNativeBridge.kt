/**************************************************************************/
/*  OrblitNativeBridge.kt                                                 */

package org.orblitengine.orblit.nativeapi

import android.accessibilityservice.AccessibilityServiceInfo
import android.annotation.SuppressLint
import android.app.AlertDialog
import android.content.Context
import android.content.DialogInterface
import android.content.Intent
import android.content.res.Configuration
import android.provider.Settings.Global
import android.os.Build
import android.os.VibrationEffect
import android.os.Vibrator
import android.view.accessibility.AccessibilityManager
import android.util.Log
import android.util.Rational
import android.util.TypedValue
import androidx.annotation.Keep
import androidx.core.net.toUri
import org.orblitengine.orblit.Orblit
import org.orblitengine.orblit.OrblitActivity
import org.orblitengine.orblit.R
import org.orblitengine.orblit.error.Error
import org.orblitengine.orblit.feature.PictureInPictureProvider
import org.orblitengine.orblit.io.FilePicker
import org.orblitengine.orblit.utils.DialogUtils
import org.orblitengine.orblit.utils.OrblitNetUtils
import org.orblitengine.orblit.utils.beginBenchmarkMeasure
import org.orblitengine.orblit.utils.dumpBenchmark
import org.orblitengine.orblit.utils.endBenchmarkMeasure
import org.orblitengine.orblit.variant.Callable as OrblitCallable

/**
 * Holds and expose Orblit apis to the native layer.
 *
 * All the methods in this class are accessed by the native code (java_wrapper.h) and as such are kept private to
 * not be accessible by the rest of the java/kotlin code.
 */
@Keep
internal class OrblitNativeBridge(private val orblit: Orblit) {

	companion object {
		private val TAG = OrblitNativeBridge::class.java.simpleName

	}

	private val vibratorService: Vibrator? by lazy { orblit.context.getSystemService(Context.VIBRATOR_SERVICE) as? Vibrator }

	/**
	 * Invoked on the render thread when the Orblit setup is complete.
	 */
	private fun onOrblitSetupCompleted() {
		orblit.onOrblitSetupCompleted()
	}

	/**
	 * Invoked on the render thread when the Orblit main loop has started.
	 */
	private fun onOrblitMainLoopStarted() {
		orblit.onOrblitMainLoopStarted()
	}

	/**
	 * Invoked on the render thread when the engine is about to terminate.
	 */
	private fun onOrblitTerminating() = orblit.onOrblitTerminating()

	/**
	 * Invoked from the render thread to toggle the immersive mode.
	 */
	private fun nativeEnableImmersiveMode(enabled: Boolean) {
		orblit.runOnHostThread {
			orblit.enableImmersiveMode(enabled)
		}
	}

	private fun isInImmersiveMode() = orblit.isInImmersiveMode()

	private fun isInEdgeToEdgeMode() = orblit.isInEdgeToEdgeMode()

	private fun setKeepScreenOn(enabled: Boolean) = orblit.setKeepScreenOn(enabled)

	private fun restart() { orblit.primaryHost?.onOrblitRestartRequested(orblit) }

	private fun alert(message: String, title: String) {
		orblit.alert(message, title)
	}

	private fun forceQuit(instanceId: Int) = orblit.forceQuit(instanceId)

	/**
	 * Returns true if screen reader is active, false otherwise.
	 */
	@Keep
	private fun isScreenReaderActive(): Boolean {
		val am = orblit.context.getSystemService(Context.ACCESSIBILITY_SERVICE) as? AccessibilityManager
		if (am != null && am.isEnabled()) {
			val serviceInfoList = am.getEnabledAccessibilityServiceList(AccessibilityServiceInfo.FEEDBACK_SPOKEN)
			if (!serviceInfoList.isEmpty()) {
				return true
			}
		}
		return false
	}

	/**
	 * Returns true if high contrast text is enabled, false otherwise.
	 */
	@Keep
	private fun isHighContrastActive(): Boolean {
		val am = orblit.context.getSystemService(Context.ACCESSIBILITY_SERVICE) as? AccessibilityManager
		if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.BAKLAVA && am != null && am.isEnabled()) {
			return am.isHighContrastTextEnabled()
		}
		return false
	}

	/**
	 * Returns true if animation is disabled, false otherwise.
	 */
	@Keep
	private fun isAnimationDisabled(): Boolean {
		val am = orblit.context.getSystemService(Context.ACCESSIBILITY_SERVICE) as? AccessibilityManager
		if (am != null && am.isEnabled()) {
			return (Global.getFloat(orblit.context.getContentResolver(), Global.ANIMATOR_DURATION_SCALE, 1.0f) == 0f
                && Global.getFloat(orblit.context.getContentResolver(), Global.TRANSITION_ANIMATION_SCALE, 1.0f) == 0f
                && Global.getFloat(orblit.context.getContentResolver(), Global.WINDOW_ANIMATION_SCALE, 1.0f) == 0f)
		}
		return false
	}

	/**
	 * Returns true if dark mode is supported, false otherwise.
	 */
	private fun isDarkModeSupported(): Boolean {
		return orblit.context.resources?.configuration?.uiMode?.and(Configuration.UI_MODE_NIGHT_MASK) != Configuration.UI_MODE_NIGHT_UNDEFINED
	}

	/**
	 * Returns true if dark mode is supported and enabled, false otherwise.
	 */
	private fun isDarkMode() = orblit.darkMode

	private fun showFilePicker(currentDirectory: String, filename: String, fileMode: Int, filters: Array<String>) {
		FilePicker.showFilePicker(orblit.context, orblit.getActivity(), currentDirectory, filename, fileMode, filters)
	}

	/**
	 * This method shows a dialog with multiple buttons.
	 *
	 * @param title The title of the dialog.
	 * @param message The message displayed in the dialog.
	 * @param buttons An array of button labels to display.
	 */
	private fun showDialog(title: String, message: String, buttons: Array<String>) {
		orblit.getActivity()?.let { DialogUtils.showDialog(it, title, message, buttons) }
	}

	/**
	 * This method shows a dialog with a text input field, allowing the user to input text.
	 *
	 * @param title The title of the input dialog.
	 * @param message The message displayed in the input dialog.
	 * @param existingText The existing text that will be pre-filled in the input field.
	 */
	private fun showInputDialog(title: String, message: String, existingText: String) {
		orblit.getActivity()?.let { DialogUtils.showInputDialog(it, title, message, existingText) }
	}

	private fun getAccentColor(): Int {
		val value = TypedValue()
		orblit.context.theme.resolveAttribute(android.R.attr.colorAccent, value, true)
		return value.data
	}

	private fun getBaseColor(): Int {
		val value = TypedValue()
		orblit.context.theme.resolveAttribute(android.R.attr.colorBackground, value, true)
		return value.data
	}

	private fun requestPermission(name: String?) = orblit.requestPermission(name)

	private fun requestPermissions() = orblit.requestPermissions()

	private fun getGrantedPermissions() = orblit.getGrantedPermissions()

	/**
	 * Used by the native code (java_wrapper.h) to vibrate the device.
	 * @param durationMs
	 */
	@SuppressLint("MissingPermission")
	private fun vibrate(durationMs: Int, amplitude: Int) {
		if (durationMs > 0 && orblit.requestPermission("VIBRATE")) {
			try {
				if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
					if (amplitude <= -1) {
						vibratorService?.vibrate(
							VibrationEffect.createOneShot(
								durationMs.toLong(),
								VibrationEffect.DEFAULT_AMPLITUDE
							)
						)
					} else {
						vibratorService?.vibrate(
							VibrationEffect.createOneShot(
								durationMs.toLong(),
								amplitude
							)
						)
					}
				} else {
					// deprecated in API 26
					vibratorService?.vibrate(durationMs.toLong())
				}
			} catch (e: SecurityException) {
				Log.w(
					TAG,
					"SecurityException: VIBRATE permission not found. Make sure it is declared in the manifest or enabled in the export preset."
				)
			}
		}
	}

	/**
	 * Internal method used to query whether the host or the registered plugins supports a given feature.
	 *
	 * This is invoked by the native code, and should not be confused with [hasFeature] which is the Android version of
	 * `OS.has_feature()`.
	 */
	private fun checkInternalFeatureSupport(feature: String): Boolean {
		if (orblit.primaryHost?.supportsFeature(feature) == true) {
			return true
		}

		for (plugin in orblit.pluginRegistry.allPlugins) {
			if (plugin.supportsFeature(feature)) {
				return true
			}
		}
		return false
	}

	/**
	 * Get the list of native_extension modules to register.
	 */
	private fun getNativeExtensionConfigFiles(): Array<String> {
		val configFiles = mutableSetOf<String>()
		for (plugin in orblit.pluginRegistry.allPlugins) {
			configFiles.addAll(plugin.pluginNativeExtensionLibrariesPaths)
		}

		return configFiles.toTypedArray()
	}

	private fun getCACertificates(): String {
		return OrblitNetUtils.getCACertificates()
	}

	private fun getActivity() = orblit.getActivity()

	private fun getRenderView() = orblit.renderView

	private fun getClipboard() = orblit.getClipboard()

	private fun setClipboard(text: String) = orblit.setClipboard(text)

	private fun hasClipboard() = orblit.hasClipboard()

	private fun setWindowColor(color: String) = orblit.setWindowColor(color)

	/**
	 * Used by the native code (java_wrapper.h) to access the input fallback mapping.
	 * @return The input fallback mapping for the current XR mode.
	 */
	private fun getInputFallbackMapping(): String? {
		return orblit.xrMode.inputFallbackMapping
	}

	private fun initInputDevices() {
		orblit.orblitInputHandler.initInputDevices()
	}

	private fun createNewOrblitInstance(args: Array<String>): Int {
		return orblit.primaryHost?.onNewOrblitInstanceRequested(args) ?: -1
	}

	private fun nativeBeginBenchmarkMeasure(scope: String, label: String) {
		beginBenchmarkMeasure(scope, label)
	}

	private fun nativeEndBenchmarkMeasure(scope: String, label: String) {
		endBenchmarkMeasure(scope, label)
	}

	private fun nativeDumpBenchmark(benchmarkFile: String) {
		dumpBenchmark(orblit.fileAccessHandler, benchmarkFile)
	}

	private fun nativeSignApk(
		inputPath: String,
		outputPath: String,
		keystorePath: String,
		keystoreUser: String,
		keystorePassword: String
	): Int {
		val signResult = orblit.primaryHost?.signApk(inputPath, outputPath, keystorePath, keystoreUser, keystorePassword)
			?: org.orblitengine.orblit.error.Error.ERR_UNAVAILABLE
		return signResult.toNativeValue()
	}

	private fun nativeVerifyApk(apkPath: String): Int {
		val verifyResult = orblit.primaryHost?.verifyApk(apkPath) ?: Error.ERR_UNAVAILABLE
		return verifyResult.toNativeValue()
	}

	private fun nativeOnEditorWorkspaceSelected(workspace: String) {
		orblit.primaryHost?.onEditorWorkspaceSelected(workspace)
	}

	private fun nativeShowEditorGameDock() {
		orblit.primaryHost?.showEditorGameDock()
	}

	private fun nativeOnDistractionFreeModeChanged(enabled: Boolean) {
		orblit.primaryHost?.onDistractionFreeModeChanged(enabled)
	}

	private fun nativeBuildEnvConnect(callback: OrblitCallable): Boolean {
		try {
			val buildProvider = orblit.primaryHost?.buildProvider
			val success = buildProvider?.buildEnvConnect(callback) ?: false
			if (!success) {
				val activity = orblit.getActivity() ?: return false
				orblit.runOnHostThread {
					val builder = AlertDialog.Builder(activity)
						.setMessage(activity.getString(R.string.gabe_connection_error_message))
						.setTitle(activity.getString(R.string.gabe_connection_error_title))
						.setCancelable(false)
						.setPositiveButton(activity.getString(R.string.dialog_download)) { dialog: DialogInterface, _: Int ->
							val intent = Intent(Intent.ACTION_VIEW, "https://github.com/ChxisB/orblit/releases".toUri())
							intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
							activity.startActivity(intent)
							dialog.cancel()
						}
						.setNegativeButton(activity.getString(R.string.dialog_cancel)) { dialog: DialogInterface, _: Int ->
							dialog.dismiss()
						}
					val dialog = builder.create()
					dialog.show()
				}
			}
			return success
		} catch (e: Exception) {
			Log.e(TAG, "Unable to connect to build environment", e)
			return false
		}
	}

	private fun nativeBuildEnvDisconnect() {
		try {
			val buildProvider = orblit.primaryHost?.buildProvider
			buildProvider?.buildEnvDisconnect()
		} catch (e: Exception) {
			Log.e(TAG, "Unable to disconnect from build environment", e)
		}
	}

	private fun nativeBuildEnvExecute(
		buildTool: String,
		arguments: Array<String>,
		projectPath: String,
		buildDir: String,
		outputCallback: OrblitCallable,
		resultCallback: OrblitCallable
	): Int {
		try {
			val buildProvider = orblit.primaryHost?.buildProvider
			return buildProvider?.buildEnvExecute(
				buildTool,
				arguments,
				projectPath,
				buildDir,
				outputCallback,
				resultCallback
			) ?: -1
		} catch (e: Exception) {
			Log.e(TAG, "Unable to execute Gradle command in build environment", e);
			return -1
		}
	}

	private fun nativeBuildEnvCancel(jobId: Int) {
		try {
			val buildProvider = orblit.primaryHost?.buildProvider
			buildProvider?.buildEnvCancel(jobId)
		} catch (e: Exception) {
			Log.e(TAG, "Unable to cancel command in build environment", e)
		}
	}

	private fun nativeBuildEnvCleanProject(projectPath: String, buildDir: String, callback: OrblitCallable) {
		try {
			val buildProvider = orblit.primaryHost?.buildProvider
			buildProvider?.buildEnvCleanProject(projectPath, buildDir, callback)
		} catch (e: Exception) {
			Log.e(TAG, "Unable to clean project in build environment", e)
		}
	}

	private fun nativeIsPiPModeSupported(): Boolean {
		val hostActivity = orblit.getActivity()
		if (hostActivity is PictureInPictureProvider) {
			return hostActivity.isPiPModeSupported()
		}
		return false
	}

	private fun nativeIsInPiPMode(): Boolean {
		val hostActivity = orblit.getActivity()
		if (hostActivity is OrblitActivity) {
			return hostActivity.isInPictureInPictureMode
		}
		return false
	}

	private fun nativeEnterPiPMode() {
		val hostActivity = orblit.getActivity()
		if (hostActivity is PictureInPictureProvider) {
			orblit.runOnHostThread {
				hostActivity.enterPiPMode()
			}
		}
	}

	private fun nativeSetPiPModeAspectRatio(numerator: Int, denominator: Int) {
		val hostActivity = orblit.getActivity()
		if (hostActivity is OrblitActivity) {
			orblit.runOnHostThread {
				hostActivity.updatePiPParams(aspectRatio = Rational(numerator, denominator))
			}
		}
	}

	private fun nativeSetAutoEnterPiPModeOnBackground(autoEnterPiPOnBackground: Boolean) {
		val hostActivity = orblit.getActivity()
		if (hostActivity is OrblitActivity) {
			orblit.runOnHostThread {
				hostActivity.updatePiPParams(enableAutoEnter = autoEnterPiPOnBackground)
			}
		}
	}
}
