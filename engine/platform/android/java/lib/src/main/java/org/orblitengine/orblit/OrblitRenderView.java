/**************************************************************************/
/*  OrblitRenderView.java                                                 */

package org.orblitengine.orblit;

import org.orblitengine.orblit.input.OrblitInputHandler;

import android.view.SurfaceView;

public interface OrblitRenderView {
	SurfaceView getView();

	/**
	 * Starts the thread that will drive Orblit's rendering.
	 */
	void startRenderer();

	/**
	 * Queues a runnable to be run on the rendering thread.
	 */
	void queueOnRenderThread(Runnable event);

	void onActivityPaused();

	void onActivityStopped();

	void onActivityResumed();

	void onActivityStarted();

	boolean blockingExitRenderer(long blockingTimeInMs);

	OrblitInputHandler getInputHandler();

	void configurePointerIcon(int pointerType, String imagePath, float hotSpotX, float hotSpotY);

	void setPointerIcon(int pointerType);

	/**
	 * @return true if pointer capture is supported.
	 */
	boolean canCapturePointer();
}
