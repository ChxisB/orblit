/*************************************************************************/
/*  BaseOrblitSpatialContainerGame.kt                                    */
/*************************************************************************/

package org.orblitengine.editor

import android.util.Log
import org.orblitengine.orblit.OrblitLib

/**
 * Provide support for running spatial container apps / games from the editor window.
 */
abstract class BaseOrblitSpatialContainerGame : OrblitXRGame() {
	companion object {
		private val TAG = BaseOrblitSpatialContainerGame::class.java.simpleName

		private val DEFAULT_SPATIAL_CONTAINER_BOUNDS = Triple(0.5f, 0.5f, 0.5f)

		@JvmStatic
		protected fun isSpatialContainerEnabled() = OrblitLib.getGlobal("xr/openxr/extensions/spatial_container/enabled").toBoolean()

		internal fun getSpatialContainerBounds(): Triple<Float, Float, Float> {
			val spatialContainerBounds = OrblitLib.getGlobal("xr/openxr/extensions/spatial_container/bounds")
			if (!spatialContainerBounds.isNullOrBlank()) {
				// The returned string has the following format: (x, y, z)
				val splitComponents = spatialContainerBounds
					.trim() // remove any leading / trailing whitespaces
					.trim('(', ')') // remove the leading and trailing parentheses
					.split(',')
				if (splitComponents.size == 3) {
					try {
						val bounds = Triple(
							splitComponents[0].toFloat(),
							splitComponents[1].toFloat(),
							splitComponents[2].toFloat()
						)
						return bounds
					} catch (_: NumberFormatException) {}
				}
				Log.w(TAG, "Error parsing spatial container bounds $spatialContainerBounds")
			}
			return DEFAULT_SPATIAL_CONTAINER_BOUNDS
		}
	}
}
