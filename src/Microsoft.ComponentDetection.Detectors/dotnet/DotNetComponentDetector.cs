namespace Microsoft.ComponentDetection.Detectors.DotNet;

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using global::NuGet.ProjectModel;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class DotNetComponentDetector : FileComponentDetector, IExperimentalDetector
{
    private const string GlobalJsonFileName = "global.json";
    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly IDirectoryUtilityService directoryUtilityService;
    private readonly IFileUtilityService fileUtilityService;
    private readonly IPathUtilityService pathUtilityService;
    private readonly LockFileFormat lockFileFormat = new();
    private readonly ConcurrentDictionary<string, string?> sdkVersionCache = [];
    private string? sourceDirectory;
    private string? sourceFileRootDirectory;

    public DotNetComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        ICommandLineInvocationService commandLineInvocationService,
        IDirectoryUtilityService directoryUtilityService,
        IFileUtilityService fileUtilityService,
        IPathUtilityService pathUtilityService,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<DotNetComponentDetector> logger)
    {
        this.commandLineInvocationService = commandLineInvocationService;
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.directoryUtilityService = directoryUtilityService;
        this.fileUtilityService = fileUtilityService;
        this.pathUtilityService = pathUtilityService;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id => "DotNet";

    public override IList<string> SearchPatterns { get; } = [LockFileFormat.AssetsFileName];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.DotNet];

    public override int Version { get; } = 1;

    public override IEnumerable<string> Categories => ["DotNet"];

    private string? NormalizeDirectory(string? path) => string.IsNullOrEmpty(path) ? path : Path.TrimEndingDirectorySeparator(this.pathUtilityService.NormalizePath(path));

    private async Task<string?> RunDotNetVersionAsync(string workingDirectoryPath, CancellationToken cancellationToken)
    {
        var workingDirectory = new DirectoryInfo(workingDirectoryPath);

        var process = await this.commandLineInvocationService.ExecuteCommandAsync("dotnet", ["dotnet.exe"], workingDirectory, cancellationToken, "--version").ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            // debug only - it could be that dotnet is not actually on the path and specified directly by the build scripts.
            this.Logger.LogDebug("Failed to invoke 'dotnet --version'. Return: {Return} StdErr: {StdErr} StdOut: {StdOut}.", process.ExitCode, process.StdErr, process.StdOut);
            return null;
        }

        return process.StdOut.Trim();
    }

    public override Task<IndividualDetectorScanResult> ExecuteDetectorAsync(ScanRequest request, CancellationToken cancellationToken = default)
    {
        this.sourceDirectory = this.NormalizeDirectory(request.SourceDirectory.FullName);
        this.sourceFileRootDirectory = this.NormalizeDirectory(request.SourceFileRoot?.FullName);

        return base.ExecuteDetectorAsync(request, cancellationToken);
    }

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var lockFile = this.lockFileFormat.Read(processRequest.ComponentStream.Stream, processRequest.ComponentStream.Location);

        var projectPath = lockFile.PackageSpec.RestoreMetadata.ProjectPath;

        if (!this.fileUtilityService.Exists(projectPath))
        {
            // Could be the assets file was not actually from this build
            this.Logger.LogWarning("Project path {ProjectPath} specified by {ProjectAssetsPath} does not exist.", projectPath, processRequest.ComponentStream.Location);
        }

        var projectDirectory = this.pathUtilityService.GetParentDirectory(projectPath);
        var sdkVersion = await this.GetSdkVersionAsync(projectDirectory, cancellationToken);

        var projectName = lockFile.PackageSpec.RestoreMetadata.ProjectName;
        var projectOutputPath = lockFile.PackageSpec.RestoreMetadata.OutputPath;

        if (!this.directoryUtilityService.Exists(projectOutputPath))
        {
            this.Logger.LogWarning("Project output path {ProjectOutputPath} specified by {ProjectAssetsPath} does not exist.", projectOutputPath, processRequest.ComponentStream.Location);
        }

        var targetType = this.GetProjectType(projectOutputPath, projectName, cancellationToken);

        var componentReporter = this.ComponentRecorder.CreateSingleFileComponentRecorder(projectPath);
        foreach (var target in lockFile.Targets)
        {
            var targetFramework = target.TargetFramework?.GetShortFolderName();

            componentReporter.RegisterUsage(new DetectedComponent(new DotNetComponent(sdkVersion, targetFramework, targetType)));
        }
    }

    private string? GetProjectType(string projectOutputPath, string projectName, CancellationToken cancellationToken)
    {
        if (this.directoryUtilityService.Exists(projectOutputPath))
        {
            var namePattern = (projectName ?? "*") + ".dll";

            // look for the compiled output, first as dll then as exe.
            var candidates = this.directoryUtilityService.EnumerateFiles(projectOutputPath, namePattern, SearchOption.AllDirectories)
                     .Concat(this.directoryUtilityService.EnumerateFiles(projectOutputPath, namePattern, SearchOption.AllDirectories));
            foreach (var candidate in candidates)
            {
                try
                {
                    return this.IsApplication(candidate) ? "application" : "library";
                }
                catch (Exception e)
                {
                    this.Logger.LogWarning("Failed to open output assembly {AssemblyPath} error {Message}.", candidate, e.Message);
                }
            }
        }

        return null;
    }

    private bool IsApplication(string assemblyPath)
    {
        using var peReader = new PEReader(this.fileUtilityService.MakeFileStream(assemblyPath));

        // despite the name `IsExe` this is actually based of the CoffHeader Characteristics
        return peReader.PEHeaders.IsExe;
    }

    /// <summary>
    /// Recursively get the sdk version from the project directory or parent directories.
    /// </summary>
    /// <param name="projectDirectory">Directory to start the search.</param>
    /// <param name="cancellationToken">Cancellation token to halt the search.</param>
    /// <returns>Sdk version found, or null if no version can be detected.</returns>
    private async Task<string?> GetSdkVersionAsync(string? projectDirectory, CancellationToken cancellationToken)
    {
        // normalize since we need to use as a key
        projectDirectory = this.NormalizeDirectory(projectDirectory);

        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            // not expected
            return null;
        }

        if (this.sdkVersionCache.TryGetValue(projectDirectory, out var sdkVersion))
        {
            return sdkVersion;
        }

        var parentDirectory = this.pathUtilityService.GetParentDirectory(projectDirectory);
        var globalJsonPath = Path.Combine(projectDirectory, GlobalJsonFileName);

        if (this.fileUtilityService.Exists(globalJsonPath))
        {
            sdkVersion = await this.RunDotNetVersionAsync(projectDirectory, cancellationToken);

            if (string.IsNullOrWhiteSpace(sdkVersion))
            {
                var globalJson = await JsonDocument.ParseAsync(this.fileUtilityService.MakeFileStream(globalJsonPath), cancellationToken: cancellationToken);
                if (globalJson.RootElement.TryGetProperty("sdk", out var sdk))
                {
                    if (sdk.TryGetProperty("version", out var version))
                    {
                        sdkVersion = version.GetString();
                    }
                }
            }

            var globalJsonComponent = new DetectedComponent(new DotNetComponent(sdkVersion));
            var recorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(globalJsonPath);
            recorder.RegisterUsage(globalJsonComponent, isExplicitReferencedDependency: true);
        }
        else if (projectDirectory.Equals(this.sourceDirectory, StringComparison.OrdinalIgnoreCase) ||
                 projectDirectory.Equals(this.sourceFileRootDirectory, StringComparison.OrdinalIgnoreCase) ||
                 string.IsNullOrEmpty(parentDirectory) ||
                 projectDirectory.Equals(parentDirectory, StringComparison.OrdinalIgnoreCase))
        {
            // if we are at the source directory, source file root, or have reached a root directory, run `dotnet --version`
            // this could fail if dotnet is not on the path, or if the global.json is malformed
            sdkVersion = await this.RunDotNetVersionAsync(projectDirectory, cancellationToken);
        }
        else
        {
            // recurse up the directory tree
            sdkVersion = await this.GetSdkVersionAsync(parentDirectory, cancellationToken);
        }

        this.sdkVersionCache[projectDirectory] = sdkVersion;

        return sdkVersion;
    }
}
