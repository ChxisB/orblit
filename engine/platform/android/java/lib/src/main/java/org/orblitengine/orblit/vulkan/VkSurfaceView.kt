/**************************************************************************/
/*  VkSurfaceView.kt                                                      */

@file:JvmName("VkSurfaceView")
package org.orblitengine.orblit.vulkan

import android.content.Context
import android.util.Log
import android.view.SurfaceHolder
import android.view.SurfaceView

/**
 * An implementation of SurfaceView that uses the dedicated surface for
 * displaying Vulkan rendering.
 * <p>
 * A [VkSurfaceView] provides the following features:
 * <p>
 * <ul>
 * <li>Manages a surface, which is a special piece of memory that can be
 * composited into the Android view system.
 * <li>Accepts a user-provided [VkRenderer] object that does the actual rendering.
 * <li>Renders on a dedicated [VkThread] thread to decouple rendering performance from the
 * UI thread.
 * </ul>
 */
open internal class VkSurfaceView(context: Context) : SurfaceView(context), SurfaceHolder.Callback {
	companion object {
		private val TAG = VkSurfaceView::class.java.simpleName

		fun checkState(expression: Boolean, errorMessage: Any) {
			check(expression) { errorMessage.toString() }
		}
	}

	/**
	 * Thread used to drive the vulkan logic.
	 */
	private val vkThread: VkThread by lazy {
		VkThread(this, renderer)
	}

	/**
	 * Performs the actual rendering.
	 */
	private lateinit var renderer: VkRenderer

	init {
		isClickable = true
		holder.addCallback(this)
	}

	/**
	 * Set the [VkRenderer] associated with the view, and starts the thread that will drive the vulkan
	 * rendering.
	 *
	 * This method should be called once and only once in the life-cycle of [VkSurfaceView].
	 */
	fun startRenderer(renderer: VkRenderer) {
		checkState(!this::renderer.isInitialized, "startRenderer must only be invoked once")
		this.renderer = renderer
		vkThread.start()
	}

	/**
	 * Queues a runnable to be run on the Vulkan rendering thread.
	 *
	 * Must not be called before a [VkRenderer] has been set.
	 */
	fun queueOnVkThread(runnable: Runnable) {
		vkThread.queueEvent(runnable)
	}

	/**
	 * Resumes the rendering thread.
	 *
	 * Must not be called before a [VkRenderer] has been set.
	 */
	protected fun resumeRenderThread() {
		vkThread.onResume()
	}

	/**
	 * Pauses the rendering thread.
	 *
	 * Must not be called before a [VkRenderer] has been set.
	 */
	protected fun pauseRenderThread() {
		vkThread.onPause()
	}

	/**
	 * Requests the render thread to exit and block until it does.
	 */
	fun requestRenderThreadExitAndWait() {
		vkThread.requestExitAndWait()
	}

	/**
	 * Requests the render thread to exit and block up to the given [timeInMs] until it's done.
	 *
	 * @return true if the thread exited, false otherwise.
	 */
	fun requestRenderThreadExitAndWait(timeInMs: Long): Boolean {
		return vkThread.requestExitAndWait(timeInMs)
	}

	override fun surfaceChanged(holder: SurfaceHolder, format: Int, width: Int, height: Int) {
		Log.v(TAG, "On surface changed [$format]: $width x $height")
		vkThread.onSurfaceChanged(width, height)
	}

	override fun surfaceDestroyed(holder: SurfaceHolder) {
		Log.v(TAG, "On surface destroyed")
		vkThread.onSurfaceDestroyed()
	}

	override fun surfaceCreated(holder: SurfaceHolder) {
		Log.v(TAG, "On surface created")
		vkThread.onSurfaceCreated()
	}
}
