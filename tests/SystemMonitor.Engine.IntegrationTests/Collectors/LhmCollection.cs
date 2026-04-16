using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

/// <summary>
/// xUnit disables parallelism across tests in the same collection. LHM's internal
/// state is not safe when multiple Computer instances initialize concurrently, so
/// all LHM-touching tests share this collection and run serially.
/// </summary>
[CollectionDefinition("Lhm", DisableParallelization = true)]
public sealed class LhmCollection { }
