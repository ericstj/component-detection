namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using global::NuGet.Packaging;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Detects NuGet packages in packages.config files.
/// </summary>
public sealed class NuGetPackagesConfigDetector : FileComponentDetector
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NuGetPackagesConfigDetector"/> class.
    /// </summary>
    /// <param name="componentStreamEnumerableFactory">The factory for handing back component streams to File detectors.</param>
    /// <param name="walkerFactory">The factory for creating directory walkers.</param>
    /// <param name="logger">The logger to use.</param>
    public NuGetPackagesConfigDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<NuGetPackagesConfigDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    /// <inheritdoc />
    public override IList<string> SearchPatterns => ["packages.config"];

    /// <inheritdoc />
    public override string Id => "NuGetPackagesConfig";

    /// <inheritdoc />
    public override IEnumerable<string> Categories =>
        [Enum.GetName(typeof(DetectorClass), DetectorClass.NuGet)];

    /// <inheritdoc />
    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.NuGet];

    /// <inheritdoc />
    public override int Version => 1;

    /// <inheritdoc />
    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        try
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var packagesConfig = new PackagesConfigReader(processRequest.ComponentStream.Stream);
            foreach (var package in packagesConfig.GetPackages(allowDuplicatePackageIds: true))
            {
                singleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new NuGetComponent(
                            package.PackageIdentity.Id,
                            package.PackageIdentity.Version.ToNormalizedString())),
                    true,
                    null,
                    /* TODO: Is this really the same concept?
                       Docs for NuGet say packages.config development dependencies are just not persisted as dependencies in the package.
                       That is not same as excluding from the output directory / runtime. */
                    package.IsDevelopmentDependency);

                // get the actual component in case it already exists
                var libraryComponent = singleFileComponentRecorder.GetComponent(package.PackageIdentity.Id);

                // Add framework information to the actual component
                var targetFramework = package.TargetFramework?.GetShortFolderName();

                if (targetFramework is not null)
                {
                    ((NuGetComponent)libraryComponent.Component).TargetFrameworks.Add(targetFramework);
                }
            }
        }
        catch (Exception e) when (e is PackagesConfigReaderException or XmlException)
        {
            this.Logger.LogWarning(e, "Failed to read packages.config file {File}", processRequest.ComponentStream.Location);
        }

        return Task.CompletedTask;
    }
}
