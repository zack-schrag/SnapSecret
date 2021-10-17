using NetArchTest.Rules;
using NetArchTest.Rules.Policies;
using Xunit;

namespace SnapSecret.Architecture.Tests
{
    public class FitnessTests
    {
        [Fact]
        public void ShouldOnlyHavePulumiDependenciesWithinInfrastructureNamespace()
        {
            var policy = Policy.Define("Dependency Direction Enforcement", "Ensure no unintended dependencies")
                .For(Types.InCurrentDomain())
                .Add(t =>
                    t.That()
                    .ResideInNamespaceContaining("SnapSecret")
                    .And()
                    .HaveDependencyOn("Pulumi")
                    .Should()
                    .ResideInNamespaceContaining("Infrastructure"),
                    "Controlling dependencies on Pulumi", "Only types in the SnapSecret.Infrastructure.* namespaces can have a dependency on Pulumi"
                );

            var result = policy.Evaluate();

            Assert.False(result.HasViolations);
        }

        [Fact]
        public void ShouldFollowHexagonalPatterns()
        {
            var policy = Policy.Define("Dependency Direction Enforcement", "Ensure no unintended dependencies")
                .For(Types.InCurrentDomain())
                .Add(t =>
                    t.That()
                    .ResideInNamespaceContaining("SnapSecret.Application")
                    .And()
                    .HaveNameStartingWith("SnapSecret.Application")
                    .Should()
                    .OnlyHaveDependenciesOn("SnapSecret.Domain"),
                    "Enforcing Hexagonal - Application should only depend on Domain", "Types in Application should only depend on Domain"
                )
                .Add(t =>
                    t.That()
                    .ResideInNamespaceContaining("SnapSecret.SecretsProviders")
                    .And()
                    .HaveNameStartingWith("SnapSecret.SecretsProviders")
                    .Should()
                    .OnlyHaveDependenciesOn("SnapSecret.Domain", "SnapSecret.Application"),
                    "Enforcing Hexagonal - SecretsProviders can depend on Domain and Application", "Types in SecretsProviders can depend on Domain and Application"
                );

            var result = policy.Evaluate();

            Assert.False(result.HasViolations);
        }
    }
}