/**************************************************************************/
/*  EditorUtils.kt                                                        */

package org.orblitengine.orblit.editor.utils

/**
 * Utility class for accessing and using editor specific capabilities.
 *
 * This class is only functional on editor builds.
 */
object EditorUtils {
	@JvmStatic
	external fun runScene(scene: String, sceneArgs: Array<String>)

	@JvmStatic
	external fun orientationChanged(isPortrait: Boolean)
}
