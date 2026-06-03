using Xunit;

// Rate-limit and shared in-memory state are order-sensitive across API test classes.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
