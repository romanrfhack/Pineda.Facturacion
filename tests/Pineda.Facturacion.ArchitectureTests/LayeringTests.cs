using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.ArchitectureTests;

public sealed class LayeringTests
{
    [Fact]
    public void Domain_Should_Not_Reference_Application_Api_Or_Infrastructure()
    {
        var references = GetReferenceNames(typeof(FiscalDocument).Assembly);

        Assert.DoesNotContain(references, static name => name.StartsWith("Pineda.Facturacion.Application", StringComparison.Ordinal));
        Assert.DoesNotContain(references, static name => name.StartsWith("Pineda.Facturacion.Api", StringComparison.Ordinal));
        Assert.DoesNotContain(references, static name => name.StartsWith("Pineda.Facturacion.Infrastructure", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_Should_Not_Reference_Api_Or_Infrastructure()
    {
        var references = GetReferenceNames(typeof(StandardVat16Calculator).Assembly);

        Assert.DoesNotContain(references, static name => name.StartsWith("Pineda.Facturacion.Api", StringComparison.Ordinal));
        Assert.DoesNotContain(references, static name => name.StartsWith("Pineda.Facturacion.Infrastructure", StringComparison.Ordinal));
    }

    private static string[] GetReferenceNames(System.Reflection.Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name ?? string.Empty)
            .ToArray();
    }
}
