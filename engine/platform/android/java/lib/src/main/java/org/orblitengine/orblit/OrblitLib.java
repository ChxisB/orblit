/**************************************************************************/
/*  OrblitLib.java                                                        */

package org.orblitengine.orblit;

import org.orblitengine.orblit.gl.OrblitRenderer;
import org.orblitengine.orblit.io.directory.DirectoryAccessHandler;
import org.orblitengine.orblit.io.file.FileAccessHandler;
import org.orblitengine.orblit.nativeapi.OrblitNativeBridge;
import org.orblitengine.orblit.tts.OrblitTTS;
import org.orblitengine.orblit.utils.OrblitNetUtils;
import org.orblitengine.orblit.variant.Callable;

import android.app.Activity;
import android.content.res.AssetManager;
import android.hardware.SensorEvent;
import android.os.Bundle;
import android.view.Surface;
import android.view.View;
import android.view.accessibility.AccessibilityNodeInfo;
import android.view.accessibility.AccessibilityNodeProvider;

import javax.microedition.khronos.opengles.GL10;

/**
 * Wrapper for native library
 */
public class OrblitLib {
	static {
		System.loadLibrary("orblit_android");
	}

	/**
	 * Invoked on the main thread to initialize Orblit native layer.
	 */
	public static native boolean initialize(
			OrblitNativeBridge nativeBridge,
			AssetManager assetManager,
			OrblitIO orblitIO,
			OrblitNetUtils netUtils,
			DirectoryAccessHandler directoryAccessHandler,
			FileAccessHandler fileAccessHandler,
			boolean useApkExpansion);

	/**
	 * Invoked on the main thread to clean up Orblit native layer.
	 * @see androidx.fragment.app.Fragment#onDestroy()
	 */
	public static native void ondestroy();

	/**
	 * Invoked on the GL thread to complete setup for the Orblit native layer logic.
	 * @param p_cmdline Command line arguments used to configure Orblit native layer components.
	 */
	public static native boolean setup(String[] p_cmdline, OrblitTTS tts);

	/**
	 * Invoked on the GL thread when the underlying Android surface has changed size.
	 * @param p_surface
	 * @param p_width
	 * @param p_height
	 * @see org.orblitengine.orblit.gl.GLSurfaceView.Renderer#onSurfaceChanged(GL10, int, int)
	 */
	public static native void resize(Surface p_surface, int p_width, int p_height);

	/**
	 * Invoked on the render thread when the underlying Android surface is created or recreated.
	 * @param p_surface
	 */
	public static native void newcontext(Surface p_surface);

	/**
	 * Forward {@link Activity#onBackPressed()} event.
	 */
	public static native void back();

	/**
	 * Invoked on the GL thread to draw the current frame.
	 * @see org.orblitengine.orblit.gl.GLSurfaceView.Renderer#onDrawFrame(GL10)
	 */
	public static native boolean step();

	/**
	 * TTS callback.
	 */
	public static native void ttsCallback(int event, long id, int pos);

	/**
	 * Accesskit.
	 */

	static native boolean createAccessKitAdapter(long windowID);
	static native void freeAccessKitAdapter(long windowID);
	static native AccessibilityNodeInfo createAccessibilityNodeInfo(long windowID, View host, int virtualViewId);
	static native AccessibilityNodeInfo findAccessibilityFocus(long windowID, View host, int focusType);
	static native boolean performAccessibilityAction(long windowID, View host, int virtualViewId, int action, Bundle arguments);
	static native boolean onAccessibilityHoverEvent(long windowID, View host, int action, float x, float y);

	/**
	 * Forward touch events.
	 */
	public static native void dispatchTouchEvent(int event, int pointer, int pointerCount, float[] positions, boolean doubleTap, boolean longPress);

	/**
	 * Dispatch mouse events
	 */
	public static native void dispatchMouseEvent(int event, int buttonMask, float x, float y, float deltaX, float deltaY, boolean doubleClick, boolean sourceMouseRelative, float pressure, float tiltX, float tiltY, boolean emulated);

	public static native void magnify(float x, float y, float factor);

	public static native void pan(float x, float y, float deltaX, float deltaY);

	/**
	 * Forward accelerometer sensor events.
	 * @see android.hardware.SensorEventListener#onSensorChanged(SensorEvent)
	 */
	public static native void accelerometer(float x, float y, float z);

	/**
	 * Forward gravity sensor events.
	 * @see android.hardware.SensorEventListener#onSensorChanged(SensorEvent)
	 */
	public static native void gravity(float x, float y, float z);

	/**
	 * Forward magnetometer sensor events.
	 * @see android.hardware.SensorEventListener#onSensorChanged(SensorEvent)
	 */
	public static native void magnetometer(float x, float y, float z);

	/**
	 * Forward gyroscope sensor events.
	 * @see android.hardware.SensorEventListener#onSensorChanged(SensorEvent)
	 */
	public static native void gyroscope(float x, float y, float z);

	/**
	 * Forward device orientation (rotation vector) events as a quaternion.
	 * @see android.hardware.SensorEventListener#onSensorChanged(SensorEvent)
	 */
	public static native void deviceOrientation(float x, float y, float z, float w);

	/**
	 * Forward regular key events.
	 */
	public static native void key(int p_physical_keycode, int p_unicode, int p_key_label, boolean p_pressed, boolean p_echo);

	/**
	 * Forward game device's key events.
	 */
	public static native void joybutton(int p_device, int p_but, boolean p_pressed);

	/**
	 * Forward joystick devices axis motion events.
	 */
	public static native void joyaxis(int p_device, int p_axis, float p_value);

	/**
	 * Forward joystick devices hat motion events.
	 */
	public static native void joyhat(int p_device, int p_hat_x, int p_hat_y);

	/**
	 * Fires when a joystick device is added or removed.
	 */
	public static native void joyconnectionchanged(int p_device, boolean p_connected, String p_name);

	/**
	 * Invoked when the Android app resumes.
	 * @see androidx.fragment.app.Fragment#onResume()
	 */
	public static native void focusin();

	/**
	 * Invoked when the Android app pauses.
	 * @see androidx.fragment.app.Fragment#onPause()
	 */
	public static native void focusout();

	/**
	 * Used to access Orblit global properties.
	 * @param p_key Property key
	 * @return String value of the property
	 */
	public static native String getGlobal(String p_key);

	/**
	 * Used to get info about the current rendering system.
	 *
	 * @return A String array with three elements:
	 *         [0] Rendering driver name chosen for rendering.
	 *         [1] Rendering driver name chosen before any fallbacks were applied.
	 *         [2] Rendering method.
	 *         [3] Source where the rendering driver was chosen from.
	 *         [4] Source where the rendering method was chosen from.
	 */
	public static native String[] getRendererInfo(boolean p_vulkan_requirements_met);

	/**
	 * Used to access Orblit's editor settings.
	 * @param settingKey Setting key
	 * @return String value of the setting
	 */
	public static native String getEditorSetting(String settingKey);

	/**
	 * Update the 'key' editor setting with the given data. Must be called on the render thread.
	 * @param key
	 * @param data
	 */
	public static native void setEditorSetting(String key, Object data);

	/**
	 * Used to access project metadata from the editor settings. Must be accessed on the render thread.
	 * @param section
	 * @param key
	 * @param defaultValue
	 * @return
	 */
	public static native Object getEditorProjectMetadata(String section, String key, Object defaultValue);

	/**
	 * Set the project metadata to the editor settings. Must be accessed on the render thread.
	 * @param section
	 * @param key
	 * @param data
	 */
	public static native void setEditorProjectMetadata(String section, String key, Object data);

	/**
	 * Invoke method |p_method| on the Orblit object specified by |p_id|
	 * @param p_id Id of the Orblit object to invoke
	 * @param p_method Name of the method to invoke
	 * @param p_params Parameters to use for method invocation
	 *
	 * @deprecated Use {@link Callable#call(long, String, Object...)} instead.
	 */
	@Deprecated
	public static void callobject(long p_id, String p_method, Object[] p_params) {
		Callable.call(p_id, p_method, p_params);
	}

	/**
	 * Invoke method |p_method| on the Orblit object specified by |p_id| during idle time.
	 * @param p_id Id of the Orblit object to invoke
	 * @param p_method Name of the method to invoke
	 * @param p_params Parameters to use for method invocation
	 *
	 * @deprecated Use {@link Callable#callDeferred(long, String, Object...)} instead.
	 */
	@Deprecated
	public static void calldeferred(long p_id, String p_method, Object[] p_params) {
		Callable.callDeferred(p_id, p_method, p_params);
	}

	/**
	 * Forward the results from a permission request.
	 * @see Activity#onRequestPermissionsResult(int, String[], int[])
	 * @param p_permission Request permission
	 * @param p_result True if the permission was granted, false otherwise
	 */
	public static native void requestPermissionResult(String p_permission, boolean p_result);

	/**
	 * Invoked on the theme light/dark mode change.
	 */
	public static native void onNightModeChanged();

	/**
	 * Invoked on the hardware keyboard connected/disconnected.
	 */
	public static native void hardwareKeyboardConnected(boolean connected);

	/**
	 * Invoked on the file picker closed.
	 */
	public static native void filePickerCallback(boolean p_ok, String[] p_selected_paths);

	/**
	 * Invoked on the GL thread to configure the height of the virtual keyboard.
	 */
	public static native void setVirtualKeyboardHeight(int p_height);

	/**
	 * Invoked on the GL thread when the {@link OrblitRenderer} has been resumed.
	 * @see OrblitRenderer#onActivityResumed()
	 */
	public static native void onRendererResumed();

	/**
	 * Invoked on the GL thread when the {@link OrblitRenderer} has been paused.
	 * @see OrblitRenderer#onActivityPaused()
	 */
	public static native void onRendererPaused();

	/**
	 * Invoked when the screen orientation changes.
	 * @param orientation the new screen orientation
	 */
	static native void onOrientationChange(int orientation);

	/**
	 * @return true if input must be dispatched from the render thread. If false, input is
	 * dispatched from the UI thread.
	 */
	public static native boolean shouldDispatchInputToRenderThread();

	/**
	 * @return the project resource directory
	 */
	public static native String getProjectResourceDir();

	static native boolean isEditorHint();

	static native boolean isProjectManagerHint();

	static native boolean hasFeature(String feature);

	static native void onPictureInPictureModeChanged(boolean isInPictureInPictureMode);
}
