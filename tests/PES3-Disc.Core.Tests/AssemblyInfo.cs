using Xunit;

// Shared test-fixtures/ paths are rewritten by multiple test classes; run sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
