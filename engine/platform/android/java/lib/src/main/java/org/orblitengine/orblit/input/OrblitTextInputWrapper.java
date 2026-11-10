/**************************************************************************/
/*  OrblitTextInputWrapper.java                                           */

package org.orblitengine.orblit.input;

import org.orblitengine.orblit.*;

import android.content.Context;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.KeyEvent;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputMethodManager;
import android.widget.TextView;
import android.widget.TextView.OnEditorActionListener;

public class OrblitTextInputWrapper implements TextWatcher, OnEditorActionListener {
	// ===========================================================
	// Constants
	// ===========================================================
	private static final String TAG = OrblitTextInputWrapper.class.getSimpleName();

	// ===========================================================
	// Fields
	// ===========================================================
	private final OrblitRenderView mRenderView;
	private final OrblitEditText mEdit;
	private String mOriginText;
	private boolean mHasSelection;

	// ===========================================================
	// Constructors
	// ===========================================================

	public OrblitTextInputWrapper(final OrblitRenderView view, final OrblitEditText edit) {
		mRenderView = view;
		mEdit = edit;
	}

	// ===========================================================
	// Getter & Setter
	// ===========================================================

	private boolean isFullScreenEdit() {
		final TextView textField = mEdit;
		final InputMethodManager imm = (InputMethodManager)textField.getContext().getSystemService(Context.INPUT_METHOD_SERVICE);
		return imm.isFullscreenMode();
	}

	public void setOriginText(final String originText) {
		mOriginText = originText;
	}

	public void setSelection(boolean selection) {
		mHasSelection = selection;
	}

	// ===========================================================
	// Methods for/from SuperClass/Interfaces
	// ===========================================================

	@Override
	public void afterTextChanged(final Editable s) {
	}

	@Override
	public void beforeTextChanged(final CharSequence pCharSequence, final int start, final int count, final int after) {
		for (int i = 0; i < count; ++i) {
			mRenderView.getInputHandler().handleKeyEvent(KeyEvent.KEYCODE_DEL, 0, 0, true, false);
			mRenderView.getInputHandler().handleKeyEvent(KeyEvent.KEYCODE_DEL, 0, 0, false, false);

			if (mHasSelection) {
				mHasSelection = false;
				break;
			}
		}
	}

	@Override
	public void onTextChanged(final CharSequence pCharSequence, final int start, final int before, final int count) {
		final int[] newChars = new int[count];
		for (int i = start; i < start + count; ++i) {
			newChars[i - start] = pCharSequence.charAt(i);
		}
		for (int i = 0; i < count; ++i) {
			final int character = newChars[i];
			if ((character == '\n') && !(mEdit.getKeyboardType() == OrblitEditText.VirtualKeyboardType.KEYBOARD_TYPE_MULTILINE)) {
				// Return keys are handled through action events
				continue;
			}
			mRenderView.getInputHandler().handleKeyEvent(0, character, 0, true, false);
			mRenderView.getInputHandler().handleKeyEvent(0, character, 0, false, false);
		}
	}

	@Override
	public boolean onEditorAction(final TextView pTextView, final int pActionID, final KeyEvent pKeyEvent) {
		if (mEdit == pTextView && isFullScreenEdit() && pKeyEvent != null) {
			final String characters = pKeyEvent.getCharacters();
			if (characters != null) {
				for (int i = 0; i < characters.length(); i++) {
					final int character = characters.codePointAt(i);
					mRenderView.getInputHandler().handleKeyEvent(0, character, 0, true, false);
					mRenderView.getInputHandler().handleKeyEvent(0, character, 0, false, false);
				}
			}
		}

		if (pActionID == EditorInfo.IME_ACTION_DONE) {
			// Enter key has been pressed
			mRenderView.getInputHandler().handleKeyEvent(KeyEvent.KEYCODE_ENTER, 0, 0, true, false);
			mRenderView.getInputHandler().handleKeyEvent(KeyEvent.KEYCODE_ENTER, 0, 0, false, false);
			mRenderView.getView().requestFocus();
			return true;
		}
		return false;
	}

	// ===========================================================
	// Methods
	// ===========================================================

	// ===========================================================
	// Inner and Anonymous Classes
	// ===========================================================
}
