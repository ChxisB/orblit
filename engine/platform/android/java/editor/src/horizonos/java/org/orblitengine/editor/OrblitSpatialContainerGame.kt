/*************************************************************************/
/*  OrblitSpatialContainerGame.kt                                        */
/*************************************************************************/

package org.orblitengine.editor

import android.os.Bundle

/**
 * Provide support for running spatial container apps / games from the editor window on HorizonOS.
 */
abstract class OrblitSpatialContainerGame : BaseOrblitSpatialContainerGame() {
	companion object {
		internal fun isSpatialContainerEnabled() = false
	}
}
