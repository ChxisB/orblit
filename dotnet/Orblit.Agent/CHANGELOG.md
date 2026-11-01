# Changelog

## 0.2.0

- A port of `orblit_agent` 0.2.0.
- A node is keyed in `Memory` by reference, so two nodes built with `new`
  never share a place.
- `Tick.Read<T>` gives `default` for a missing key. For a value type that is
  zero or false.
- A label writes a whole number with the `.0`, `wait 1.0s`, for numbers from
  0.0001 up to below 1e15.
