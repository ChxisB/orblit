/*************************************************************************/
/*  OrblitSpatialContainerGame.kt                                        */
/*************************************************************************/

package org.orblitengine.editor

/**
 * Provide support for running spatial container apps / games from the editor window on Android XR.
 */
abstract class OrblitSpatialContainerGame : BaseOrblitSpatialContainerGame() {

	companion object {
		internal fun isSpatialContainerEnabled() = BaseOrblitSpatialContainerGame.isSpatialContainerEnabled()
	}

	override fun getOrblitAppLayout() = R.layout.spatial_container_game_layout
}
