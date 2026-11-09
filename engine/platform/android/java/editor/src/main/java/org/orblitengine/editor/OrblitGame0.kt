/**************************************************************************/
/*  OrblitGame0.kt                                                        */

package org.orblitengine.editor

/**
 * First instance for the Game window. Typically, this is the default instance launched when multi-instance is not
 * enabled / supported.
 */
class OrblitGame0: OrblitGame() {
	override fun getEditorWindowInfo() = RUN_GAME_INFO_0
}
