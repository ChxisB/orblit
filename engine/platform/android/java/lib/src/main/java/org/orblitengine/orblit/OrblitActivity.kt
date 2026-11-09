/**************************************************************************/
/*  OrblitActivity.kt                                                     */

package org.orblitengine.orblit

import android.app.Activity
import android.app.PictureInPictureParams
import android.content.ComponentName
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Rect
import android.os.Build
import android.os.Bundle
import android.util.Log
import android.util.Rational
import android.view.View
import androidx.activity.addCallback
import androidx.annotation.CallSuper
import androidx.annotation.LayoutRes
import androidx.fragment.app.FragmentActivity
import org.orblitengine.orblit.feature.PictureInPictureProvider
import org.orblitengine.orblit.utils.CommandLineFileParser
import org.orblitengine.orblit.utils.PermissionsUtil
import org.orblitengine.orblit.utils.ProcessPhoenix
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicReference

/**
 * Base abstract activity for Android apps intending to use Orblit as the primary screen.
 *
 * This is also a reference implementation for how to set up and use the [OrblitFragment] fragment
 * within an Android app.
 */
abstract class OrblitActivity : FragmentActivity(), OrblitHost, PictureInPictureProvider {

	companion object {
		private val TAG = OrblitActivity::class.java.simpleName

		@JvmStatic
		val EXTRA_COMMAND_LINE_PARAMS = "command_line_params"

		@JvmStatic
		protected val EXTRA_NEW_LAUNCH = "new_launch_requested"

		// This window must not match those in BaseOrblitEditor.RUN_GAME_INFO etc
		@JvmStatic
		private val DEFAULT_WINDOW_ID = 664
	}

	/**
	 * Set to true if the activity should automatically enter picture-in-picture when put in the background.
	 */
	private val pipAspectRatio = AtomicReference<Rational>()
	private val autoEnterPiP = AtomicBoolean(false)
	private val gameViewSourceRectHint = Rect()
	private val commandLineParams = ArrayList<String>()

	// The bounds of what the aspect ratio can be, are between 2.39:1 and 1:2.39 (inclusive).
	// If aspect ratio does not fall between these values, app will crash.
	private val minPiPRatio = Rational(100, 239)
	private val maxPiPRatio = Rational(239, 100)

	/**
	 * Interaction with the [Orblit] object is delegated to the [OrblitFragment] class.
	 */
	protected var orblitFragment: OrblitFragment? = null
		private set

	/**
	 * Flag to control whether the launch intent should be sanitized.
	 */
	protected open fun shouldSanitizeLaunchIntent(): Boolean {
		return true
	}

	/**
	 * Strip out the command line parameters from intent targeting exported activities.
	 */
	protected fun sanitizeLaunchIntent(launchIntent: Intent = intent): Intent {
		val targetComponent = launchIntent.component ?: componentName
		val activityInfo = packageManager.getActivityInfo(targetComponent, 0)
		if (activityInfo.exported && shouldSanitizeLaunchIntent()) {
			launchIntent.removeExtra(EXTRA_COMMAND_LINE_PARAMS)
		}

		return launchIntent
	}

	/**
	 * Only retrieve the command line parameters from the intent from non-exported activities.
	 * This ensures only internal components can configure how the engine is run.
	 */
	protected fun retrieveCommandLineParamsFromLaunchIntent(launchIntent: Intent = intent): Array<String> {
		val targetComponent = launchIntent.component ?: componentName
		val activityInfo = packageManager.getActivityInfo(targetComponent, 0)
		if (!activityInfo.exported || !shouldSanitizeLaunchIntent()) {
			val params = launchIntent.getStringArrayExtra(EXTRA_COMMAND_LINE_PARAMS)
			return params ?: emptyArray()
		}
		return emptyArray()
	}

	@CallSuper
	override fun onCreate(savedInstanceState: Bundle?) {
		intent = sanitizeLaunchIntent(intent)

		val assetsCommandLine = try {
			CommandLineFileParser.parseCommandLine(assets.open("_cl_"))
		} catch (_: Exception) {
			mutableListOf()
		}
		Log.d(TAG, "Project command line parameters: $assetsCommandLine")
		commandLineParams.addAll(assetsCommandLine)

		val intentCommandLine = retrieveCommandLineParamsFromLaunchIntent()
		Log.d(TAG, "Launch intent $intent with parameters ${intentCommandLine.contentToString()}")
		commandLineParams.addAll(intentCommandLine)

		super.onCreate(savedInstanceState)

		setContentView(getOrblitAppLayout())

		// Register `OnBackPressedCallback` for the Orblit fragment.
		onBackPressedDispatcher.addCallback { orblitFragment?.onBackPressed() }

		handleStartIntent(intent, true)

		val currentFragment = supportFragmentManager.findFragmentById(R.id.orblit_fragment_container)
		if (currentFragment is OrblitFragment) {
			Log.v(TAG, "Reusing existing Orblit fragment instance.")
			orblitFragment = currentFragment
		} else {
			Log.v(TAG, "Creating new Orblit fragment instance.")
			orblitFragment = initOrblitInstance()

			val transaction = supportFragmentManager.beginTransaction()
			if (currentFragment != null) {
				Log.v(TAG, "Removing existing fragment before replacement.")
				transaction.remove(currentFragment)
			}

			transaction.replace(R.id.orblit_fragment_container, orblitFragment!!)
				.setPrimaryNavigationFragment(orblitFragment)
				.commitNowAllowingStateLoss()
		}

		if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
			val gameView = findViewById<View>(R.id.orblit_fragment_container)
			gameView?.addOnLayoutChangeListener { v, left, top, right, bottom, oldLeft, oldTop, oldRight, oldBottom ->
				gameView.getGlobalVisibleRect(gameViewSourceRectHint)
			}
		}
	}

	override fun onNewOrblitInstanceRequested(args: Array<String>): Int {
		Log.d(TAG, "Restarting with parameters ${args.contentToString()}")
		val intent = Intent()
			.setComponent(ComponentName(this, javaClass.name))
			.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
			.putExtra(EXTRA_COMMAND_LINE_PARAMS, args)
		triggerRebirth(null, intent)
		// fake 'process' id returned by create_instance() etc
		return DEFAULT_WINDOW_ID
	}

	protected fun triggerRebirth(bundle: Bundle?, intent: Intent) {
		// Launch a new activity
		Orblit.getInstance(applicationContext).destroyAndKillProcess {
			ProcessPhoenix.triggerRebirth(this, bundle, intent)
		}
	}

	@LayoutRes
	protected open fun getOrblitAppLayout() = R.layout.app_layout

	override fun onDestroy() {
		Log.v(TAG, "Destroying OrblitActivity $this...")
		super.onDestroy()
	}

	override fun onStop() {
		super.onStop()

		if (isInPictureInPictureMode && !isFinishing) {
			// We get in this state when PiP is closed, so we terminate the activity.
			finish()
		}
	}

	override fun onOrblitForceQuit(instance: Orblit) {
		runOnUiThread { terminateOrblitInstance(instance) }
	}

	private fun terminateOrblitInstance(instance: Orblit) {
		orblitFragment?.let {
			if (instance === it.orblit) {
				Log.v(TAG, "Force quitting Orblit instance")
				ProcessPhoenix.forceQuit(this)
			}
		}
	}

	override fun onOrblitRestartRequested(instance: Orblit) {
		runOnUiThread {
			orblitFragment?.let {
				if (instance === it.orblit) {
					// It's very hard to properly de-initialize Orblit on Android to restart the game
					// from scratch. Therefore, we need to kill the whole app process and relaunch it.
					//
					// Restarting only the activity, wouldn't be enough unless it did proper cleanup (including
					// releasing and reloading native libs or resetting their state somehow and clearing static data).
					Log.v(TAG, "Restarting Orblit instance...")
					ProcessPhoenix.triggerRebirth(this)
				}
			}
		}
	}

	override fun onOrblitSetupCompleted() {
		super.onOrblitSetupCompleted()

		if (isPiPEnabled()) {
			try {
				// Update the aspect ratio for picture-in-picture mode.
				val viewportWidth = Integer.parseInt(OrblitLib.getGlobal("display/window/size/viewport_width"))
				val viewportHeight = Integer.parseInt(OrblitLib.getGlobal("display/window/size/viewport_height"))
				pipAspectRatio.set(Rational(viewportWidth, viewportHeight))
			} catch (e: NumberFormatException) {
				Log.w(TAG, "Unable to parse viewport dimensions.", e)
			}
		}
	}

	override fun onNewIntent(newIntent: Intent) {
		intent = sanitizeLaunchIntent(newIntent)
		super.onNewIntent(intent)
		handleStartIntent(intent, false)
	}

	@CallSuper
	protected open fun handleStartIntent(intent: Intent, newLaunch: Boolean) {
		if (!newLaunch) {
			val newLaunchRequested = intent.getBooleanExtra(EXTRA_NEW_LAUNCH, false)
			if (newLaunchRequested) {
				Log.d(TAG, "New launch requested, restarting..")
				val restartIntent = Intent(intent).putExtra(EXTRA_NEW_LAUNCH, false)
				ProcessPhoenix.triggerRebirth(this, restartIntent)
				return
			}
		}
	}

	@CallSuper
	override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
		super.onActivityResult(requestCode, resultCode, data)
		orblitFragment?.onActivityResult(requestCode, resultCode, data)
	}

	@CallSuper
	override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<String>, grantResults: IntArray) {
		super.onRequestPermissionsResult(requestCode, permissions, grantResults)
		orblitFragment?.onRequestPermissionsResult(requestCode, permissions, grantResults)

		// Logging the result of permission requests
		if (requestCode == PermissionsUtil.REQUEST_ALL_PERMISSION_REQ_CODE || requestCode == PermissionsUtil.REQUEST_SINGLE_PERMISSION_REQ_CODE) {
			Log.d(TAG, "Received permissions request result..")
			for (i in permissions.indices) {
				val permissionGranted = grantResults[i] == PackageManager.PERMISSION_GRANTED
				Log.d(TAG, "Permission ${permissions[i]} ${if (permissionGranted) { "granted"} else { "denied" }}")
			}
		}
	}

	override fun getActivity(): Activity? {
		return this
	}

	override fun getOrblit(): Orblit? {
		return orblitFragment?.orblit
	}

	/**
	 * Used to initialize the Orblit fragment instance in [onCreate].
	 */
	protected open fun initOrblitInstance(): OrblitFragment {
		return OrblitFragment()
	}

	@CallSuper
	override fun getCommandLine(): MutableList<String> = commandLineParams

	override fun onPictureInPictureModeChanged(isInPictureInPictureMode: Boolean) {
		super.onPictureInPictureModeChanged(isInPictureInPictureMode)
		orblit?.onPictureInPictureModeChanged(isInPictureInPictureMode)
	}

	/**
	 * Returns true if picture-in-picture (PiP) mode is supported.
	 */
	override fun isPiPModeSupported() = isPiPEnabled() && packageManager.hasSystemFeature(PackageManager.FEATURE_PICTURE_IN_PICTURE)

	/**
	 * Returns true if the current activity has enabled picture-in-picture in its manifest declaration using
	 * 'android:supportsPictureInPicture="true"'
	 */
	protected open fun isPiPEnabled() = false

	fun updatePiPParams(enableAutoEnter: Boolean = autoEnterPiP.get(), aspectRatio: Rational? = pipAspectRatio.get()) {
		val fixedAspectRatio = aspectRatio?.let {
			if (it < minPiPRatio || it > maxPiPRatio) {
				Log.w(TAG, "The bounds of the aspect ratio must be between 2.39:1 and 1:2.39 (inclusive). Coercing to valid range.")
				it.coerceIn(minPiPRatio, maxPiPRatio)
			} else {
				it
			}
		}

		if (isPiPModeSupported()) {
			autoEnterPiP.set(enableAutoEnter)
			pipAspectRatio.set(fixedAspectRatio)

			if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
				val builder = PictureInPictureParams.Builder()
					.setSourceRectHint(gameViewSourceRectHint)
					.setAspectRatio(fixedAspectRatio)
				if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
					builder.setSeamlessResizeEnabled(false)
						.setAutoEnterEnabled(enableAutoEnter)
				}
				if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
					builder.setExpandedAspectRatio(fixedAspectRatio)
				}
				setPictureInPictureParams(builder.build())
			}
		}
	}

	override fun enterPiPMode() {
		if (isPiPModeSupported()) {
			updatePiPParams()

			Log.v(TAG, "Entering PiP mode")
			enterPictureInPictureMode()
		}
	}

	override fun onUserLeaveHint() {
		if (autoEnterPiP.get()) {
			enterPiPMode()
		}
	}
}
