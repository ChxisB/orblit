/**************************************************************************/
/*  OvrContextFactory.java                                                */

package org.orblitengine.orblit.xr.ovr;

import org.orblitengine.orblit.gl.GLSurfaceView;

import android.opengl.EGL14;

import javax.microedition.khronos.egl.EGL10;
import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.egl.EGLContext;
import javax.microedition.khronos.egl.EGLDisplay;

/**
 * EGL Context factory for the Oculus mobile VR SDK.
 */
public class OvrContextFactory implements GLSurfaceView.EGLContextFactory {
	private static final int[] CONTEXT_ATTRIBS = {
		EGL14.EGL_CONTEXT_CLIENT_VERSION, 3, EGL10.EGL_NONE
	};

	@Override
	public EGLContext createContext(EGL10 egl, EGLDisplay display, EGLConfig eglConfig) {
		return egl.eglCreateContext(display, eglConfig, EGL10.EGL_NO_CONTEXT, CONTEXT_ATTRIBS);
	}

	@Override
	public void destroyContext(EGL10 egl, EGLDisplay display, EGLContext context) {
		egl.eglDestroyContext(display, context);
	}
}
