# Changelog

## 0.1.0

- Port of `orblit_progress` 0.1.0. `Compare` is a sealed class with static
  instances, because a C# enum cannot carry a symbol and a method.
- Values are `long`, because a name holds up to
  32 bits of unsigned value.
- `Condition.FromJson` reads decoded JSON as `object?`: a map is an
  `IReadOnlyDictionary<string, object?>`, a list an `IReadOnlyList<object?>`
  and a whole number an `int` or `long`.
