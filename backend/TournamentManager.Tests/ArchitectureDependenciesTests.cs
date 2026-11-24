using System.Reflection;
using TournamentManager.Application.Abstractions;
using Xunit;

namespace TournamentManager.Tests;

public sealed class ArchitectureDependenciesTests
{
    [Fact]
    public void Domain_DoesNotReferenceOtherLayersOrPersistenceTechnology()
    {
        var domainAssembly = Assembly.Load("TournamentManager.Domain");
        AssertCleanReferences(domainAssembly);
    }

    [Fact]
    public void Application_DoesNotReferenceInfrastructureApiOrPersistenceTechnology()
    {
        AssertCleanReferences(typeof(IDatabaseReadinessProbe).Assembly);
    }

    private static void AssertCleanReferences(Assembly assembly)
    {
        var forbidden = new[]
        {
            "TournamentManager.Infrastructure",
            "TournamentManager.Api",
            "Microsoft.EntityFrameworkCore",
            "Npgsql"
        };
        var dependencies = assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();

        foreach (var prefix in forbidden)
        {
            Assert.DoesNotContain(dependencies, name => name.StartsWith(prefix, StringComparison.Ordinal));
        }
    }
}
