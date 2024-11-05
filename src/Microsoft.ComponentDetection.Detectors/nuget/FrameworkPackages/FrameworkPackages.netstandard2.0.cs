namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

/// <summary>
/// Framework packages for .NETStandard,Version=v2.0.
/// </summary>
internal partial class FrameworkPackages
{
    internal static class NETStandard20
    {
        internal static FrameworkPackages Instance { get; } = new(NuGetFramework.Parse("netstandard2.0"), "NETStandard.Library")
        {
            { "Microsoft.Win32.Primitives", "4.3.0" },
            { "System.AppContext", "4.3.0" },
            { "System.Collections", "4.3.0" },
            { "System.Collections.NonGeneric", "4.3.0" },
            { "System.Collections.Specialized", "4.3.0" },
            { "System.ComponentModel", "4.0.1" },
            { "System.ComponentModel.EventBasedAsync", "4.0.11" },
            { "System.ComponentModel.Primitives", "4.3.0" },
            { "System.ComponentModel.TypeConverter", "4.3.0" },
            { "System.Console", "4.3.1" },
            { "System.Data.Common", "4.3.0" },
            { "System.Diagnostics.Contracts", "4.0.1" },
            { "System.Diagnostics.Debug", "4.3.0" },
            { "System.Diagnostics.FileVersionInfo", "4.3.0" },
            { "System.Diagnostics.Process", "4.3.0" },
            { "System.Diagnostics.StackTrace", "4.3.0" },
            { "System.Diagnostics.TextWriterTraceListener", "4.3.0" },
            { "System.Diagnostics.Tools", "4.3.0" },
            { "System.Diagnostics.TraceSource", "4.3.0" },
            { "System.Diagnostics.Tracing", "4.3.0" },
            { "System.Drawing.Primitives", "4.3.0" },
            { "System.Dynamic.Runtime", "4.0.11" },
            { "System.Globalization", "4.3.0" },
            { "System.Globalization.Calendars", "4.3.0" },
            { "System.Globalization.Extensions", "4.3.0" },
            { "System.IO", "4.3.0" },
            { "System.IO.Compression", "4.3.0" },
            { "System.IO.Compression.ZipFile", "4.3.0" },
            { "System.IO.FileSystem", "4.3.0" },
            { "System.IO.FileSystem.DriveInfo", "4.3.1" },
            { "System.IO.FileSystem.Primitives", "4.3.0" },
            { "System.IO.FileSystem.Watcher", "4.3.0" },
            { "System.IO.IsolatedStorage", "4.3.0" },
            { "System.IO.MemoryMappedFiles", "4.3.0" },
            { "System.IO.Pipes", "4.3.0" },
            { "System.IO.UnmanagedMemoryStream", "4.3.0" },
            { "System.Linq", "4.3.0" },
            { "System.Linq.Expressions", "4.3.0" },
            { "System.Linq.Parallel", "4.0.1" },
            { "System.Linq.Queryable", "4.0.1" },
            { "System.Net.Http", "4.3.4" },
            { "System.Net.NameResolution", "4.3.0" },
            { "System.Net.NetworkInformation", "4.3.0" },
            { "System.Net.Ping", "4.3.0" },
            { "System.Net.Primitives", "4.3.1" },
            { "System.Net.Requests", "4.0.11" },
            { "System.Net.Security", "4.3.2" },
            { "System.Net.Sockets", "4.3.0" },
            { "System.Net.WebHeaderCollection", "4.0.1" },
            { "System.Net.WebSockets", "4.3.0" },
            { "System.Net.WebSockets.Client", "4.3.2" },
            { "System.Reflection", "4.3.0" },
            { "System.Reflection.Extensions", "4.3.0" },
            { "System.Reflection.Primitives", "4.3.0" },
            { "System.Resources.Reader", "4.3.0" },
            { "System.Resources.ResourceManager", "4.3.0" },
            { "System.Resources.Writer", "4.3.0" },
            { "System.Runtime", "4.3.1" },
            { "System.Runtime.CompilerServices.VisualC", "4.3.0" },
            { "System.Runtime.Extensions", "4.3.1" },
            { "System.Runtime.Handles", "4.3.0" },
            { "System.Runtime.InteropServices", "4.3.0" },
            { "System.Runtime.InteropServices.RuntimeInformation", "4.3.0" },
            { "System.Runtime.Numerics", "4.0.1" },
            { "System.Runtime.Serialization.Formatters", "4.3.0" },
            { "System.Runtime.Serialization.Primitives", "4.3.0" },
            { "System.Runtime.Serialization.Xml", "4.3.0" },
            { "System.Security.Claims", "4.3.0" },
            { "System.Security.Cryptography.Algorithms", "4.3.1" },
            { "System.Security.Cryptography.Csp", "4.3.0" },
            { "System.Security.Cryptography.Encoding", "4.3.0" },
            { "System.Security.Cryptography.Primitives", "4.3.0" },
            { "System.Security.Cryptography.X509Certificates", "4.3.2" },
            { "System.Security.Principal", "4.0.1" },
            { "System.Security.SecureString", "4.3.0" },
            { "System.Text.Encoding", "4.3.0" },
            { "System.Text.Encoding.Extensions", "4.3.0" },
            { "System.Text.RegularExpressions", "4.3.0" },
            { "System.Threading", "4.0.11" },
            { "System.Threading.Overlapped", "4.3.0" },
            { "System.Threading.Tasks", "4.3.0" },
            { "System.Threading.Tasks.Parallel", "4.0.1" },
            { "System.Threading.Thread", "4.3.0" },
            { "System.Threading.ThreadPool", "4.3.0" },
            { "System.Threading.Timer", "4.3.0" },
            { "System.ValueTuple", "4.4.0" },
            { "System.Xml.ReaderWriter", "4.3.1" },
            { "System.Xml.XDocument", "4.0.11" },
            { "System.Xml.XmlDocument", "4.3.0" },
            { "System.Xml.XmlSerializer", "4.0.11" },
            { "System.Xml.XPath", "4.3.0" },
            { "System.Xml.XPath.XDocument", "4.3.0" },
        };

        internal static void Register() => FrameworkPackages.Register(Instance);
    }
}
