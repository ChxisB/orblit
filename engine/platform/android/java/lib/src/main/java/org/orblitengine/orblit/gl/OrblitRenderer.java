/**************************************************************************/
/*  OrblitRenderer.java                                                   */

package org.orblitengine.orblit.gl;

import org.orblitengine.orblit.OrblitLib;
import org.orblitengine.orblit.plugin.OrblitPlugin;
import org.orblitengine.orblit.plugin.OrblitPluginRegistry;

import android.util.Log;

import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

/**
 * Orblit's GL renderer implementation.
 */
public class OrblitRenderer implements GLSurfaceView.Renderer {
	private final String TAG = OrblitRenderer.class.getSimpleName();

	private final OrblitPluginRegistry pluginRegistry;
	private boolean activityJustResumed = false;

	public OrblitRenderer() {
		this.pluginRegistry = OrblitPluginRegistry.getPluginRegistry();
	}

	public boolean onDrawFrame(GL10 gl) {
		if (activityJustResumed) {
			OrblitLib.onRendererResumed();
			activityJustResumed = false;
		}

		boolean swapBuffers = OrblitLib.step();
		for (OrblitPlugin plugin : pluginRegistry.getAllPlugins()) {
			plugin.onGLDrawFrame(gl);
		}

		return swapBuffers;
	}

	@Override
	public void onRenderThreadExiting() {
		Log.d(TAG, "Destroying Orblit Engine");
		OrblitLib.ondestroy();
	}

	public void onSurfaceChanged(GL10 gl, int width, int height) {
		OrblitLib.resize(null, width, height);
		for (OrblitPlugin plugin : pluginRegistry.getAllPlugins()) {
			plugin.onGLSurfaceChanged(gl, width, height);
		}
	}

	public void onSurfaceCreated(GL10 gl, EGLConfig config) {
		OrblitLib.newcontext(null);
		for (OrblitPlugin plugin : pluginRegistry.getAllPlugins()) {
			plugin.onGLSurfaceCreated(gl, config);
		}
	}

	public void onActivityResumed() {
		// We defer invoking OrblitLib.onRendererResumed() until the first draw frame call.
		// This ensures we have a valid GL context and surface when we do so.
		activityJustResumed = true;
	}

	public void onActivityPaused() {
		OrblitLib.onRendererPaused();
	}
}
