/**************************************************************************/
/*  VkRenderer.kt                                                         */

@file:JvmName("VkRenderer")
package org.orblitengine.orblit.vulkan

import android.util.Log
import android.view.Surface
import org.orblitengine.orblit.OrblitLib
import org.orblitengine.orblit.plugin.OrblitPluginRegistry

/**
 * Responsible to setting up and driving the Vulkan rendering logic.
 *
 * <h3>Threading</h3>
 * The renderer will be called on a separate thread, so that rendering
 * performance is decoupled from the UI thread. Clients typically need to
 * communicate with the renderer from the UI thread, because that's where
 * input events are received. Clients can communicate using any of the
 * standard Java techniques for cross-thread communication, or they can
 * use the  [VkSurfaceView.queueOnVkThread] convenience method.
 *
 * @see [VkSurfaceView.startRenderer]
 */
internal class VkRenderer {

	companion object {
		private val TAG = VkRenderer::class.java.simpleName
	}

	private val pluginRegistry: OrblitPluginRegistry = OrblitPluginRegistry.getPluginRegistry()

	internal var initialized = false
		private set
	private var rendererResumed = false

	internal fun initialize(): Boolean {
		if (!initialized) {
			// Check the app is resumed to initialize the renderer.
			if (rendererResumed) {
				Log.v(TAG, "Initializing renderer...")
				initialized = true
			}
		}

		return initialized
	}

	/**
	 * Called when the surface is created and signals the beginning of rendering.
	 */
	fun onVkSurfaceCreated(surface: Surface) {
		OrblitLib.newcontext(surface)

		for (plugin in pluginRegistry.getAllPlugins()) {
			plugin.onVkSurfaceCreated(surface)
		}
	}

	/**
	 * Called after the surface is created and whenever its size changes.
	 */
	fun onVkSurfaceChanged(surface: Surface, width: Int, height: Int) {
		OrblitLib.resize(surface, width, height)

		for (plugin in pluginRegistry.getAllPlugins()) {
			plugin.onVkSurfaceChanged(surface, width, height)
		}
	}

	/**
	 * Called to draw the current frame.
	 */
	fun onVkDrawFrame() {
		OrblitLib.step()
		for (plugin in pluginRegistry.getAllPlugins()) {
			plugin.onVkDrawFrame()
		}
	}

	/**
	 * Called when the rendering thread is resumed.
	 */
	fun onVkResume() {
		Log.v(TAG, "Renderer resumed")
		rendererResumed = true
		OrblitLib.onRendererResumed()
	}

	/**
	 * Called when the rendering thread is paused.
	 */
	fun onVkPause() {
		Log.v(TAG, "Renderer paused")
		rendererResumed = false
		OrblitLib.onRendererPaused()
	}

	/**
	 * Invoked when the render thread is in the process of shutting down.
	 */
	fun onRenderThreadExiting() {
		Log.v(TAG, "Destroying Orblit Engine")
		OrblitLib.ondestroy()
	}
}
