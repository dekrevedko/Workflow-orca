using System.IO.Compression;
using System.Xml.Linq;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class PackageConsumerInfrastructureGuards
{
    [Fact]
    public void Fixtures_ArePackageReferenceOnlyAndUseExactLocalFeed()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var fixtureRoot = Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.Guards", "PackageFixtures");
        var definitions = FixtureDefinitions.Read<PackageConsumerFixture[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/package-consumer-fixtures.json");

        definitions.Should().HaveCount(8);
        definitions.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        definitions.Should().OnlyContain(x =>
            x.TurnsGreenSection == 7 || x.TurnsGreenSection == 8);
        foreach (var fixture in definitions)
        {
            var projectPath = Path.Combine(fixtureRoot, fixture.Project.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(projectPath).Should().BeTrue($"{fixture.Id} project must exist");
            File.Exists(Path.Combine(Path.GetDirectoryName(projectPath)!, "Program.cs")).Should().BeTrue();
            var project = XDocument.Load(projectPath);
            project.Descendants("ProjectReference").Should().BeEmpty();
            project.Descendants("Reference").Should().BeEmpty();
            project.Descendants("Compile").Should().BeEmpty();
            var packages = project.Descendants("PackageReference").Select(x => x.Attribute("Include")?.Value).ToArray();
            packages.Should().Equal(fixture.Packages);
        }

        var props = XDocument.Load(Path.Combine(fixtureRoot, "Directory.Build.props"));
        props.Descendants("RestoreSources").Single().Value.Replace('\\', '/')
            .Should().Be("$(MSBuildThisFileDirectory)../../../artifacts/phase0-packages");
        props.Descendants("RestoreFallbackFolders").Single().Value.Replace('\\', '/')
            .Should().Be("$(UserProfile)/.nuget/packages",
                "third-party packages may reuse the host cache while every OrcaCore package comes from the exact local feed");
        props.Descendants("RestoreIgnoreFailedSources").Single().Value.Should().Be("false");
        var central = XDocument.Load(Path.Combine(fixtureRoot, "Directory.Packages.props"));
        central.Descendants("PackageVersion").Should().OnlyContain(x =>
            x.Attribute("Version") != null && x.Attribute("Version")!.Value == "0.0.0-phase0");
        central.Descendants("PackageVersion").Select(x => x.Attribute("Include")?.Value)
            .Should().BeEquivalentTo(definitions.SelectMany(x => x.Packages).Distinct());
    }

    [Fact]
    public void FixtureRoles_HaveExactDirectPackageSelections()
    {
        var definitions = FixtureDefinitions.Read<PackageConsumerFixture[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/package-consumer-fixtures.json");
        var contract = FixtureDefinitions.Read<V1PublicContract>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json");
        var packagesById = contract.Packages.ToDictionary(x => x.Id, StringComparer.Ordinal);

        foreach (var fixture in definitions)
        {
            fixture.Packages.Should().OnlyContain(packageId => packagesById.ContainsKey(packageId));
            var closure = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>(fixture.Packages);
            while (pending.TryPop(out var packageId))
            {
                foreach (var dependency in packagesById[packageId].Dependencies)
                {
                    if (closure.Add(dependency)) pending.Push(dependency);
                }
            }

            closure.ExceptWith(fixture.Packages);
            fixture.TransitivePackages.Should().BeEquivalentTo(closure,
                $"{fixture.Id} must record the full transitive closure of its direct package references");
        }

        definitions.Single(x => x.Id == "primary-package").Packages.Should().Equal("OrcaCore");
        definitions.Single(x => x.Id == "minimal-ephemeral").Packages.Should().Equal("OrcaCore", "OrcaCore.Engine.Ephemeral");
        definitions.Single(x => x.Id == "provider-custom-host").Packages.Should().Equal(
            "OrcaCore", "OrcaCore.Runtime.Protocol", "OrcaCore.Provider.Abstractions");
        definitions.Where(x => x.Id != "provider-custom-host").SelectMany(x => x.Packages)
            .Should().NotContain(new[] { "OrcaCore.Runtime.Protocol", "OrcaCore.Provider.Abstractions" });
        definitions.Single(x => x.Id == "kubernetes-companion").Packages
            .Should().NotContain(x => x.Contains("Kubernetes", StringComparison.Ordinal) || x.Contains("AWS", StringComparison.Ordinal));
    }
}

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class PackageConsumerProductGuards
{
    [Fact]
    public void LocalFeed_ContainsEveryExactManifestPackageWithDeclaredDependencies()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var feed = Path.Combine(root, "artifacts", "phase0-packages");
        var contract = FixtureDefinitions.Read<V1PublicContract>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json");
        Directory.Exists(feed).Should().BeTrue("the Phase 0 pack command must create the local feed");
        foreach (var package in contract.Packages)
        {
            var path = Path.Combine(feed, $"{package.Id}.0.0.0-phase0.nupkg");
            File.Exists(path).Should().BeTrue($"{package.Id} must be packed at the exact verification version");
            if (!File.Exists(path)) continue;
            using var archive = ZipFile.OpenRead(path);
            var nuspec = archive.Entries.Single(x => x.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
            using var stream = nuspec.Open();
            var document = XDocument.Load(stream);
            document.Descendants().Single(x => x.Name.LocalName == "id").Value.Should().Be(package.Id);
            document.Descendants().Single(x => x.Name.LocalName == "version").Value.Should().Be("0.0.0-phase0");
            document.Descendants().Where(x => x.Name.LocalName == "dependency")
                .Select(x => x.Attribute("id")?.Value)
                .OfType<string>()
                .Where(id => id.StartsWith("OrcaCore", StringComparison.Ordinal))
                .Distinct()
                .Should().BeEquivalentTo(package.Dependencies);
            archive.Entries
                .Where(entry =>
                    entry.FullName.StartsWith("lib/", StringComparison.Ordinal) &&
                    entry.FullName.EndsWith(".dll", StringComparison.Ordinal))
                .Select(entry => Path.GetFileName(entry.FullName))
                .Should().Equal([$"{package.Id}.dll"],
                    $"{package.Id} must own exactly one matching implementation assembly");
        }
    }

    [Fact]
    public void ProductProjects_IsolateProviderNativeDependenciesAndShipNoTelemetrySdk()
    {
        var root = Path.Combine(FixtureDefinitions.RepositoryRoot(), "src");
        var projects = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Select(path => (Path: path, Document: XDocument.Load(path)))
            .ToArray();
        var references = projects.SelectMany(project => project.Document.Descendants("PackageReference")
                .Select(reference => (
                    Project: Path.GetFileNameWithoutExtension(project.Path),
                    Package: reference.Attribute("Include")?.Value)))
            .Where(reference => reference.Package is not null)
            .ToArray();

        references.Should().NotContain(reference =>
            reference.Package!.StartsWith("OpenTelemetry", StringComparison.Ordinal),
            "SDK and exporter registration is host-owned");
        references.Where(reference => reference.Package is "Npgsql" or "Dapper")
            .Should().OnlyContain(reference => reference.Project == "OrcaCore.Providers.PostgreSql",
                "provider-native dependencies stay inside their owning provider");
    }
}
