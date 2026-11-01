// Two engines starting at once crashed the process, and one headless session owns the UI thread,
// so the tests take turns.
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
