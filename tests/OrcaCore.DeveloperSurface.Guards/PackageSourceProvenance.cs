using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OrcaCore.DeveloperSurface.Guards;

internal sealed record PackageSourceProvenanceEntry(
    string Id,
    string Project,
    string SourceSha256);

internal sealed record PackageSourceProvenanceRecord(
    int FormatVersion,
    string PackageVersion,
    PackageSourceProvenanceEntry[] Packages);

internal static class PackageSourceProvenance
{
    internal const int FormatVersion = 1;
    internal const string CandidatePathVariable = "ORCACORE_PACKAGE_SOURCE_PROVENANCE_CANDIDATE";

    private static readonly string[] SharedBuildInputs =
    {
        ".editorconfig",
        "Directory.Build.props",
        "Directory.Packages.props",
        "global.json"
    };

    private static readonly HashSet<string> NormalizedTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".csproj",
        ".editorconfig",
        ".json",
        ".md",
        ".props",
        ".resx",
        ".sql",
        ".targets",
        ".txt",
        ".xml"
    };

    internal static string ApprovedPath() => Path.Combine(
        FixtureDefinitions.RepositoryRoot(),
        "tests",
        "OrcaCore.DeveloperSurface.Guards",
        "Fixtures",
        "package-source-provenance.json");

    internal static PackageSourceProvenanceRecord ReadApproved() =>
        Read(ApprovedPath());

    internal static PackageSourceProvenanceRecord Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Approved package source provenance is missing: '{path}'.");

        return JsonSerializer.Deserialize<PackageSourceProvenanceRecord>(
                   File.ReadAllText(path),
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidOperationException($"Package source provenance is empty: '{path}'.");
    }

    internal static PackageSourceProvenanceRecord Capture(string? feed = null)
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var contract = FixtureDefinitions.Read<V1PublicContract>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json");
        if (!string.IsNullOrWhiteSpace(feed))
        {
            var expectedPackageFiles = contract.Packages
                .Select(package => $"{package.Id}.{contract.PackageVersion}.nupkg")
                .Order(StringComparer.Ordinal)
                .ToArray();
            var actualPackageFiles = Directory.Exists(feed)
                ? Directory.EnumerateFiles(feed, $"OrcaCore*.{contract.PackageVersion}.nupkg")
                    .Select(Path.GetFileName)
                    .Order(StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>();
            if (!actualPackageFiles.SequenceEqual(expectedPackageFiles, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Package feed inventory mismatch. Expected [{string.Join(", ", expectedPackageFiles)}], " +
                    $"found [{string.Join(", ", actualPackageFiles)}]. Run " +
                    "tests/OrcaCore.DeveloperSurface.Guards/pack-exact-package-feed.ps1 first.");
            }
        }

        var packages = contract.Packages
            .OrderBy(package => package.Id, StringComparer.Ordinal)
            .Select(package => CapturePackage(root, feed, package.Id, contract.PackageVersion))
            .ToArray();
        return new PackageSourceProvenanceRecord(FormatVersion, contract.PackageVersion, packages);
    }

    internal static string Diff(
        PackageSourceProvenanceRecord expected,
        PackageSourceProvenanceRecord actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        var findings = new List<string>();
        if (expected.FormatVersion != FormatVersion)
            findings.Add($"approved format version is {expected.FormatVersion}, expected {FormatVersion}.");
        if (actual.FormatVersion != FormatVersion)
            findings.Add($"captured format version is {actual.FormatVersion}, expected {FormatVersion}.");
        if (!string.Equals(expected.PackageVersion, actual.PackageVersion, StringComparison.Ordinal))
            findings.Add($"package version differs: approved '{expected.PackageVersion}', actual '{actual.PackageVersion}'.");

        var expectedById = expected.Packages.ToDictionary(package => package.Id, StringComparer.Ordinal);
        var actualById = actual.Packages.ToDictionary(package => package.Id, StringComparer.Ordinal);
        foreach (var id in expectedById.Keys.Union(actualById.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!expectedById.TryGetValue(id, out var expectedPackage))
            {
                findings.Add($"{id}: package provenance is unreviewed.");
                continue;
            }
            if (!actualById.TryGetValue(id, out var actualPackage))
            {
                findings.Add($"{id}: package provenance is missing.");
                continue;
            }
            if (!string.Equals(expectedPackage.Project, actualPackage.Project, StringComparison.Ordinal))
                findings.Add($"{id}: project differs: approved '{expectedPackage.Project}', actual '{actualPackage.Project}'.");
            if (!string.Equals(expectedPackage.SourceSha256, actualPackage.SourceSha256, StringComparison.Ordinal))
                findings.Add($"{id}: source SHA-256 differs.");
        }

        return string.Join(Environment.NewLine, findings);
    }

    internal static void WriteCandidate(PackageSourceProvenanceRecord record, string destination)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        var approved = Path.GetFullPath(ApprovedPath());
        var candidate = Path.GetFullPath(destination);
        if (string.Equals(approved, candidate, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Candidate capture refuses to overwrite the approved package provenance.");
        if (File.Exists(candidate))
            throw new InvalidOperationException($"Candidate package provenance path already exists: '{candidate}'.");

        Directory.CreateDirectory(Path.GetDirectoryName(candidate)!);
        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }).Replace("\r\n", "\n", StringComparison.Ordinal);
        File.WriteAllText(candidate, json + "\n", new UTF8Encoding(false));
    }

    private static PackageSourceProvenanceEntry CapturePackage(
        string root,
        string? feed,
        string packageId,
        string packageVersion)
    {
        var projectPaths = Directory.EnumerateFiles(Path.Combine(root, "src"), $"{packageId}.csproj", SearchOption.AllDirectories)
            .ToArray();
        if (projectPaths.Length != 1)
            throw new InvalidOperationException($"Expected one source project for '{packageId}', found {projectPaths.Length}.");

        var projectPath = projectPaths[0];
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var relativeProject = Path.GetRelativePath(root, projectPath).Replace('\\', '/');
        var sourceFiles = SharedBuildInputs.Select(path => Path.Combine(root, path))
            .Concat(Directory.EnumerateFiles(projectDirectory, "*", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path)))
            .Concat(DeclaredProjectInputs(projectPath, projectDirectory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.Ordinal)
            .ToArray();
        foreach (var sourceFile in sourceFiles)
            if (!File.Exists(sourceFile))
                throw new InvalidOperationException($"Package source input is missing: '{sourceFile}'.");

        var sourceRecord = new StringBuilder();
        foreach (var sourceFile in sourceFiles)
        {
            var content = ReadCanonicalContent(sourceFile);
            sourceRecord.Append(Path.GetRelativePath(root, sourceFile).Replace('\\', '/'))
                .Append('\t')
                .Append(content.Length)
                .Append('\t')
                .Append(Sha256(content))
                .Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(feed))
        {
            var packagePath = Path.Combine(feed, $"{packageId}.{packageVersion}.nupkg");
            using var archive = ZipFile.OpenRead(packagePath);
            var assemblyEntries = archive.Entries
                .Where(entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal) &&
                                entry.FullName.EndsWith($"/{packageId}.dll", StringComparison.Ordinal))
                .ToArray();
            if (assemblyEntries.Length != 1)
                throw new InvalidOperationException(
                    $"Package '{packagePath}' must contain exactly one implementation assembly; found {assemblyEntries.Length}.");

            var packageAssemblyPath = assemblyEntries[0].FullName.Split('/');
            if (packageAssemblyPath.Length != 3 || packageAssemblyPath[0] != "lib")
                throw new InvalidOperationException(
                    $"Package '{packagePath}' has an unsupported implementation assembly path '{assemblyEntries[0].FullName}'.");
            var targetFramework = packageAssemblyPath[1];
            var assemblyPath = Path.Combine(projectDirectory, "bin", "Release", targetFramework, $"{packageId}.dll");
            if (!File.Exists(assemblyPath))
                throw new InvalidOperationException(
                    $"Current Release assembly is missing: '{assemblyPath}'. Run " +
                    "tests/OrcaCore.DeveloperSurface.Guards/pack-exact-package-feed.ps1 first.");
            var assemblySha256 = Sha256(File.ReadAllBytes(assemblyPath));
            using var packageAssemblyStream = assemblyEntries[0].Open();
            var packageAssemblySha256 = Sha256(packageAssemblyStream);
            EnsureAssemblyHashesMatch(packageId, assemblySha256, packageAssemblySha256);
        }

        return new PackageSourceProvenanceEntry(
            packageId,
            relativeProject,
            Sha256(Encoding.UTF8.GetBytes(sourceRecord.ToString())));
    }

    internal static void EnsureAssemblyHashesMatch(
        string packageId,
        string currentAssemblySha256,
        string packageAssemblySha256)
    {
        if (string.Equals(packageAssemblySha256, currentAssemblySha256, StringComparison.Ordinal))
            return;
        throw new InvalidOperationException(
            $"Package '{packageId}' is stale: packaged assembly SHA-256 {packageAssemblySha256} " +
            $"does not match current Release assembly SHA-256 {currentAssemblySha256}.");
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> DeclaredProjectInputs(string projectPath, string projectDirectory)
    {
        var itemNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "AdditionalFiles",
            "Compile",
            "Content",
            "EmbeddedResource",
            "None"
        };
        var document = XDocument.Load(projectPath);
        foreach (var include in document.Descendants()
                     .Where(element => itemNames.Contains(element.Name.LocalName))
                     .Select(element => element.Attribute("Include")?.Value)
                     .Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            foreach (var path in ExpandDeclaredProjectInput(projectPath, projectDirectory, include!))
                yield return path;
        }
    }

    private static IEnumerable<string> ExpandDeclaredProjectInput(
        string projectPath,
        string projectDirectory,
        string include)
    {
        if (include.Contains("$(", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Package provenance cannot prove dynamic project input '{include}' in '{projectPath}'.");

        var normalized = include.Replace('\\', '/');
        var wildcardIndex = normalized.IndexOfAny(['*', '?']);
        if (wildcardIndex < 0)
        {
            var path = Path.GetFullPath(Path.Combine(projectDirectory, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(path))
                throw new InvalidOperationException($"Declared project input is missing: '{path}'.");
            yield return path;
            yield break;
        }

        var separatorIndex = normalized.LastIndexOf('/', wildcardIndex);
        var basePath = separatorIndex < 0 ? "." : normalized[..separatorIndex];
        var pattern = separatorIndex < 0 ? normalized : normalized[(separatorIndex + 1)..];
        var searchRoot = Path.GetFullPath(Path.Combine(projectDirectory, basePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!Directory.Exists(searchRoot))
            throw new InvalidOperationException(
                $"Declared project input root is missing for '{include}' in '{projectPath}': '{searchRoot}'.");

        var expression = GlobExpression(pattern);
        var matches = Directory.EnumerateFiles(searchRoot, "*", SearchOption.AllDirectories)
            .Where(path => expression.IsMatch(Path.GetRelativePath(searchRoot, path).Replace('\\', '/')))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (matches.Length == 0)
            throw new InvalidOperationException(
                $"Declared project input '{include}' in '{projectPath}' matched no files.");
        foreach (var match in matches)
            yield return match;
    }

    private static Regex GlobExpression(string pattern)
    {
        var expression = new StringBuilder("^");
        for (var index = 0; index < pattern.Length; index++)
        {
            var current = pattern[index];
            if (current == '*' && index + 1 < pattern.Length && pattern[index + 1] == '*')
            {
                index++;
                if (index + 1 < pattern.Length && pattern[index + 1] == '/')
                {
                    index++;
                    expression.Append("(?:.*/)?");
                }
                else
                {
                    expression.Append(".*");
                }
            }
            else if (current == '*')
            {
                expression.Append("[^/]*");
            }
            else if (current == '?')
            {
                expression.Append("[^/]");
            }
            else
            {
                expression.Append(Regex.Escape(current.ToString()));
            }
        }
        return new Regex(expression.Append('$').ToString(), RegexOptions.CultureInvariant);
    }

    private static byte[] ReadCanonicalContent(string path)
    {
        if (!NormalizedTextExtensions.Contains(Path.GetExtension(path)))
            return File.ReadAllBytes(path);
        var text = File.ReadAllText(path)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return Encoding.UTF8.GetBytes(text);
    }

    private static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static string Sha256(Stream content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
