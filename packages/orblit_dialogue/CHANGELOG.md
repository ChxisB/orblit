# Changelog

## 0.1.0

- `Dialogue` and its nodes (`LineNode`, `ChoiceNode`, `SetNode`, `JumpNode`),
  read from and written to `.odialogue` files. Variants pick where a
  conversation starts from `Condition`s on a progress table.
- `DialogueBook` checks every dialogue against a `ProgressSchema` and the
  registered `before` functions.
- `DialogueRunner` walks one conversation. Writes and hooks change the
  table, and `table.changesSince` gives what to send.
