using System.Reflection;
using System.Text.RegularExpressions;
using FamilySplit.Common.Modules;
using FamilySplit.Infrastructure;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace FamilySplit.UnitTests.Architecture;

[Trait("Category", "Architecture")]
public sealed class ArchitectureTests
{
    // ── Rule 1: Reference allow-list ──────────────────────────────────────────────

    [Fact]
    public void FeatureAssemblies_OnlyReference_AllowedProjects()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FamilySplit.Domain",
            "FamilySplit.Infrastructure",
            "FamilySplit.Common",
        };

        foreach (var asm in FeatureAssemblies.All)
        {
            var forbidden = asm.GetReferencedAssemblies()
                .Where(r => r.Name!.StartsWith("FamilySplit.", StringComparison.Ordinal)
                         && !allowed.Contains(r.Name!))
                .Select(r => r.Name!)
                .ToList();

            forbidden.Should().BeEmpty(
                because: $"feature assembly '{asm.GetName().Name}' must only reference Domain, Infrastructure, or Common");
        }
    }

    [Fact]
    public void Domain_DoesNotReference_AnyFamilySplitAssembly()
    {
        var refs = typeof(FamilySplit.Domain.Entities.User).Assembly
            .GetReferencedAssemblies()
            .Where(r => r.Name!.StartsWith("FamilySplit.", StringComparison.Ordinal))
            .Select(r => r.Name!)
            .ToList();

        refs.Should().BeEmpty(because: "Domain must have no FamilySplit.* dependencies");
    }

    [Fact]
    public void Common_OnlyReferences_Domain_And_Infrastructure()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FamilySplit.Domain",
            "FamilySplit.Infrastructure",
        };

        var forbidden = typeof(IFeatureModule).Assembly
            .GetReferencedAssemblies()
            .Where(r => r.Name!.StartsWith("FamilySplit.", StringComparison.Ordinal)
                     && !allowed.Contains(r.Name!))
            .Select(r => r.Name!)
            .ToList();

        forbidden.Should().BeEmpty(
            because: "Common must only reference Domain and Infrastructure");
    }

    // ── Rule 2: Exactly one public IFeatureModule per feature assembly ────────────

    [Fact]
    public void FeatureAssemblies_HaveExactlyOne_PublicIFeatureModule()
    {
        foreach (var asm in FeatureAssemblies.All)
        {
            var modules = asm.GetExportedTypes()
                .Where(t => typeof(IFeatureModule).IsAssignableFrom(t)
                         && t is { IsInterface: false, IsAbstract: false })
                .ToList();

            modules.Should().HaveCount(1,
                because: $"feature assembly '{asm.GetName().Name}' must have exactly one public IFeatureModule");
        }
    }

    // ── Rule 3: Handlers are sealed and reside in a use-case namespace ────────────

    [Fact]
    public void Handlers_AreSealed()
    {
        if (FeatureAssemblies.All.Length == 0) return;

        var result = Types.InAssemblies(FeatureAssemblies.All)
            .That()
                .HaveNameEndingWith("CommandHandler")
                .Or()
                .HaveNameEndingWith("QueryHandler")
            .Should()
                .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: string.Join(", ", result.FailingTypes.Select(t => t.FullName)));
    }

    private static readonly Regex UseCaseNamespacePattern =
        new(@"^FamilySplit\.Features\.\w+\.\w+$", RegexOptions.Compiled);

    [Fact]
    public void Handlers_ResideIn_UseCaseNamespace()
    {
        foreach (var asm in FeatureAssemblies.All)
        {
            var handlers = asm.GetTypes()
                .Where(t => t.Name.EndsWith("CommandHandler", StringComparison.Ordinal)
                         || t.Name.EndsWith("QueryHandler", StringComparison.Ordinal));

            foreach (var handler in handlers)
            {
                UseCaseNamespacePattern.IsMatch(handler.Namespace ?? string.Empty)
                    .Should().BeTrue(
                        because: $"'{handler.FullName}' must reside in namespace 'FamilySplit.Features.{{Slice}}.{{UseCase}}'");
            }
        }
    }

    // ── Rule 4: CommandValidator resides in the same namespace as its Command ─────

    [Fact]
    public void CommandValidators_ResideInSameNamespace_AsTheirCommand()
    {
        foreach (var asm in FeatureAssemblies.All)
        {
            var validators = asm.GetTypes()
                .Where(t => t.Name.EndsWith("CommandValidator", StringComparison.Ordinal)
                         && !t.IsAbstract && !t.IsInterface);

            foreach (var validator in validators)
            {
                var commandType = validator.BaseType
                    ?.GetGenericArguments()
                    .FirstOrDefault();

                if (commandType is null) continue;

                validator.Namespace.Should().Be(commandType.Namespace,
                    because: $"validator '{validator.FullName}' must reside in the same namespace as its command '{commandType.FullName}'");
            }
        }
    }

    // ── Rule 5: AppDbContext only used by handlers and sanctioned helpers ─────────

    [Fact]
    public void AppDbContext_OnlyUsedBy_HandlersAndSanctionedHelpers()
    {
        var sanctionedSuffixes = new[] { "Handler", "Guard", "Seeder" };

        foreach (var asm in FeatureAssemblies.All)
        {
            var violators = asm.GetTypes()
                .Where(t => t.GetConstructors()
                    .SelectMany(c => c.GetParameters())
                    .Any(p => p.ParameterType == typeof(AppDbContext)))
                .Where(t => !sanctionedSuffixes.Any(s =>
                    t.Name.EndsWith(s, StringComparison.Ordinal)))
                .Select(t => t.FullName!)
                .ToList();

            violators.Should().BeEmpty(
                because: "only *Handler, *Guard, and *Seeder types may take AppDbContext as a constructor parameter");
        }
    }

    // ── Rule 6: Registry completeness — host feature refs ≡ FeatureAssemblies.All

    [Fact]
    public void ApiHost_FeatureReferences_MatchRegistry()
    {
        var hostFeatureRefs = typeof(Program).Assembly
            .GetReferencedAssemblies()
            .Where(r => r.Name!.StartsWith("FamilySplit.Features.", StringComparison.Ordinal))
            .Select(r => r.Name!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var registryNames = FeatureAssemblies.All
            .Select(a => a.GetName().Name!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        registryNames.Should().BeEquivalentTo(hostFeatureRefs,
            because: "every FamilySplit.Features.* assembly the host references must be in FeatureAssemblies.All, and vice versa");
    }

    // ── Rule 7 (CQRS): QueryHandlers must not mutate or depend on write services ──

    [Fact]
    public void QueryHandlers_DoNotDependOn_MutationServices()
    {
        foreach (var asm in FeatureAssemblies.All)
        {
            var queryHandlers = asm.GetTypes()
                .Where(t => t.Name.EndsWith("QueryHandler", StringComparison.Ordinal));

            foreach (var handler in queryHandlers)
            {
                var paramTypes = handler.GetConstructors()
                    .SelectMany(c => c.GetParameters())
                    .Select(p => p.ParameterType)
                    .ToList();

                foreach (var param in paramTypes)
                {
                    param.Name.Should().NotEndWith("CommandHandler",
                        because: $"QueryHandler '{handler.Name}' must not depend on a CommandHandler");

                    param.FullName.Should().NotContain("AuditService",
                        because: $"QueryHandler '{handler.Name}' must not depend on AuditService");

                    param.FullName.Should().NotContain("INotificationService",
                        because: $"QueryHandler '{handler.Name}' must not depend on INotificationService");
                }
            }
        }
    }

    [Fact]
    public void QueryHandlers_DoNotCall_SaveChangesAsync()
    {
        if (FeatureAssemblies.All.Length == 0) return;

        var result = Types.InAssemblies(FeatureAssemblies.All)
            .That()
                .HaveNameEndingWith("QueryHandler")
            .Should()
                .MeetCustomRule(new DoesNotCallSaveChangesRule())
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: string.Join(", ", result.FailingTypes.Select(t => t.FullName)));
    }
}
