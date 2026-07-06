using System;
using System.Collections.Generic;

namespace IntegrationFlow.Contexts.Integrations._01Infrastructure;

/// <summary>
/// Optional scope support for <see cref="_03Domain.IIntegrationLogger"/>.
/// </summary>
internal interface IScopedIntegrationLogger
{
    IDisposable BeginScope(IReadOnlyDictionary<string, object?> state);
}
