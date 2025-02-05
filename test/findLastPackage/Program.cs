using findLastPackage;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using System;
using System.IO.Compression;
using System.Runtime.Versioning;


string[] frameworkNames = [
    "Microsoft.NETCore.App",
    "Microsoft.AspNetCore.App",
    "Microsoft.WindowsDesktop.App",
    "NETStandard.Library"
    ];

// framework compat facades that shouldn't be compared against packages
HashSet<string> filesToIgnore = new(StringComparer.OrdinalIgnoreCase)
{
    "mscorlib",
    "Microsoft.VisualBasic",
    "System",
    "System.ComponentModel.DataAnnotations",
    "System.Configuration",
    "System.Core",
    "System.Data",
    "System.Drawing",
    "System.IO.Compression.FileSystem",
    "System.Net",
    "System.Numerics",
    "System.Runtime.Serialization",
    "System.Security",
    "System.ServiceProcess",
    "System.ServiceModel.Web",
    "System.Transactions",
    "System.Web",
    "System.Windows",
    "System.Xml",
    "System.Xml.Serialization",
    "System.Xml.Linq",
    "WindowsBase"
};

var inboxFrameworkPackages = new Dictionary<string, Dictionary<NuGetFramework, Dictionary<string, Version>>>();

foreach (var frameworkName in frameworkNames)
{
    var frameworkKey = GetFrameworkKey(frameworkName);

    if (!inboxFrameworkPackages.TryGetValue(frameworkKey, out var inboxPackages))
    {
        inboxFrameworkPackages[frameworkKey] = inboxPackages = new();
    }

    var packageId = frameworkName + ".Ref";
    var versions = NuGetUtilities.GetVersions(packageId).Select(v => (packageId: packageId, version:v));

    if (frameworkName == "Microsoft.NETCore.App")
    {
        versions = versions.Append((frameworkName, NuGetVersion.Parse("2.1.0")))
                           .Append((frameworkName, NuGetVersion.Parse("2.0.0")));
    } 
    else if (frameworkName == "NETStandard.Library")
    {
        versions = versions.Append((frameworkName, NuGetVersion.Parse("2.0.0")));
    }


    NuGetVersion? lastVersion = null;
    foreach (var version in versions)
    {
        // only read the latest
        if (lastVersion is not null && lastVersion.Major == version.version.Major && lastVersion.Minor == version.version.Minor)
        {
            continue;
        }
        lastVersion = version.version;

        using (var frameworkPackage = NuGetUtilities.DownloadAndReadPackage(version.packageId, version.version))
        {
            // locate the references
            var referenceFiles = frameworkPackage.GetFiles("ref");
            if (!referenceFiles.Any())
            {
                referenceFiles = frameworkPackage.GetFiles("build");
            }
            if (!referenceFiles.Any())
            {
                throw new Exception("Unexpected framework package format");
            }
            referenceFiles = referenceFiles.Where(f => f.EndsWith(".dll") && !filesToIgnore.Contains(Path.GetFileNameWithoutExtension(f))).ToArray();

            var tfm = referenceFiles.First().Split('\\', '/')[1];

            NuGetFramework framework = NuGetFramework.Parse(tfm);

            var packages = new Dictionary<string, Version>();
            ApplyOverrides(frameworkPackage, packages);
            
            if (frameworkName == "Microsoft.NETCore.App" && framework.Equals(FrameworkConstants.CommonFrameworks.NetCoreApp20))
            {
                // import the runtime.json
                ApplyRuntimeJson("Microsoft.NETCore.Targets", NuGetVersion.Parse("1.1.4"), packages);
                ApplyPackageDependencies("runtime.native.System.Security.Cryptography", NuGetVersion.Parse("4.3.4"), framework, packages);
                ApplyPackageDependencies("runtime.native.System.Security.Cryptography.OpenSsl", NuGetVersion.Parse("4.3.3"), framework, packages);
                ApplyPackageDependencies("runtime.native.System.Security.Cryptography.Apple", NuGetVersion.Parse("4.3.1"), framework, packages);
                ApplyPackageDependencies("Microsoft.NETCore.App", NuGetVersion.Parse("2.0.0"), framework, packages);
            }

            //if (!ApplyPlatformManifest(frameworkPackage, framework, packages))
            //{
            //    ApplyRefFiles(referenceFiles.Select(r => frameworkPackage.GetEntry(r)), framework, packages);
            //}

            inboxPackages[framework] = packages;
        }
    }

}

FrameworkReducer reducer = new();
foreach (var inboxFrameworkPair in inboxFrameworkPackages)
{
    var frameworkKey = inboxFrameworkPair.Key;
    var inboxPackages = inboxFrameworkPair.Value;

    // reduce packages and emit
    foreach (var framework in inboxPackages.Keys)
    {
        // reduce in place
        var reducedPackages = inboxPackages[framework];

        // find the nearest framework not ourself, and remove any packages that it defines
        var nearest = reducer.GetNearest(framework, inboxPackages.Keys.Where(f => f != framework));
        var overlappingFramework = nearest;

        // n^2 but we have a small n and it reduces the data
        while (overlappingFramework != null)
        {
            foreach (var package in inboxPackages[overlappingFramework])
            {
                if (reducedPackages.TryGetValue(package.Key, out var existingVersion))
                {
                    if (existingVersion < package.Value)
                    {
                        Console.WriteLine($"{package.Key} - Compatible framework {overlappingFramework} has higher version {package.Value} than {framework} - {existingVersion}");
                        reducedPackages.Remove(package.Key);
                    }
                    else if (existingVersion == package.Value)
                    {
                        // compatible framework has the same version referenced
                        reducedPackages.Remove(package.Key);
                    }
                    // else compatible framework has a lower version, keep it

                }
            }
            overlappingFramework = reducer.GetNearest(overlappingFramework, inboxPackages.Keys.Where(f => f != overlappingFramework));
        }
    }
}

var defaultFrameworkPackages = inboxFrameworkPackages[string.Empty];

foreach(var defaultFrameworkPair in defaultFrameworkPackages)
{
    var framework = defaultFrameworkPair.Key;

    var propertyPairs = inboxFrameworkPackages.Where(p => p.Key != string.Empty && p.Value.ContainsKey(framework))
        .Select(p => (propertyName: p.Key, reducedPackages: p.Value[framework]));

    propertyPairs = propertyPairs.Prepend((propertyName: "Instance", reducedPackages: defaultFrameworkPair.Value));

    // write out our source file
    using StreamWriter fileWriter = new($"FrameworkPackages.{framework.GetShortFolderName()}.cs");

    // DotNetFrameworkName is something like ".NETStandard,Version=v2.0", convert to an identifier
    var tfmToken = framework.DotNetFrameworkName.Replace(",Version=v", "").Replace(".", "");

    var nearest = reducer.GetNearest(framework, defaultFrameworkPackages.Keys.Where(f => f != framework));
    var nearestTfmToken = nearest?.DotNetFrameworkName.Replace(",Version=v", "").Replace(".", "");

    fileWriter.WriteLine("namespace Microsoft.ComponentDetection.Detectors.NuGet;");
    fileWriter.WriteLine();
    fileWriter.WriteLine("using global::NuGet.Frameworks;");
    fileWriter.WriteLine();
    fileWriter.WriteLine("/// <summary>");
    fileWriter.WriteLine($"/// Framework packages for {framework.ToString()}.");
    fileWriter.WriteLine("/// </summary>");
    fileWriter.WriteLine("internal partial class FrameworkPackages");
    fileWriter.WriteLine("{");
    fileWriter.WriteLine($"    internal static class {tfmToken}");
    fileWriter.WriteLine("    {");
    foreach (var pair in propertyPairs)
    {
        var imports = nearest != null && (pair.propertyName == "Instance" || inboxFrameworkPackages[pair.propertyName].ContainsKey(nearest));
        fileWriter.WriteLine($"        internal static FrameworkPackages {pair.propertyName} {{ get; }} = new(NuGetFramework.Parse(\"{framework.GetShortFolderName()}\"), \"{GetFrameworkName(pair.propertyName, framework)}\"{(imports ? $", {nearestTfmToken}.{pair.propertyName}" : "")}){(pair.reducedPackages.Any() ? "" : ";")}");

        if (pair.reducedPackages.Any())
        {
            fileWriter.WriteLine("        {");
            foreach (var package in pair.reducedPackages.OrderBy(p => p.Key))
            {
                fileWriter.WriteLine($"            {{ \"{package.Key}\", \"{package.Value}\" }},");
            }
            fileWriter.WriteLine("        };");
        }
        fileWriter.WriteLine();
    }
    fileWriter.WriteLine($"        internal static void Register() => FrameworkPackages.Register({string.Join(", ", propertyPairs.Select(p => p.propertyName))});");
    fileWriter.WriteLine("    }");
    fileWriter.WriteLine("}");
}


void ApplyRefFiles(IEnumerable<ZipArchiveEntry> entries, NuGetFramework framework, Dictionary<string, Version> packages)
{
    foreach (var entry in entries)
    {
        string fileName = Path.GetFileNameWithoutExtension(entry.Name);
                
        var packageId = fileName;
        var versions = NuGetUtilities.GetVersionsFromEntry(entry);

        EvaluatePackage(packageId, framework, versions, packages);
    }
}

void ApplyRuntimeJson(string packageId, NuGetVersion packageVersion, Dictionary<string, Version> packages)
{
    using (var runtimePacakage = NuGetUtilities.DownloadAndReadPackage(packageId, packageVersion))
    {
        var runtimeJsonStream = runtimePacakage.GetEntry("runtime.json").Open();

        var runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(runtimeJsonStream);

        var runtimeDependencies = runtimeGraph.Runtimes.Values.SelectMany(rt => rt.RuntimeDependencySets.Values).SelectMany(rds => rds.Dependencies.Values);

        foreach (var runtimeDependency in runtimeDependencies)
        {
            var runtimePackageVersion = runtimeDependency.VersionRange.MinVersion.As3PartVersion();

            if (!packages.TryGetValue(runtimeDependency.Id, out var existingVersion) || existingVersion < runtimePackageVersion)
            {
                packages[runtimeDependency.Id] = runtimePackageVersion;
            }
        }

        
    }
}

void ApplyPackageDependencies(string packageId, NuGetVersion packageVersion, NuGetFramework targetFramework, Dictionary<string, Version> packages)
{
    var compareVersion = packageVersion.As3PartVersion();
    if (!packages.TryGetValue(packageId, out var existingVersion) || existingVersion < compareVersion)
    {
        packages[packageId] = compareVersion;

        using (var packageReader = NuGetUtilities.DownloadAndReadPackage(packageId, packageVersion))
        {
            FrameworkReducer reducer = new();
            var dependencyGroups = packageReader.GetPackageDependencies().ToDictionary(dg => dg.TargetFramework);

            var depFramework = reducer.GetNearest(targetFramework, dependencyGroups.Keys);

            if (depFramework != null)
            {
                foreach (var dependency in dependencyGroups[depFramework].Packages)
                {
                    ApplyPackageDependencies(dependency.Id, dependency.VersionRange.MinVersion, targetFramework, packages);
                }
            }
        }
    }
}

bool ApplyPlatformManifest(PackageArchiveReader packageArchiveReader, NuGetFramework framework, Dictionary<string, Version> packages)
{

    var platformManifestFile = packageArchiveReader.GetFiles().FirstOrDefault(f => f.EndsWith("PlatformManifest.txt"));
    
    if (platformManifestFile is null)
    {
        return false;
    }

    // merge in platforms data
    using var reader = new StreamReader(packageArchiveReader.GetEntry(platformManifestFile).Open());

    while (!reader.EndOfStream)
    {
        var platformFileInfo = reader.ReadLine();
        var platformFileParts = platformFileInfo.Trim().Split('|');

        if (platformFileParts.Length == 4)
        {
            var packageId = Path.GetFileNameWithoutExtension(platformFileParts[0]);
            var versions = ( assemblyVersion: ParseVersion(platformFileParts[2]), fileVersion: ParseVersion(platformFileParts[3]));
            EvaluatePackage(packageId, framework, versions, packages);
        }

    }

    return true;
}

Version ParseVersion(string version) => string.IsNullOrEmpty(version) ? default : Version.Parse(version);

void EvaluatePackage(string packageId, NuGetFramework framework, (Version assemblyVersion, Version fileVersion) versions, Dictionary<string, Version> packages)
{
    // For a library in a ref pack, look at all stable packages.
    var stableVersions = NuGetUtilities.GetStableVersions2(packageId);
    // Starting with the latest download each.
    foreach (var stableVersion in stableVersions)
    {
        if (packages.TryGetValue(packageId, out var existingVersion) && existingVersion >= stableVersion)
        {
            // already have an equal or higher packages
            break;
        }

        // Evaluate the package for the current framework.
        var packageContentVersions = NuGetUtilities.ResolvePackageAssetVersions(packageId, stableVersion, framework);

        if (!packageContentVersions.Any())
        {
            continue;
        }

        bool packageWins = false;
        foreach (var packageContentVersion in packageContentVersions)
        {
            if (packageContentVersion.assemblyVersion > versions.assemblyVersion)
            {
                packageWins = true;
                break;
            }

            if (packageContentVersion.assemblyVersion < versions.assemblyVersion)
            {
                break;
            }

            // equal assembly version
            if (packageContentVersion.fileVersion > versions.fileVersion)
            {
                packageWins = true;
                break;
            }

            // package file version is equal to or less than -- package loses
        }

        // If the library wins, stop.  If it loses, then continue with the next newest package
        if (!packageWins)
        {
            packages[packageId] = stableVersion;
            break;
        }
    }
}

void ApplyOverrides(PackageArchiveReader packageArchiveReader, Dictionary<string, Version> packages)
{
    ZipArchiveEntry entry = null;

    try
    {
        entry = packageArchiveReader.GetEntry("data/PackageOverrides.txt");
    }
    catch (FileNotFoundException)
    {
        return;
    }

    // merge in overrides data
    using var reader = new StreamReader(entry.Open());

    while (!reader.EndOfStream)
    {
        var packageOverride = reader.ReadLine();
        var packageOverrideParts = packageOverride.Trim().Split('|');

        if (packageOverrideParts.Length == 2)
        {
            var packageId = packageOverrideParts[0];
            // throw out the pre-release            
            var packageVersion = NuGetVersion.Parse(packageOverrideParts[1]).As3PartVersion();

            if (packages.TryGetValue(packageId, out var existingVersion))
            {
                if (existingVersion < packageVersion)
                {
                    Console.WriteLine($"{packageId} -- Caclulated {existingVersion} < PackageOverrides {packageVersion}");
                }
                else if (existingVersion > packageVersion)
                {
                    Console.WriteLine($"{packageId}  -- Caclulated {existingVersion} > PackageOverrides {packageVersion}");
                    continue;
                }
            }

            packages[packageId] = packageVersion;
        }

    }
}

string GetFrameworkKey(string frameworkName) =>
    frameworkName switch
    {
        "NETStandard.Library" => string.Empty,
        "Microsoft.NETCore.App" => string.Empty,
        "Microsoft.AspNetCore.App" => "AspNetCore",
        "Microsoft.WindowsDesktop.App" => "WindowsDesktop",
        _ => frameworkName
    };

string GetFrameworkName(string propertyName, NuGetFramework framework) =>
    propertyName switch
    {         
        "AspNetCore" => "Microsoft.AspNetCore.App",
        "WindowsDesktop" => "Microsoft.WindowsDesktop.App",
        "Instance" => framework.Framework switch
        {
            FrameworkConstants.FrameworkIdentifiers.Net => "Microsoft.NETCore.App",
            FrameworkConstants.FrameworkIdentifiers.NetCoreApp => "Microsoft.NETCore.App",
            FrameworkConstants.FrameworkIdentifiers.NetStandard => "NETStandard.Library",
        }
    };