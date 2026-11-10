/**************************************************************************/
/*  Orblit.kt                                                             */

package org.orblitengine.orblit

import android.app.Activity
import android.app.AlertDialog
import android.content.*
import android.content.pm.PackageManager
import android.content.res.Configuration
import android.content.res.Resources
import android.graphics.Color
import android.graphics.drawable.ColorDrawable
import android.hardware.Sensor
import android.hardware.SensorManager
import android.os.*
import android.util.Log
import android.util.TypedValue
import android.view.*
import android.widget.FrameLayout
import androidx.annotation.Keep
import androidx.annotation.StringRes
import androidx.core.graphics.ColorUtils
import androidx.core.graphics.toColorInt
import androidx.core.view.ViewCompat
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsAnimationCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat
import org.orblitengine.orblit.input.OrblitEditText
import org.orblitengine.orblit.input.OrblitInputHandler
import org.orblitengine.orblit.io.FilePicker
import org.orblitengine.orblit.io.StorageScope
import org.orblitengine.orblit.io.directory.DirectoryAccessHandler
import org.orblitengine.orblit.io.file.FileAccessHandler
import org.orblitengine.orblit.nativeapi.OrblitNativeBridge
import org.orblitengine.orblit.plugin.AndroidRuntimePlugin
import org.orblitengine.orblit.plugin.OrblitPlugin
import org.orblitengine.orblit.plugin.OrblitPluginRegistry
import org.orblitengine.orblit.tts.OrblitTTS
import org.orblitengine.orblit.utils.OrblitNetUtils
import org.orblitengine.orblit.utils.PermissionsUtil
import org.orblitengine.orblit.utils.PermissionsUtil.requestPermission
import org.orblitengine.orblit.utils.beginBenchmarkMeasure
import org.orblitengine.orblit.utils.benchmarkFile
import org.orblitengine.orblit.utils.endBenchmarkMeasure
import org.orblitengine.orblit.utils.useBenchmark
import org.orblitengine.orblit.xr.XRMode
import java.util.*
import java.util.concurrent.Callable
import java.util.concurrent.CountDownLatch
import java.util.concurrent.FutureTask
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicReference

/**
 * Core component used to interface with the native layer of the engine.
 *
 * Can be hosted by [Activity], [androidx.fragment.app.Fragment] or [android.app.Service] android components, so long
 * as its lifecycle methods are properly invoked.
 */
class Orblit private constructor(val context: Context) {

	companion object {
		private val TAG = Orblit::class.java.simpleName

		@Volatile private var INSTANCE: Orblit? = null

		@JvmStatic
		fun getInstance(context: Context): Orblit {
			return INSTANCE ?: synchronized(this) {
				INSTANCE ?: Orblit(context.applicationContext).also { INSTANCE = it }
			}
		}

		private const val EXIT_RENDERER_TIMEOUT_IN_MS = 1500L

		// Supported build flavors
		private const val EDITOR_FLAVOR = "editor"
		private const val TEMPLATE_FLAVOR = "template"

		/**
		 * @return true if this is an editor build, false if this is a template build.
		 */
		internal fun isEditorBuild() = BuildConfig.FLAVOR == EDITOR_FLAVOR

		/**
		 * @return true if this is a template build, false if this is an editor build.
		 */
		internal fun isTemplateBuild() = BuildConfig.FLAVOR == TEMPLATE_FLAVOR
	}

	/**
	 * Describes the engine current run status.
	 */
	enum class RunStatus {
		INITIALIZING,
		STARTED,
		TERMINATING
	}

	private val orblitNativeBridge = OrblitNativeBridge(this)

	private val mSensorManager: SensorManager? by lazy { context.getSystemService(Context.SENSOR_SERVICE) as? SensorManager }
	private val mClipboard: ClipboardManager? by lazy { context.getSystemService(Context.CLIPBOARD_SERVICE) as? ClipboardManager }
	internal val pluginRegistry: OrblitPluginRegistry by lazy { OrblitPluginRegistry.getPluginRegistry() }

	private val accelerometerEnabled = AtomicBoolean(false)
	private val mAccelerometer: Sensor? by lazy { mSensorManager?.getDefaultSensor(Sensor.TYPE_ACCELEROMETER) }

	private val gravityEnabled = AtomicBoolean(false)
	private val mGravity: Sensor? by lazy { mSensorManager?.getDefaultSensor(Sensor.TYPE_GRAVITY) }

	private val magnetometerEnabled = AtomicBoolean(false)
	private val mMagnetometer: Sensor? by lazy { mSensorManager?.getDefaultSensor(Sensor.TYPE_MAGNETIC_FIELD) }

	private val gyroscopeEnabled = AtomicBoolean(false)
	private val mGyroscope: Sensor? by lazy { mSensorManager?.getDefaultSensor(Sensor.TYPE_GYROSCOPE) }

	private val deviceOrientationEnabled = AtomicBoolean(false)
	private val rotationVector: Sensor? by lazy {
		mSensorManager?.getDefaultSensor(Sensor.TYPE_GAME_ROTATION_VECTOR)
			?: mSensorManager?.getDefaultSensor(Sensor.TYPE_ROTATION_VECTOR)
	}

	val isXrRuntime: Boolean by lazy { hasFeature("xr_runtime") }

	val tts = OrblitTTS(context)
	val directoryAccessHandler = DirectoryAccessHandler(context)
	val fileAccessHandler = FileAccessHandler(context)
	val netUtils = OrblitNetUtils(context)
	val orblitInputHandler = OrblitInputHandler(context, this)

	private val hasClipboardCallable = Callable {
		mClipboard?.hasPrimaryClip() == true
	}

	private val getClipboardCallable = Callable {
		val clipData = mClipboard?.primaryClip
		val text = clipData?.getItemAt(0)?.text
		text?.toString() ?: ""
	}

	/**
	 * Task to run when the engine terminates.
	 */
	private val runOnTerminate = AtomicReference<Runnable>()

	/**
	 * Tracks whether [OrblitLib.initialize] was completed successfully.
	 */
	private var nativeLayerInitializeCompleted = false

	/**
	 * Tracks whether [OrblitLib.setup] was completed successfully.
	 */
	private var nativeLayerSetupCompleted = false

	/**
	 * Tracks whether [onInitRenderView] was completed successfully.
	 */
	private var renderViewInitialized = false
	internal var primaryHost: OrblitHost? = null

	/**
	 * Tracks whether we're in the RESUMED lifecycle state.
	 * See [onResume] and [onPause]
	 */
	private var resumed = false

	/**
	 * Tracks the engine's run status.
	 */
	private val _runStatus = AtomicReference<RunStatus>(RunStatus.INITIALIZING)
	val runStatus: RunStatus
		get() = _runStatus.get()

	val io = OrblitIO(this)

	private var commandLine : MutableList<String> = ArrayList<String>()
	internal var xrMode = XRMode.REGULAR
	private val useImmersive = AtomicBoolean(false)
	private val isEdgeToEdge = AtomicBoolean(false)
	private var useDebugOpengl = false
	internal var darkMode = false
	private var backgroundColor: Int = Color.BLACK
	private var orientation = Configuration.ORIENTATION_UNDEFINED
	private var isImeAnimating = false
	var disableOrblitSplash = false
		private set

	internal var containerLayout: FrameLayout? = null
	var renderView: OrblitRenderView? = null

	/**
	 * Returns true if the native engine has been initialized through [initEngine], false otherwise.
	 */
	private fun isNativeInitialized() = nativeLayerInitializeCompleted && nativeLayerSetupCompleted

	/**
	 * Returns true if the engine has been initialized, false otherwise.
	 */
	fun isInitialized() = primaryHost != null && isNativeInitialized() && renderViewInitialized

	/**
	 * Provides access to the primary host [Activity].
	 */
	fun getActivity() = primaryHost?.activity

	/**
	 * Start initialization of the Orblit engine.
	 *
	 * This must be followed by [onInitRenderView] to complete initialization of the engine.
	 *
	 * @return false if initialization of the native layer fails, true otherwise.
	 */
	fun initEngine(host: OrblitHost?, commandLineParams: List<String>, hostPlugins: Set<OrblitPlugin> = Collections.emptySet()): Boolean {
		if (isNativeInitialized()) {
			Log.d(TAG, "Engine already initialized")
			return true
		}

		val config = context.resources.configuration
		darkMode = (config.uiMode and Configuration.UI_MODE_NIGHT_MASK) == Configuration.UI_MODE_NIGHT_YES
		orientation = config.orientation

		beginBenchmarkMeasure("Startup", "Orblit::initEngine")
		try {
			this.primaryHost = host
			commandLine.addAll(commandLineParams)

			Log.v(TAG, "Initializing Orblit plugin registry")
			val runtimePlugins = mutableSetOf<OrblitPlugin>(AndroidRuntimePlugin(this))
			runtimePlugins.addAll(hostPlugins)
			OrblitPluginRegistry.initializePluginRegistry(this, runtimePlugins)

			// Let the plugins take a peek at the command line params and provide their own params if desired.
			val originalCommandLineParams = Collections.unmodifiableList(commandLineParams)
			for (plugin in pluginRegistry.allPlugins) {
				try {
					val pluginCommandLineParams = plugin.getCommandLineParams(originalCommandLineParams)
					if (pluginCommandLineParams != originalCommandLineParams && pluginCommandLineParams.isNotEmpty()) {
						Log.d(TAG, "Received command line params from plugin $plugin: $pluginCommandLineParams")
						commandLine.addAll(pluginCommandLineParams)
					}
				} catch (e: Exception) {
					Log.e(TAG, "Unable to get command line params from plugin $plugin", e)
				}
			}

			Log.v(TAG, "InitEngine with params: $commandLine")
			val newArgs: MutableList<String> = ArrayList()
			var useApkExpansion = false
			var i = 0
			while (i < commandLine.size) {
				val hasExtra: Boolean = i < commandLine.size - 1
				if (commandLine[i] == XRMode.REGULAR.cmdLineArg) {
					xrMode = XRMode.REGULAR
				} else if (commandLine[i] == XRMode.OPENXR.cmdLineArg) {
					xrMode = XRMode.OPENXR
				} else if (commandLine[i] == "--debug_opengl") {
					useDebugOpengl = true
				} else if (commandLine[i] == "--edge_to_edge") {
					isEdgeToEdge.set(true)
				} else if (commandLine[i] == "--fullscreen") {
					useImmersive.set(true)
					newArgs.add(commandLine[i])
				} else if (hasExtra && commandLine[i] == "--background_color") {
					setWindowColor(commandLine[i + 1])
					i++
				} else if (commandLine[i] == "--disable_orblit_splash") {
					disableOrblitSplash = true
				} else if (commandLine[i] == "--benchmark") {
					useBenchmark = true
					newArgs.add(commandLine[i])
				} else if (hasExtra && commandLine[i] == "--benchmark-file") {
					useBenchmark = true
					newArgs.add(commandLine[i])

					// Retrieve the filepath
					benchmarkFile = commandLine[i + 1]
					newArgs.add(commandLine[i + 1])

					i++
				} else if (hasExtra && commandLine[i] == "--main-pack") {
					newArgs.add(commandLine[i])

					val mainPackPath = commandLine[i + 1]
					newArgs.add(commandLine[i + 1])
					// Check the storage scope of the main pack path. For template builds, `useApkExpansion` is enabled
					// if the storage scope is APP.
					val storageScope = fileAccessHandler.storageScopeIdentifier.identifyStorageScope(mainPackPath)
					if (isTemplateBuild()) {
						useApkExpansion = storageScope == StorageScope.APP
					}
					i++
				} else if (commandLine[i].trim().isNotEmpty()) { // This block should always be last!
					newArgs.add(commandLine[i])
				}
				i++
			}

			commandLine = if (newArgs.isEmpty()) { mutableListOf() } else { newArgs }
			if (!nativeLayerInitializeCompleted) {
				nativeLayerInitializeCompleted = OrblitLib.initialize(
					orblitNativeBridge,
					context.assets,
					io,
					netUtils,
					directoryAccessHandler,
					fileAccessHandler,
					useApkExpansion,
				)
				Log.v(TAG, "Orblit native layer initialization completed: $nativeLayerInitializeCompleted")
			}

			if (nativeLayerInitializeCompleted && !nativeLayerSetupCompleted) {
				Log.v(TAG, "Setting up native layer with params: $commandLine")
				nativeLayerSetupCompleted = OrblitLib.setup(commandLine.toTypedArray(), tts)
				if (!nativeLayerSetupCompleted) {
					throw IllegalStateException("Unable to setup the Orblit engine! Aborting...")
				} else {
					Log.v(TAG, "Orblit native layer setup completed")
				}
			}
		} finally {
			endBenchmarkMeasure("Startup", "Orblit::initEngine")
		}
		return isNativeInitialized()
	}

	/**
	 * Enable edge-to-edge.
	 *
	 * Must be called from the UI thread.
	 */
	@JvmOverloads
	fun enableEdgeToEdge(enabled: Boolean, override: Boolean = false) {
		// Note: If modifying edge-to-edge or immersive mode logic, ensure to test with OrblitIO.getDisplaySafeArea()
		// to confirm there are no regressions in safe area calculation.
		val window = getActivity()?.window ?: return

		if (!isEdgeToEdge.compareAndSet(!enabled, enabled) && !override) {
			return
		}

		val rootView = window.decorView
		WindowCompat.setDecorFitsSystemWindows(window, !(isEdgeToEdge.get() || useImmersive.get()))
		if (enabled) {
			ViewCompat.setOnApplyWindowInsetsListener(rootView) { v: View, insets: WindowInsetsCompat ->
				v.post {
					resetVirtualKeyboardHeight(insets)
				}
				WindowInsetsCompat.CONSUMED
			}
			rootView.setPadding(0, 0, 0, 0)
			if (Build.VERSION.SDK_INT < Build.VERSION_CODES.R) {
				window.addFlags(WindowManager.LayoutParams.FLAG_TRANSLUCENT_STATUS)
				window.addFlags(WindowManager.LayoutParams.FLAG_TRANSLUCENT_NAVIGATION)
			}
		} else {
			if (rootView.rootWindowInsets != null) {
				if (!useImmersive.get()) {
					val windowInsets = WindowInsetsCompat.toWindowInsetsCompat(rootView.rootWindowInsets)
					val insets = windowInsets.getInsets(getInsetType())
					rootView.setPadding(insets.left, insets.top, insets.right, insets.bottom)
				}
			}

			ViewCompat.setOnApplyWindowInsetsListener(rootView) { v: View, insets: WindowInsetsCompat ->
				v.post {
					if (useImmersive.get()) {
						if (isEditorBuild()) {
							val windowInsets = insets.getInsets(WindowInsetsCompat.Type.displayCutout())
							v.setPadding(windowInsets.left, windowInsets.top, windowInsets.right, windowInsets.bottom)
						} else {
							v.setPadding(0, 0, 0, 0)
						}
					} else {
						val windowInsets = insets.getInsets(getInsetType())
						v.setPadding(windowInsets.left, windowInsets.top, windowInsets.right, windowInsets.bottom)
					}

					resetVirtualKeyboardHeight(insets)
				}
				WindowInsetsCompat.CONSUMED
			}
		}
	}

	private fun getInsetType(): Int {
		return if (!useImmersive.get() || isEditorBuild()) {
			WindowInsetsCompat.Type.systemBars() or WindowInsetsCompat.Type.displayCutout()
		} else {
			WindowInsetsCompat.Type.systemBars()
		}
	}

	private fun resetVirtualKeyboardHeight(insets: WindowInsetsCompat) {
		val isImeVisible = insets.isVisible(WindowInsetsCompat.Type.ime())
		val imeBottom = insets.getInsets(WindowInsetsCompat.Type.ime()).bottom

		if (isImeVisible && !isImeAnimating) {
			OrblitLib.setVirtualKeyboardHeight(imeBottom)
		}
	}

	/**
	 * Toggle immersive mode.
	 * Must be called from the UI thread.
	 */
	@JvmOverloads
	fun enableImmersiveMode(enabled: Boolean, override: Boolean = false) {
		// Note: If modifying edge-to-edge or immersive mode logic, ensure to test with OrblitIO.getDisplaySafeArea()
		// to confirm there are no regressions in safe area calculation.
		val activity = getActivity() ?: return
		val window = activity.window ?: return

		if (!useImmersive.compareAndSet(!enabled, enabled) && !override) {
			return
		}

		WindowCompat.setDecorFitsSystemWindows(window, !(isEdgeToEdge.get() || useImmersive.get()))
		val controller = WindowInsetsControllerCompat(window, window.decorView)
		if (enabled) {
			controller.hide(WindowInsetsCompat.Type.systemBars())
			controller.systemBarsBehavior = WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
		} else {
			val fullScreenThemeValue = TypedValue()
			var hasStatusBar = if (activity.theme.resolveAttribute(android.R.attr.windowFullscreen, fullScreenThemeValue, true) && fullScreenThemeValue.type == TypedValue.TYPE_INT_BOOLEAN) {
				fullScreenThemeValue.data == 0
			} else {
				// Fallback to checking the editor build
				!isEditorBuild()
			}

			if (isEditorBuild() && orientation == Configuration.ORIENTATION_PORTRAIT) {
				hasStatusBar = true
			}

			val types = if (hasStatusBar) {
				WindowInsetsCompat.Type.navigationBars() or WindowInsetsCompat.Type.statusBars()
			} else {
				WindowInsetsCompat.Type.navigationBars()
			}
			controller.show(types)
		}
	}

	fun isInImmersiveMode() = useImmersive.get()

	fun isInEdgeToEdgeMode() = isEdgeToEdge.get()

	fun setSystemBarsAppearance() {
		val window = getActivity()?.window ?: return
		val isLight = ColorUtils.calculateLuminance(getWindowBackgroundColor(window)) > 0.5

		val controller = WindowInsetsControllerCompat(window, window.decorView)
		controller.isAppearanceLightNavigationBars = isLight
		controller.isAppearanceLightStatusBars = isLight
	}

	private fun getWindowBackgroundColor(window: Window): Int {
		val background = window.decorView.background
		return if (background is ColorDrawable) {
			background.color
		} else {
			backgroundColor
		}
	}

	fun setWindowColor(colorStr: String) {
		val color = try {
			colorStr.toColorInt()
		} catch (e: java.lang.IllegalArgumentException) {
			Log.w(TAG, "Failed to parse background color: $colorStr", e)
			return
		}
		val decorView = getActivity()?.window?.decorView ?: return
		runOnHostThread {
			decorView.setBackgroundColor(color)
			backgroundColor = color
			setSystemBarsAppearance()
		}
	}

	/**
	 * Used to complete initialization of the view used by the engine for rendering.
	 *
	 * This must be preceded by [initEngine] to properly initialize the engine.
	 *
	 * @param host The [OrblitHost] that's initializing the render views
	 * @param providedContainerLayout Optional argument; if provided, this is reused to host the Orblit's render views
	 *
	 * @return A [FrameLayout] instance containing Orblit's render views if initialization is successful, null otherwise.
	 *
	 * @throws IllegalStateException if [initEngine] has not been called
	 */
	@JvmOverloads
	fun onInitRenderView(host: OrblitHost, providedContainerLayout: FrameLayout = FrameLayout(context)): FrameLayout? {
		if (!isNativeInitialized()) {
			throw IllegalStateException("initEngine(...) must be invoked successfully prior to initializing the render view")
		}

		beginBenchmarkMeasure("Startup", "Orblit::onInitRenderView")
		Log.v(TAG, "OnInitRenderView: $host")
		try {
			this.primaryHost = host
			getActivity()?.window?.addFlags(WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON)

			if (containerLayout != null) {
				assert(renderViewInitialized)
				return containerLayout
			}

			containerLayout = providedContainerLayout
			containerLayout?.removeAllViews()
			val layoutParams = containerLayout?.layoutParams ?: ViewGroup.LayoutParams(
					ViewGroup.LayoutParams.MATCH_PARENT,
					ViewGroup.LayoutParams.MATCH_PARENT
			)
			layoutParams.width = ViewGroup.LayoutParams.MATCH_PARENT
			layoutParams.height = ViewGroup.LayoutParams.MATCH_PARENT
			containerLayout?.layoutParams = layoutParams

			// OrblitEditText layout
			val editText = OrblitEditText(context)
			editText.layoutParams =
					ViewGroup.LayoutParams(
							ViewGroup.LayoutParams.MATCH_PARENT,
							context.resources.getDimension(R.dimen.text_edit_height).toInt()
					)
			// Prevent OrblitEditText from showing on splash screen on devices with Android 14 or newer.
			editText.setBackgroundColor(Color.TRANSPARENT)
			// ...add to FrameLayout
			containerLayout?.addView(editText)

			// Check whether the render view should be made transparent
			val shouldBeTransparent =
				!isProjectManagerHint() &&
					!isEditorHint() &&
					java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("display/window/per_pixel_transparency/allowed"))
			Log.d(TAG, "Render view should be transparent: $shouldBeTransparent")

			val nativeRenderer = getNativeRenderer()
			if (nativeRenderer == "vulkan") {
				renderView = OrblitVulkanRenderView(this, orblitInputHandler, shouldBeTransparent)
			} else if (nativeRenderer == "opengl3") {
				renderView = OrblitGLRenderView(this, orblitInputHandler, xrMode, useDebugOpengl, shouldBeTransparent)
			} else {
				throw IllegalStateException("No native renderer is available.")
			}

			renderView?.let {
				it.startRenderer()
				containerLayout?.addView(
					it.view,
					ViewGroup.LayoutParams(
							ViewGroup.LayoutParams.MATCH_PARENT,
							ViewGroup.LayoutParams.MATCH_PARENT
					)
				)
			}

			editText.setView(renderView)
			io.setEdit(editText)

			val activity = host.activity
			// Listeners for keyboard height.
			val topView = activity?.window?.decorView ?: providedContainerLayout
			// Report the height of virtual keyboard as it changes during the animation.
			ViewCompat.setWindowInsetsAnimationCallback(topView, object : WindowInsetsAnimationCompat.Callback(DISPATCH_MODE_STOP) {
				var startBottom = 0
				var endBottom = 0
				override fun onPrepare(animation: WindowInsetsAnimationCompat) {
					startBottom = ViewCompat.getRootWindowInsets(topView)?.getInsets(WindowInsetsCompat.Type.ime())?.bottom ?: 0
					isImeAnimating = true
				}

				override fun onStart(
					animation: WindowInsetsAnimationCompat,
					bounds: WindowInsetsAnimationCompat.BoundsCompat
				): WindowInsetsAnimationCompat.BoundsCompat {
					endBottom = ViewCompat.getRootWindowInsets(topView)?.getInsets(WindowInsetsCompat.Type.ime())?.bottom ?: 0
					isImeAnimating = true
					return bounds
				}

				override fun onProgress(
					windowInsets: WindowInsetsCompat,
					animationsList: List<WindowInsetsAnimationCompat>
				): WindowInsetsCompat {
					// Find the IME animation.
					var imeAnimation: WindowInsetsAnimationCompat? = null
					for (animation in animationsList) {
						if (animation.typeMask and WindowInsetsCompat.Type.ime() != 0) {
							imeAnimation = animation
							break
						}
					}
					isImeAnimating = imeAnimation != null

					// Update keyboard height based on IME animation.
					if (imeAnimation != null) {
						val interpolatedFraction = imeAnimation.interpolatedFraction
						// Linear interpolation between start and end values.
						val keyboardHeight = startBottom * (1.0f - interpolatedFraction) + endBottom * interpolatedFraction
						val finalHeight = maxOf(keyboardHeight.toInt() - topView.rootView.paddingBottom, 0)
						OrblitLib.setVirtualKeyboardHeight(finalHeight)
					}
					return windowInsets
				}

				override fun onEnd(animation: WindowInsetsAnimationCompat) {
					isImeAnimating = false
					// Fixes an issue on Android 10 and older where immersive mode gets auto disabled after the keyboard is hidden on some devices.
					if (useImmersive.get() && Build.VERSION.SDK_INT < Build.VERSION_CODES.R) {
						runOnHostThread {
							enableImmersiveMode(enabled = true, override = true)
						}
					}
				}
			})

			renderView?.queueOnRenderThread {
				for (plugin in pluginRegistry.allPlugins) {
					// Plugins should be registered early so they are available as soon as the app starts.
					// Otherwise, a delay in registration may make them unavailable during _init() of the main script or an autoload.
					plugin.onRegisterPluginWithOrblitNative()
				}
				setKeepScreenOn(java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("display/window/energy_saving/keep_screen_on")))
			}

			// Include the returned non-null views in the Orblit view hierarchy.
			for (plugin in pluginRegistry.allPlugins) {
				val pluginView = plugin.onMainCreate(activity)
				if (pluginView != null) {
					if (plugin.shouldBeOnTop()) {
						containerLayout?.addView(pluginView)
					} else {
						containerLayout?.addView(pluginView, 0)
					}
				}
			}
			renderViewInitialized = true
		} finally {
			if (!renderViewInitialized) {
				containerLayout?.removeAllViews()
				containerLayout = null
			}

			endBenchmarkMeasure("Startup", "Orblit::onInitRenderView")
		}
		return containerLayout
	}

	fun onStart(host: OrblitHost) {
		Log.v(TAG, "OnStart: $host")
		if (host != primaryHost) {
			return
		}

		renderView?.onActivityStarted()
		for (plugin in pluginRegistry.allPlugins) {
			plugin.onMainStart()
		}
	}

	fun onResume(host: OrblitHost) {
		Log.v(TAG, "OnResume: $host")
		resumed = true
		if (host != primaryHost) {
			return
		}

		renderView?.onActivityResumed()
		registerSensorsIfNeeded()
		for (plugin in pluginRegistry.allPlugins) {
			plugin.onMainResume()
		}
	}

	private fun registerSensorsIfNeeded() {
		if (!resumed || runStatus != RunStatus.STARTED) {
			return
		}

		if (accelerometerEnabled.get() && mAccelerometer != null) {
			mSensorManager?.registerListener(orblitInputHandler, mAccelerometer, SensorManager.SENSOR_DELAY_GAME)
		}
		if (gravityEnabled.get() && mGravity != null) {
			mSensorManager?.registerListener(orblitInputHandler, mGravity, SensorManager.SENSOR_DELAY_GAME)
		}
		if (magnetometerEnabled.get() && mMagnetometer != null) {
			mSensorManager?.registerListener(orblitInputHandler, mMagnetometer, SensorManager.SENSOR_DELAY_GAME)
		}
		if (gyroscopeEnabled.get() && mGyroscope != null) {
			mSensorManager?.registerListener(orblitInputHandler, mGyroscope, SensorManager.SENSOR_DELAY_GAME)
		}
		if (deviceOrientationEnabled.get() && rotationVector != null) {
			mSensorManager?.registerListener(orblitInputHandler, rotationVector, SensorManager.SENSOR_DELAY_GAME)
		}
	}

	internal fun onPictureInPictureModeChanged(isInPictureInPictureMode: Boolean) {
		Log.v(TAG, "onPictureInPictureModeChanged: $isInPictureInPictureMode")
		runOnRenderThread {
			OrblitLib.onPictureInPictureModeChanged(isInPictureInPictureMode)
		}
	}

	fun onPause(host: OrblitHost) {
		Log.v(TAG, "OnPause: $host")
		resumed = false
		if (host != primaryHost) {
			return
		}

		for (plugin in pluginRegistry.allPlugins) {
			plugin.onMainPause()
		}
		renderView?.onActivityPaused()
		mSensorManager?.unregisterListener(orblitInputHandler)
	}

	fun onStop(host: OrblitHost) {
		Log.v(TAG, "OnStop: $host")
		if (host != primaryHost) {
			return
		}

		for (plugin in pluginRegistry.allPlugins) {
			plugin.onMainStop()
		}
		renderView?.onActivityStopped()
	}

	fun onDestroy(primaryHost: OrblitHost) {
		if (this.primaryHost != primaryHost) {
			return
		}
		Log.v(TAG, "OnDestroy: $primaryHost")

		for (plugin in pluginRegistry.allPlugins) {
			plugin.onMainDestroy()
		}

		if (renderView?.blockingExitRenderer(EXIT_RENDERER_TIMEOUT_IN_MS) != true) {
			Log.w(TAG, "Unable to exit the renderer within $EXIT_RENDERER_TIMEOUT_IN_MS ms... Force quitting the process.")
			onOrblitTerminating()
			forceQuit(0)
		}

		this.primaryHost = null
	}

	/**
	 * Configuration change callback
	*/
	fun onConfigurationChanged(newConfig: Configuration) {
		orblitInputHandler.onConfigurationChanged(newConfig)

		val newDarkMode = newConfig.uiMode.and(Configuration.UI_MODE_NIGHT_MASK) == Configuration.UI_MODE_NIGHT_YES
		if (darkMode != newDarkMode) {
			darkMode = newDarkMode
			runOnRenderThread {
				OrblitLib.onNightModeChanged()
			}
		}

		if (orientation != newConfig.orientation) {
			orientation = newConfig.orientation
			runOnRenderThread {
				OrblitLib.onOrientationChange(orientation)
			}

			if (isEditorBuild() && !isInImmersiveMode()) {
				val window = getActivity()?.window
				if (window != null) {
					val controller = WindowInsetsControllerCompat(window, window.decorView)
					if (orientation == Configuration.ORIENTATION_PORTRAIT) {
						controller.show(WindowInsetsCompat.Type.statusBars())
					} else {
						controller.hide(WindowInsetsCompat.Type.statusBars())
					}
				}
			}
		}
	}

	/**
	 * Activity result callback
	 */
	fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
		for (plugin in pluginRegistry.allPlugins) {
			plugin.onMainActivityResult(requestCode, resultCode, data)
		}
		runOnRenderThread {
			FilePicker.handleActivityResult(context, requestCode, resultCode, data)
		}
	}

	/**
	 * Permissions request callback
	 */
	fun onRequestPermissionsResult(
		requestCode: Int,
		permissions: Array<String?>,
		grantResults: IntArray
	) {
		for (plugin in pluginRegistry.allPlugins) {
			plugin.onMainRequestPermissionsResult(requestCode, permissions, grantResults)
		}
		runOnRenderThread {
			for (i in permissions.indices) {
				OrblitLib.requestPermissionResult(
					permissions[i],
					grantResults[i] == PackageManager.PERMISSION_GRANTED
				)
			}
		}
	}

	/**
	 * Invoked on the render thread when the Orblit setup is complete.
	 */
	internal fun onOrblitSetupCompleted() {
		Log.v(TAG, "OnOrblitSetupCompleted")

		// These properties are defined after Orblit setup completion, so we retrieve them here.
		val rightClickEmulataionEnabled = java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("input_devices/pointing/android/enable_long_press_as_right_click"))
		val panScaleEnabled = java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("input_devices/pointing/android/enable_pan_and_scale_gestures"))
		val rotaryInputAxisValue = OrblitLib.getGlobal("input_devices/pointing/android/rotary_input_scroll_axis")
		val overrideVolumeButtons = java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("input_devices/pointing/android/override_volume_buttons"))
		val scrollDeadzoneDisabled = java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("input_devices/pointing/android/disable_scroll_deadzone"))

		runOnHostThread {
			orblitInputHandler.apply {
				enableRightClickEmulation(rightClickEmulataionEnabled)
				enablePanningAndScalingGestures(panScaleEnabled)
				setOverrideVolumeButtons(overrideVolumeButtons)
				disableScrollDeadzone(scrollDeadzoneDisabled)
				try {
					setRotaryInputAxis(Integer.parseInt(rotaryInputAxisValue))
				} catch (e: NumberFormatException) {
					Log.w(TAG, e)
				}
			}
		}

		for (plugin in pluginRegistry.allPlugins) {
			plugin.onOrblitSetupCompleted()
		}
		primaryHost?.onOrblitSetupCompleted()
	}

	/**
	 * Invoked on the render thread when the Orblit main loop has started.
	 */
	internal fun onOrblitMainLoopStarted() {
		Log.v(TAG, "OnOrblitMainLoopStarted")
		_runStatus.set(RunStatus.STARTED)

		accelerometerEnabled.set(java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("input_devices/sensors/enable_accelerometer")))
		gravityEnabled.set(java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("input_devices/sensors/enable_gravity")))
		gyroscopeEnabled.set(java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("input_devices/sensors/enable_gyroscope")))
		magnetometerEnabled.set(java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("input_devices/sensors/enable_magnetometer")))
		deviceOrientationEnabled.set(java.lang.Boolean.parseBoolean(OrblitLib.getGlobal("input_devices/sensors/enable_device_orientation")))

		runOnHostThread {
			// Used to report 'Time to full display' metrics.
			// See https://developer.android.com/topic/performance/vitals/launch-time#time-full for more info.
			getActivity()?.reportFullyDrawn()

			registerSensorsIfNeeded()
		}

		for (plugin in pluginRegistry.allPlugins) {
			plugin.onOrblitMainLoopStarted()
		}
		primaryHost?.onOrblitMainLoopStarted()
	}

	/**
	 * Invoked on the render thread when the engine is about to terminate.
	 */
	internal fun onOrblitTerminating() {
		Log.v(TAG, "OnOrblitTerminating")
		_runStatus.set(RunStatus.TERMINATING)

		for (plugin in pluginRegistry.allPlugins) {
			plugin.onOrblitTerminating()
		}
		runOnTerminate.get()?.run()
	}


	fun alert(
		@StringRes messageResId: Int,
		@StringRes titleResId: Int,
		okCallback: Runnable?
	) {
		val res: Resources = context.resources ?: return
		alert(res.getString(messageResId), res.getString(titleResId), okCallback)
	}

	@JvmOverloads
	fun alert(message: String, title: String, okCallback: Runnable? = null) {
		val renderLatch = CountDownLatch(1)
		runOnHostThread {
			val activity = getActivity()
			if (activity == null) {
				renderLatch.countDown()
				return@runOnHostThread
			}

			try {
				val builder = AlertDialog.Builder(activity)
				builder.setMessage(message).setTitle(title)
				builder.setPositiveButton(
					R.string.dialog_ok
				) { dialog: DialogInterface, _: Int ->
					okCallback?.run()
					dialog.cancel()
					renderLatch.countDown()
				}
				builder.setOnCancelListener {
					renderLatch.countDown()
				}
				val dialog = builder.create()
				dialog.show()
			} catch (e: WindowManager.BadTokenException) {
				// fallback in case the activity state changes before show().
				renderLatch.countDown()
			}
		}

		// We only block the render thread.
		val blockerRunnable = Runnable {
			try {
				renderLatch.await()
			} catch (_: InterruptedException) {}
		}
		if (Thread.currentThread() == Looper.getMainLooper().thread) {
			runOnRenderThread(blockerRunnable)
		} else {
			blockerRunnable.run()
		}
	}

	/**
	 * Queue a runnable to be run on the render thread.
	 *
	 * This must be called after the render thread has started.
	 */
	fun runOnRenderThread(action: Runnable) {
		renderView?.queueOnRenderThread(action)
	}

	/**
	 * Runs the specified action on the host thread.
	 */
	fun runOnHostThread(action: Runnable) {
		primaryHost?.runOnHostThread(action)
	}

/**
	 * Returns the native rendering driver.
	 */
	private fun getNativeRenderer(): String {
		val rendererInfo = OrblitLib.getRendererInfo(meetsVulkanRequirements(context.packageManager))
		val renderingDriverChosen = rendererInfo[0]
		val renderingDriverOriginal = rendererInfo[1]
		val renderingMethod = rendererInfo[2]
		val renderingDriverSource = rendererInfo[3]
		val renderingMethodSource = rendererInfo[4]
		Log.d(TAG, """renderingDevice: $renderingDriverChosen (${renderingDriverSource})
			renderer: $renderingMethod (${renderingMethodSource})""")

		if (renderingDriverOriginal == "vulkan" && renderingDriverChosen == "") {
			// Throw the exception for the case where Vulkan failed to create and no fallback was available.
			throw IllegalStateException(context.getString(R.string.error_missing_vulkan_requirements_message))
		}

		return renderingDriverChosen
	}

	/**
	 * Returns true if the device meets the base requirements for Vulkan support, false otherwise.
	 */
	private fun meetsVulkanRequirements(packageManager: PackageManager?): Boolean {
		if (packageManager == null) {
			return false
		}
		if (!packageManager.hasSystemFeature(PackageManager.FEATURE_VULKAN_HARDWARE_LEVEL, 1)) {
			// Optional requirements… log as warning if missing
			Log.w(TAG, "The vulkan hardware level does not meet the minimum requirement: 1")
		}

		// Check for api version 1.1
		return packageManager.hasSystemFeature(PackageManager.FEATURE_VULKAN_HARDWARE_VERSION, 0x401000)
	}

	internal fun setKeepScreenOn(enabled: Boolean) {
		runOnHostThread {
			if (enabled) {
				getActivity()?.window?.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
			} else {
				getActivity()?.window?.clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
			}
		}
	}

	@Keep
	fun hasClipboard(): Boolean {
		return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P || Looper.getMainLooper().thread == Thread.currentThread()) {
			hasClipboardCallable.call()
		} else {
			val task = FutureTask(hasClipboardCallable)
			runOnHostThread(task)
			task.get()
		}
	}

	@Keep
	fun getClipboard(): String {
		return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P || Looper.getMainLooper().thread == Thread.currentThread()) {
			getClipboardCallable.call()
		} else {
			val task = FutureTask(getClipboardCallable)
			runOnHostThread(task)
			task.get()
		}
	}

	@Keep
	fun setClipboard(text: String?) {
		runOnHostThread {
			mClipboard?.setPrimaryClip(ClipData.newPlainText("myLabel", text))
		}
	}

	/**
	 * Destroys the Orblit Engine and kill the process it's running in.
	 */
	@JvmOverloads
	fun destroyAndKillProcess(destroyRunnable: Runnable? = null) {
		val host = primaryHost
		if (host == null) {
			// Run the destroyRunnable right away as we are about to force quit.
			destroyRunnable?.run()

			// Fallback to force quit
			forceQuit(0)
			return
		}

		// Store the destroyRunnable so it can be run when the engine is terminating
		runOnTerminate.set(destroyRunnable)

		runOnHostThread {
			onDestroy(host)
		}
	}

	internal fun forceQuit(instanceId: Int): Boolean {
		primaryHost?.let {
			if (instanceId == 0) {
				it.onOrblitForceQuit(this)
				return true
			} else {
				return it.onOrblitForceQuit(instanceId)
			}
		}
		return false
	}

	fun onBackPressed() {
		for (plugin in pluginRegistry.allPlugins) {
			Log.v(TAG, "Invoking onMainBackPressed for plugin ${plugin.pluginName}")
			plugin.onMainBackPressed()
		}
		runOnRenderThread { OrblitLib.back() }
	}

	fun requestPermission(name: String?): Boolean {
		val activity = getActivity() ?: return false
		return requestPermission(name, activity)
	}

	fun requestPermissions(): Boolean {
		return PermissionsUtil.requestManifestPermissions(getActivity())
	}

	fun getGrantedPermissions(): Array<String?>? {
		return PermissionsUtil.getGrantedPermissions(context)
	}

	/**
	 * Returns true if this is the Orblit editor.
	 */
	fun isEditorHint() = isEditorBuild() && OrblitLib.isEditorHint()

	/**
	 * Returns true if this is the Orblit project manager.
	 */
	fun isProjectManagerHint() = isEditorBuild() && OrblitLib.isProjectManagerHint()

	/**
	 * Returns true if the feature for the given feature tag is supported in the currently running instance, depending
	 * on the platform, build, etc.
	 */
	fun hasFeature(feature: String): Boolean {
		return OrblitLib.hasFeature(feature)
	}
}
