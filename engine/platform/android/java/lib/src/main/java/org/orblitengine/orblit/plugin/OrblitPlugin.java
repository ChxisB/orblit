/**************************************************************************/
/*  OrblitPlugin.java                                                     */

package org.orblitengine.orblit.plugin;

import org.orblitengine.orblit.BuildConfig;
import org.orblitengine.orblit.Orblit;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.util.Log;
import android.view.Surface;
import android.view.View;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import java.lang.reflect.Method;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

/**
 * Base class for Orblit Android plugins.
 * <p>
 * A Orblit Android plugin is an Android library with the following requirements:
 * <p>
 * - The plugin must have a dependency on the Orblit Android library: `implementation "org.orblitengine:orblit:<orblitLibVersion>"`
 * <p>
 * - The plugin must include a <meta-data> tag in its Android manifest with the following format:
 * <meta-data android:name="org.orblitengine.plugin.v2.[PluginName]" android:value="[plugin.init.ClassFullName]" />
 * <p>
 * Where:
 * <p>
 * - 'PluginName' is the name of the plugin.
 * <p>
 * - 'plugin.init.ClassFullName' is the full name (package + class name) of the plugin init class
 * extending {@link OrblitPlugin}.
 * <p>
 * A Orblit Android plugin can also define and provide c/c++ native_extension libraries, which will be
 * automatically bundled by the aar build system.
 * NativeExtension ('*.oext') config files must be located in the project 'assets' directory and
 * their paths specified by {@link OrblitPlugin#getPluginNativeExtensionLibrariesPaths()}.
 */
public abstract class OrblitPlugin {
	private static final String TAG = OrblitPlugin.class.getSimpleName();

	private final Orblit orblit;
	private final ConcurrentHashMap<String, SignalInfo> registeredSignals = new ConcurrentHashMap<>();

	/**
	 * Base constructor passing a {@link Orblit} instance through which the plugin can access Orblit's
	 * APIs and lifecycle events.
	 */
	public OrblitPlugin(Orblit orblit) {
		this.orblit = orblit;
	}

	/**
	 * Provides access to the Orblit engine.
	 */
	protected Orblit getOrblit() {
		return orblit;
	}

	/**
	 * Provides access to the hosting {@link Activity}.
	 */
	@Nullable
	protected Activity getActivity() {
		return orblit.getActivity();
	}

	/**
	 * Provides access to the {@link Context}.
	 */
	protected Context getContext() {
		return orblit.getContext();
	}

	/**
	 * Register the plugin with Orblit native code.
	 * <p>
	 * This method is invoked on the render thread to register the plugin on engine startup.
	 */
	public final void onRegisterPluginWithOrblitNative() {
		final String pluginName = getPluginName();
		if (!nativeRegisterSingleton(pluginName, this)) {
			return;
		}

		List<String> pluginMethods = getPluginMethods();

		Set<Method> filteredMethods = new HashSet<>();
		Class<?> clazz = getClass();

		Method[] methods = clazz.getDeclaredMethods();
		for (Method method : methods) {
			// Check if the method is annotated with {@link UsedByOrblit}.
			if (method.getAnnotation(UsedByOrblit.class) != null) {
				filteredMethods.add(method);
			} else {
				// For backward compatibility, process the methods from the given <pluginMethods> argument.
				for (String methodName : pluginMethods) {
					if (methodName.equals(method.getName())) {
						filteredMethods.add(method);
						break;
					}
				}
			}
		}

		for (Method method : filteredMethods) {
			List<String> ptr = new ArrayList<>();

			Class<?>[] paramTypes = method.getParameterTypes();
			for (Class<?> c : paramTypes) {
				ptr.add(c.getName());
			}

			String[] pt = new String[ptr.size()];
			ptr.toArray(pt);

			nativeRegisterMethod(pluginName, method.getName(), method.getReturnType().getName(), pt);
		}

		Set<SignalInfo> pluginSignals = getPluginSignals();

		// Register the signals for this plugin.
		for (SignalInfo signalInfo : pluginSignals) {
			String signalName = signalInfo.getName();
			nativeRegisterSignal(pluginName, signalName, signalInfo.getParamTypesNames());
			registeredSignals.put(signalName, signalInfo);
		}
	}

	/**
	 * Invoked once during the initialization process after creation of the
	 * {@link org.orblitengine.orblit.OrblitRenderView} view.
	 * <p>
	 * The plugin can return a non-null {@link View} layout which will be added to the Orblit view
	 * hierarchy.
	 * <p>
	 * Use {@link OrblitPlugin#shouldBeOnTop()} to specify whether the plugin's {@link View} should
	 * be added on top or behind the main Orblit view.
	 *
	 * @see Activity#onCreate(Bundle)
	 * @return the plugin's view to be included; null if no views should be included.
	 */
	@Nullable
	public View onMainCreate(@Nullable Activity activity) {
		return null;
	}

	/**
	 * @see Activity#onActivityResult(int, int, Intent)
	 */
	public void onMainActivityResult(int requestCode, int resultCode, Intent data) {
	}

	/**
	 * @see Activity#onRequestPermissionsResult(int, String[], int[])
	 */
	public void onMainRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
	}

	/**
	 * @see Activity#onPause()
	 */
	public void onMainPause() {}

	/**
	 * @see Activity#onStop()
	 */
	public void onMainStop() {}

	/**
	 * @see Activity#onStart()
	 */
	public void onMainStart() {}

	/**
	 * @see Activity#onResume()
	 */
	public void onMainResume() {}

	/**
	 * @see Activity#onDestroy()
	 */
	public void onMainDestroy() {}

	/**
	 * @see Activity#onBackPressed()
	 */
	public boolean onMainBackPressed() { return false; }

	/**
	 * Invoked on the render thread when set up of the Orblit engine is complete.
	 * <p>
	 * This is invoked before {@link OrblitPlugin#onOrblitMainLoopStarted()}.
	 */
	public void onOrblitSetupCompleted() {}

	/**
	 * Invoked on the render thread when the Orblit main loop has started.
	 *
	 * This is invoked after {@link OrblitPlugin#onOrblitSetupCompleted()}.
	 */
	public void onOrblitMainLoopStarted() {}

	/**
	 * Invoked on the render thread when the Orblit engine is terminating.
	 */
	public void onOrblitTerminating() {}

	/**
	 * When using the OpenGL renderer, this is invoked once per frame on the GL thread after the
	 * frame is drawn.
	 */
	public void onGLDrawFrame(GL10 gl) {}

	/**
	 * When using the OpenGL renderer, this is called on the GL thread after the surface is created
	 * and whenever the OpenGL ES surface size changes.
	 */
	public void onGLSurfaceChanged(GL10 gl, int width, int height) {}

	/**
	 * When using the OpenGL renderer, this is called on the GL thread when the surface is created
	 * or recreated.
	 */
	public void onGLSurfaceCreated(GL10 gl, EGLConfig config) {}

	/**
	 * When using the Vulkan renderer, this is invoked once per frame on the Vulkan thread after
	 * the frame is drawn.
	 */
	public void onVkDrawFrame() {}

	/**
	 * When using the Vulkan renderer, this is called on the Vulkan thread after the surface is
	 * created and whenever the surface size changes.
	 */
	public void onVkSurfaceChanged(Surface surface, int width, int height) {}

	/**
	 * When using the Vulkan renderer, this is called on the Vulkan thread when the surface is
	 * created or recreated.
	 */
	public void onVkSurfaceCreated(Surface surface) {}

	/**
	 * Return the list of command line parameters that should be passed to Orblit.
	 * <p>
	 * When invoking this method, the engine passes the original set of command line params it was started with,
	 * which can be used by the plugin to enable / disable its own feature(s).
	 * The given set of command line params is unmodifiable and does not include command line parameters passed by other plugins.
	 *
	 * @param unmodifiableOriginalCommandLineParams Original set of command line params the engine was started with.
	 * @return a new set of command line parameters to be passed to the engine.
	 */
	@NonNull
	public List<String> getCommandLineParams(List<String> unmodifiableOriginalCommandLineParams) {
		return Collections.emptyList();
	}

	@Override
	public String toString() {
		return getPluginName();
	}

	/**
	 * Returns the name of the plugin.
	 * <p>
	 * This value must match the one listed in the plugin '<meta-data>' manifest entry.
	 */
	@NonNull
	public abstract String getPluginName();

	/**
	 * Returns the list of methods to be exposed to Orblit.
	 *
	 * @deprecated Use the {@link UsedByOrblit} annotation instead.
	 */
	@NonNull
	@Deprecated
	public List<String> getPluginMethods() {
		return Collections.emptyList();
	}

	/**
	 * Returns the list of signals to be exposed to Orblit.
	 */
	@NonNull
	public Set<SignalInfo> getPluginSignals() {
		return Collections.emptySet();
	}

	/**
	 * Returns the paths for the plugin's native_extension libraries (if any).
	 * <p>
	 * Each returned path must be relative to the 'assets' directory and point to a '*.oext' file.
	 */
	@NonNull
	public Set<String> getPluginNativeExtensionLibrariesPaths() {
		return Collections.emptySet();
	}

	/**
	 * Returns whether the plugin's {@link View} returned in
	 * {@link OrblitPlugin#onMainCreate(Activity)} should be placed on top of the main Orblit view.
	 * <p>
	 * Returning false causes the plugin's {@link View} to be placed behind, which can be useful
	 * when used with transparency in order to let the Orblit view handle inputs.
	 */
	public boolean shouldBeOnTop() {
		return true;
	}

	/**
	 * Returns whether the plugin supports the given feature tag.
	 */
	public boolean supportsFeature(String featureTag) {
		return false;
	}

	/**
	 * Runs the specified action on the host thread.
	 *
	 * @param action the action to run on the host thread
	 *
	 * @deprecated Use the {@link OrblitPlugin#runOnHostThread} instead.
	 */
	@Deprecated
	protected void runOnUiThread(Runnable action) {
		runOnHostThread(action);
	}

	/**
	 * Runs the specified action on the host thread.
	 *
	 * @param action the action to run on the host thread
	 */
	protected void runOnHostThread(Runnable action) {
		orblit.runOnHostThread(action);
	}

	/**
	 * Queue the specified action to be run on the render thread.
	 *
	 * @param action the action to run on the render thread
	 */
	protected void runOnRenderThread(Runnable action) {
		orblit.runOnRenderThread(action);
	}

	/**
	 * Emit a registered Orblit signal.
	 * @param signalName Name of the signal to emit. It will be validated against the set of registered signals.
	 * @param signalArgs Arguments used to populate the emitted signal. The arguments will be validated against the registered {@link SignalInfo} matching the signalName parameter.
	 */
	protected void emitSignal(final String signalName, final Object... signalArgs) {
		try {
			// Check that the given signal is among the registered set.
			SignalInfo signalInfo = registeredSignals.get(signalName);
			if (signalInfo == null) {
				throw new IllegalArgumentException(
						"Signal " + signalName + " is not registered for this plugin.");
			}
			emitSignal(getOrblit(), getPluginName(), signalInfo, signalArgs);
		} catch (IllegalArgumentException exception) {
			Log.w(TAG, exception);
			if (BuildConfig.DEBUG) {
				throw exception;
			}
		}
	}

	/**
	 * Emit a registered Orblit signal.
	 * @param signal Signal to emit. It will be validated against the set of registered signals.
	 * @param signalArgs Arguments used to populate the emitted signal. The arguments will be validated against the registered {@link SignalInfo} matching the signal parameter.
	 */
	protected void emitSignal(SignalInfo signal, final Object... signalArgs) {
		emitSignal(signal.getName(), signalArgs);
	}

	/**
	 * Emit a Orblit signal.
	 * @param orblit Orblit instance
	 * @param pluginName Name of the Orblit plugin the signal will be emitted from. The plugin must already be registered with the Orblit engine.
	 * @param signalInfo Information about the signal to emit.
	 * @param signalArgs Arguments used to populate the emitted signal. The arguments will be validated against the given {@link SignalInfo} parameter.
	 */
	public static void emitSignal(Orblit orblit, String pluginName, SignalInfo signalInfo, final Object... signalArgs) {
		try {
			if (signalInfo == null) {
				throw new IllegalArgumentException("Signal must be non null.");
			}

			// Validate the arguments count.
			Class<?>[] signalParamTypes = signalInfo.getParamTypes();
			if (signalArgs.length != signalParamTypes.length) {
				throw new IllegalArgumentException(
						"Invalid arguments count. Should be " + signalParamTypes.length + "  but is " + signalArgs.length);
			}

			// Validate the argument's types.
			for (int i = 0; i < signalParamTypes.length; i++) {
				Object signalArg = signalArgs[i];
				if (signalArg != null && !signalParamTypes[i].isInstance(signalArg)) {
					throw new IllegalArgumentException(
							"Invalid type for argument #" + i + ". Should be of type '" + signalParamTypes[i].getName() + "' but is of type '" + signalArg.getClass().getName() + "'.");
				}
			}

			orblit.runOnRenderThread(() -> nativeEmitSignal(pluginName, signalInfo.getName(), signalArgs));

		} catch (IllegalArgumentException exception) {
			Log.w(TAG, exception);
			if (BuildConfig.DEBUG) {
				throw exception;
			}
		}
	}

	/**
	 * Used to setup a {@link OrblitPlugin} instance.
	 * @param p_name Name of the instance.
	 */
	private static native boolean nativeRegisterSingleton(String p_name, Object object);

	/**
	 * Used to complete registration of the {@link OrblitPlugin} instance's methods.
	 * @param p_sname Name of the instance
	 * @param p_name Name of the method to register
	 * @param p_ret Return type of the registered method
	 * @param p_params Method parameters types
	 */
	private static native void nativeRegisterMethod(String p_sname, String p_name, String p_ret, String[] p_params);

	/**
	 * Used to complete registration of the {@link OrblitPlugin} instance's signals.
	 * @param pluginName Name of the plugin
	 * @param signalName Name of the signal to register
	 * @param signalParamTypes Signal parameters types
	 */
	private static native void nativeRegisterSignal(String pluginName, String signalName, String[] signalParamTypes);

	/**
	 * Used to emit signal by {@link OrblitPlugin} instance.
	 * @param pluginName Name of the plugin
	 * @param signalName Name of the signal to emit
	 * @param signalParams Signal parameters
	 */
	private static native void nativeEmitSignal(String pluginName, String signalName, Object[] signalParams);
}
