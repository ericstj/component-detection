namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

/// <summary>
/// Framework packages for .NETCoreApp,Version=v2.1.
/// </summary>
internal partial class FrameworkPackages
{
    internal static NuGetFramework NETCoreApp21 { get; } = NuGetFramework.Parse("netcoreapp2.1");

    internal static FrameworkPackages NETCoreApp21Packages { get; } = new(NETCoreApp21, NETCoreApp20Packages)
    {
        { "System.Collections.Immutable", "1.5.0" },
        { "System.ComponentModel.Annotations", "4.4.1" },
        { "System.Diagnostics.DiagnosticSource", "4.5.0" },
        { "System.Memory", "4.5.5" },
        { "System.Reflection.DispatchProxy", "4.5.0" },
        { "System.Reflection.Metadata", "1.6.0" },
        { "System.Threading.Tasks.Dataflow", "4.9.0" },
        { "System.Threading.Tasks.Extensions", "4.5.4" },
        { "System.ValueTuple", "4.5.0" },
    };
}
