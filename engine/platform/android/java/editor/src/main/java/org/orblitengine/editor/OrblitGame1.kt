/**************************************************************************/
/*  OrblitGame1.kt                                                        */

package org.orblitengine.editor

/**
 * Second instance for the Game window.
 *
 * Used when multi-instance is enabled.
 */
class OrblitGame1: OrblitGame() {
	override fun getEditorWindowInfo() = RUN_GAME_INFO_1
}
