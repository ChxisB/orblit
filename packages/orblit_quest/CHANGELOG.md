# Changelog

## 0.1.0

- `Quest` and `Stage`, read from and written to `.oquest` files. A quest is
  a key, a title and stages in order, each with journal text and optional
  requirements as `Condition`s.
- `QuestBook` checks every quest against a `ProgressSchema` and moves a
  player's `ProgressTable`. `advance` goes forward only, checks the
  requirements of the stage it enters, and says when the quest completed.
