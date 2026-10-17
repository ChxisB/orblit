/// Conversations as data.
///
/// A [Dialogue] is read from a `.odialogue` file. It is nodes of lines,
/// choices, writes and jumps, and variants whose conditions over a
/// `ProgressTable` pick where it starts. A [DialogueBook] checks the
/// dialogues against a game's schema and hooks. A [DialogueRunner] walks
/// one conversation and draws nothing.
library;

export 'src/book.dart' show DialogueBook;
export 'src/dialogue.dart'
    show Dialogue, DialogueFormatException, Variant, dialogueExtension;
export 'src/node.dart'
    show ChoiceNode, ChoiceOption, JumpNode, LineNode, Node, SetNode, Write;
export 'src/runner.dart' show Before, DialogueRunner;
