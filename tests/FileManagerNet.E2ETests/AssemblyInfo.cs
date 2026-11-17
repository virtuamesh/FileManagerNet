using Xunit;

/* The tests share one server and one file tree, so they must not run in
 * parallel — each test still gets its own browser context. */
[assembly: CollectionBehavior(DisableTestParallelization = true)]
