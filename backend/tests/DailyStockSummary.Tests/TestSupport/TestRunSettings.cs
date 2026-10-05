using Xunit;

// Many tests start a complete copy of the API (WebApplicationFactory). Hosts that start at the same moment in one
// process can interfere with each other, which showed up as an occasional "Cannot access a disposed object" in a
// startup-failure test on a 2-core CI runner. The whole suite takes about a second, so it runs one test at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
