global using Microsoft.Playwright;
global using Xunit;
global using static Microsoft.Playwright.Assertions;

[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
