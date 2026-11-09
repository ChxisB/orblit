/**************************************************************************/
/*  OrblitVulkanRenderView.java                                           */

package org.orblitengine.orblit;

import org.orblitengine.orblit.OrblitLib;
import org.orblitengine.orblit.input.OrblitInputHandler;
import org.orblitengine.orblit.vulkan.VkRenderer;
import org.orblitengine.orblit.vulkan.VkSurfaceView;

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

class OrblitVulkanRenderView extends VkSurfaceView implements OrblitRenderView {
	private final Orblit orblit;
	private final OrblitInputHandler mInputHandler;
	private final VkRenderer mRenderer;
	private final SparseArray<PointerIcon> customPointerIcons = new SparseArray<>();

	private final AccessibilityNodeProvider accessibilityNodeProvider;
	private final long mWindowID = 0; // DisplaySystemEnums::MAIN_WINDOW_ID

	public OrblitVulkanRenderView(Orblit orblit, OrblitInputHandler inputHandler, boolean shouldBeTranslucent) {
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
		mInputHandler = inputHandler;
		mRenderer = new VkRenderer();
		setPointerIcon(PointerIcon.getSystemIcon(getContext(), PointerIcon.TYPE_DEFAULT));
		setFocusableInTouchMode(true);
		setClickable(false);

		if (shouldBeTranslucent) {
			this.getHolder().setFormat(PixelFormat.TRANSLUCENT);
		}
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
	public void startRenderer() {
		startRenderer(mRenderer);
	}

	@Override
	public SurfaceView getView() {
		return this;
	}

	@Override
	public void queueOnRenderThread(Runnable event) {
		queueOnVkThread(event);
	}

	@Override
	public void onActivityPaused() {
		queueOnVkThread(() -> {
			OrblitLib.focusout();
			// Pause the renderer
			mRenderer.onVkPause();
		});
	}

	@Override
	public void onActivityStopped() {
		pauseRenderThread();
	}

	@Override
	public void onActivityStarted() {
		resumeRenderThread();
	}

	@Override
	public void onActivityResumed() {
		queueOnVkThread(() -> {
			// Resume the renderer
			mRenderer.onVkResume();
			OrblitLib.focusin();
		});
	}

	@Override
	public boolean blockingExitRenderer(long blockingTimeInMs) {
		return requestRenderThreadExitAndWait(blockingTimeInMs);
	}

	@Override
	public OrblitInputHandler getInputHandler() {
		return mInputHandler;
	}

	@SuppressLint("ClickableViewAccessibility")
	@Override
	public boolean onTouchEvent(MotionEvent event) {
		super.onTouchEvent(event);
		return mInputHandler.onTouchEvent(event);
	}

	@Override
	public boolean onKeyUp(final int keyCode, KeyEvent event) {
		return mInputHandler.onKeyUp(keyCode, event) || super.onKeyUp(keyCode, event);
	}

	@Override
	public boolean onKeyDown(final int keyCode, KeyEvent event) {
		return mInputHandler.onKeyDown(keyCode, event) || super.onKeyDown(keyCode, event);
	}

	@Override
	public boolean onGenericMotionEvent(MotionEvent event) {
		return mInputHandler.onGenericMotionEvent(event) || super.onGenericMotionEvent(event);
	}

	@Override
	public boolean onCapturedPointerEvent(MotionEvent event) {
		return mInputHandler.onGenericMotionEvent(event);
	}

	@Override
	public boolean canCapturePointer() {
		// Pointer capture is not supported on XR devices.
		return !orblit.isXrRuntime() && mInputHandler.canCapturePointer();
	}
	@Override
	public void requestPointerCapture() {
		if (canCapturePointer()) {
			super.requestPointerCapture();
			mInputHandler.onPointerCaptureChange(true);
		}
	}

	@Override
	public void releasePointerCapture() {
		super.releasePointerCapture();
		mInputHandler.onPointerCaptureChange(false);
	}

	@Override
	public void onPointerCaptureChange(boolean hasCapture) {
		super.onPointerCaptureChange(hasCapture);
		mInputHandler.onPointerCaptureChange(hasCapture);
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
}
