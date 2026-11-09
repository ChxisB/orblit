/**************************************************************************/
/*  RegularContextFactory.java                                            */

package org.orblitengine.orblit.xr.regular;

import org.orblitengine.orblit.gl.GLSurfaceView;
import org.orblitengine.orblit.utils.GLUtils;

import android.util.Log;

import javax.microedition.khronos.egl.EGL10;
import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.egl.EGLContext;
import javax.microedition.khronos.egl.EGLDisplay;

/**
 * Factory used to setup the opengl context for pancake games.
 */
public class RegularContextFactory implements GLSurfaceView.EGLContextFactory {
	private static final String TAG = RegularContextFactory.class.getSimpleName();

	private static final int _EGL_CONTEXT_FLAGS_KHR = 0x30FC;
	private static final int _EGL_CONTEXT_OPENGL_DEBUG_BIT_KHR = 0x00000001;

	private static int EGL_CONTEXT_CLIENT_VERSION = 0x3098;

	private final boolean mUseDebugOpengl;

	public RegularContextFactory() {
		this(false);
	}

	public RegularContextFactory(boolean useDebugOpengl) {
		this.mUseDebugOpengl = useDebugOpengl;
	}

	public EGLContext createContext(EGL10 egl, EGLDisplay display, EGLConfig eglConfig) {
		Log.w(TAG, "creating OpenGL ES 3.0 context :");

		GLUtils.checkEglError(TAG, "Before eglCreateContext", egl);
		EGLContext context;
		int[] debug_attrib_list = { EGL_CONTEXT_CLIENT_VERSION, 3, _EGL_CONTEXT_FLAGS_KHR, _EGL_CONTEXT_OPENGL_DEBUG_BIT_KHR, EGL10.EGL_NONE };
		int[] attrib_list = { EGL_CONTEXT_CLIENT_VERSION, 3, EGL10.EGL_NONE };
		if (mUseDebugOpengl) {
			context = egl.eglCreateContext(display, eglConfig, EGL10.EGL_NO_CONTEXT, debug_attrib_list);
			if (context == null || context == EGL10.EGL_NO_CONTEXT) {
				Log.w(TAG, "creating 'OpenGL Debug' context failed");
				context = egl.eglCreateContext(display, eglConfig, EGL10.EGL_NO_CONTEXT, attrib_list);
			}
		} else {
			context = egl.eglCreateContext(display, eglConfig, EGL10.EGL_NO_CONTEXT, attrib_list);
		}
		GLUtils.checkEglError(TAG, "After eglCreateContext", egl);
		return context;
	}

	public void destroyContext(EGL10 egl, EGLDisplay display, EGLContext context) {
		egl.eglDestroyContext(display, context);
	}
}
