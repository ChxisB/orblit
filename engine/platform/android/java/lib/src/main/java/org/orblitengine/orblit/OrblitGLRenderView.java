/**************************************************************************/
/*  OrblitGLRenderView.java                                               */

package org.orblitengine.orblit;

import org.orblitengine.orblit.OrblitLib;
import org.orblitengine.orblit.gl.GLSurfaceView;
import org.orblitengine.orblit.gl.OrblitRenderer;
import org.orblitengine.orblit.input.OrblitInputHandler;
import org.orblitengine.orblit.xr.XRMode;
import org.orblitengine.orblit.xr.ovr.OvrConfigChooser;
import org.orblitengine.orblit.xr.ovr.OvrContextFactory;
import org.orblitengine.orblit.xr.ovr.OvrWindowSurfaceFactory;
import org.orblitengine.orblit.xr.regular.RegularConfigChooser;
import org.orblitengine.orblit.xr.regular.RegularContextFactory;
import org.orblitengine.orblit.xr.regular.RegularFallbackConfigChooser;

import android.annotation.SuppressLint;
import android.content.res.AssetManager;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.PixelFormat;
import android.os.Bundle;
import android.text.TextUtils;
import android.util.SparseArray;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.PointerIcon;
import android.view.SurfaceView;
import android.view.View;
import android.view.accessibility.AccessibilityNodeInfo;
import android.view.accessibility.AccessibilityNodeProvider;

import androidx.annotation.Keep;

import java.io.InputStream;

/**
 * A simple GLSurfaceView sub-class that demonstrate how to perform
 * OpenGL ES 2.0 rendering into a GL Surface. Note the following important
 * details:
 *
 * - The class must use a custom context factory to enable 2.0 rendering.
 *   See ContextFactory class definition below.
 *
 * - The class must use a custom EGLConfigChooser to be able to select
 *   an EGLConfig that supports 3.0. This is done by providing a config
 *   specification to eglChooseConfig() that has the attribute
 *   EGL10.ELG_RENDERABLE_TYPE containing the EGL_OPENGL_ES2_BIT flag
 *   set. See ConfigChooser class definition below.
 *
 * - The class must select the surface's format, then choose an EGLConfig
 *   that matches it exactly (with regards to red/green/blue/alpha channels
 *   bit depths). Failure to do so would result in an EGL_BAD_MATCH error.
 */
class OrblitGLRenderView extends GLSurfaceView implements OrblitRenderView {
	private final Orblit orblit;
	private final OrblitInputHandler inputHandler;
	private final OrblitRenderer orblitRenderer;
	private final SparseArray<PointerIcon> customPointerIcons = new SparseArray<>();
	private final AccessibilityNodeProvider accessibilityNodeProvider;
	private final long mWindowID = 0; // DisplaySystemEnums::MAIN_WINDOW_ID

	public OrblitGLRenderView(Orblit orblit, OrblitInputHandler inputHandler, XRMode xrMode, boolean useDebugOpengl, boolean shouldBeTranslucent) {
		super(orblit.getContext());
		if (OrblitLib.createAccessKitAdapter(mWindowID)) {
			accessibilityNodeProvider = new AccessibilityNodeProvider() {
				@Override
				public AccessibilityNodeInfo createAccessibilityNodeInfo(int virtualViewId) {
					return OrblitLib.createAccessibilityNodeInfo(mWindowID, getView(), virtualViewId);
				}
				@Override
				public AccessibilityNodeInfo findFocus(int focusType) {
					return OrblitLib.findAccessibilityFocus(mWindowID, getView(), focusType);
				}
				@Override
				public boolean performAction(int virtualViewId, int action, Bundle arguments) {
					return OrblitLib.performAccessibilityAction(mWindowID, getView(), virtualViewId, action, arguments);
				}
			};
			setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
		} else {
			accessibilityNodeProvider = null;
		}
		this.orblit = orblit;
		this.inputHandler = inputHandler;
		this.orblitRenderer = new OrblitRenderer();
		setPointerIcon(PointerIcon.getSystemIcon(getContext(), PointerIcon.TYPE_DEFAULT));
		init(xrMode, shouldBeTranslucent, useDebugOpengl);
	}

	@Override
	public boolean onHoverEvent(MotionEvent event) {
		if (accessibilityNodeProvider != null && OrblitLib.onAccessibilityHoverEvent(mWindowID, this, event.getAction(), event.getX(), event.getY())) {
			return true;
		}
		return super.onHoverEvent(event);
	}

	@Override
	public AccessibilityNodeProvider getAccessibilityNodeProvider() {
		if (accessibilityNodeProvider == null) {
			return super.getAccessibilityNodeProvider();
		}
		return accessibilityNodeProvider;
	}

	@Override
	protected void finalize() throws Throwable {
		try {
			OrblitLib.freeAccessKitAdapter(mWindowID);
		} finally {
			super.finalize();
		}
	}

	@Override
	public SurfaceView getView() {
		return this;
	}

	@Override
	public void queueOnRenderThread(Runnable event) {
		queueEvent(event);
	}

	@Override
	public void onActivityPaused() {
		queueEvent(() -> {
			OrblitLib.focusout();
			// Pause the renderer
			orblitRenderer.onActivityPaused();
		});
	}

	@Override
	public void onActivityStopped() {
		pauseGLThread();
	}

	@Override
	public void onActivityResumed() {
		queueEvent(() -> {
			// Resume the renderer
			orblitRenderer.onActivityResumed();
			OrblitLib.focusin();
		});
	}

	@Override
	public void onActivityStarted() {
		resumeGLThread();
	}

	@Override
	public boolean blockingExitRenderer(long blockingTimeInMs) {
		return requestRenderThreadExitAndWait(blockingTimeInMs);
	}

	@Override
	public OrblitInputHandler getInputHandler() {
		return inputHandler;
	}

	@SuppressLint("ClickableViewAccessibility")
	@Override
	public boolean onTouchEvent(MotionEvent event) {
		super.onTouchEvent(event);
		return inputHandler.onTouchEvent(event);
	}

	@Override
	public boolean onKeyUp(final int keyCode, KeyEvent event) {
		return inputHandler.onKeyUp(keyCode, event) || super.onKeyUp(keyCode, event);
	}

	@Override
	public boolean onKeyDown(final int keyCode, KeyEvent event) {
		return inputHandler.onKeyDown(keyCode, event) || super.onKeyDown(keyCode, event);
	}

	@Override
	public boolean onGenericMotionEvent(MotionEvent event) {
		return inputHandler.onGenericMotionEvent(event) || super.onGenericMotionEvent(event);
	}

	@Override
	public boolean onCapturedPointerEvent(MotionEvent event) {
		return inputHandler.onGenericMotionEvent(event);
	}

	@Override
	public void onPointerCaptureChange(boolean hasCapture) {
		super.onPointerCaptureChange(hasCapture);
		inputHandler.onPointerCaptureChange(hasCapture);
	}

	@Override
	public boolean canCapturePointer() {
		// Pointer capture is not supported on XR devices.
		return !orblit.isXrRuntime() && inputHandler.canCapturePointer();
	}

	@Override
	public void requestPointerCapture() {
		if (canCapturePointer()) {
			super.requestPointerCapture();
			inputHandler.onPointerCaptureChange(true);
		}
	}

	@Override
	public void releasePointerCapture() {
		super.releasePointerCapture();
		inputHandler.onPointerCaptureChange(false);
	}

	/**
	 * Used to configure the PointerIcon for the given type.
	 *
	 * Called from JNI
	 */
	@Keep
	@Override
	public void configurePointerIcon(int pointerType, String imagePath, float hotSpotX, float hotSpotY) {
		try {
			Bitmap bitmap = null;
			if (!TextUtils.isEmpty(imagePath)) {
				if (orblit.getDirectoryAccessHandler().filesystemFileExists(imagePath)) {
					// Try to load the bitmap from the file system
					bitmap = BitmapFactory.decodeFile(imagePath);
				} else if (orblit.getDirectoryAccessHandler().assetsFileExists(imagePath)) {
					// Try to load the bitmap from the assets directory
					AssetManager am = getContext().getAssets();
					InputStream imageInputStream = am.open(imagePath);
					bitmap = BitmapFactory.decodeStream(imageInputStream);
				}
			}

			PointerIcon customPointerIcon = PointerIcon.create(bitmap, hotSpotX, hotSpotY);
			customPointerIcons.put(pointerType, customPointerIcon);
		} catch (Exception e) {
			// Reset the custom pointer icon
			customPointerIcons.delete(pointerType);
		}
	}

	/**
	 * called from JNI to change pointer icon
	 */
	@Keep
	@Override
	public void setPointerIcon(int pointerType) {
		PointerIcon pointerIcon = customPointerIcons.get(pointerType);
		if (pointerIcon == null) {
			pointerIcon = PointerIcon.getSystemIcon(getContext(), pointerType);
		}
		setPointerIcon(pointerIcon);
	}

	@Override
	public PointerIcon onResolvePointerIcon(MotionEvent me, int pointerIndex) {
		return getPointerIcon();
	}

	private void init(XRMode xrMode, boolean translucent, boolean useDebugOpengl) {
		setPreserveEGLContextOnPause(true);
		setFocusableInTouchMode(true);
		switch (xrMode) {
			case OPENXR:
				// Replace the default egl config chooser.
				setEGLConfigChooser(new OvrConfigChooser());

				// Replace the default context factory.
				setEGLContextFactory(new OvrContextFactory());

				// Replace the default window surface factory.
				setEGLWindowSurfaceFactory(new OvrWindowSurfaceFactory());
				break;

			case REGULAR:
			default:
				/* By default, GLSurfaceView() creates a RGB_565 opaque surface.
				 * If we want a translucent one, we should change the surface's
				 * format here, using PixelFormat.TRANSLUCENT for GL Surfaces
				 * is interpreted as any 32-bit surface with alpha by SurfaceFlinger.
				 */
				if (translucent) {
					this.getHolder().setFormat(PixelFormat.TRANSLUCENT);
				}

				/* Setup the context factory for 2.0 rendering.
				 * See ContextFactory class definition below
				 */
				setEGLContextFactory(new RegularContextFactory(useDebugOpengl));

				/* We need to choose an EGLConfig that matches the format of
				 * our surface exactly. This is going to be done in our
				 * custom config chooser. See ConfigChooser class definition
				 * below.
				 */

				setEGLConfigChooser(
						new RegularFallbackConfigChooser(8, 8, 8, 8, 24, 0,
								new RegularConfigChooser(8, 8, 8, 8, 16, 0)));
				break;
		}
	}

	@Override
	public void startRenderer() {
		/* Set the renderer responsible for frame rendering */
		setRenderer(orblitRenderer);
	}
}
