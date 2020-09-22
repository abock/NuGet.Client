using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using NuGet.Frameworks;

namespace NuGet.Packaging.Rules
{
    internal static class FrameworkNameValidatorUtility
    {
        internal static bool IsValidFrameworkName(NuGetFramework framework)
        {
            return IsValidFrameworkName(PackagingConstants.Folders.Build + Path.DirectorySeparatorChar
                + framework.GetShortFolderName() + Path.DirectorySeparatorChar);
        }

        internal static bool IsValidFrameworkName(string path)
        {
            NuGetFramework fx;
            try
            {
                string effectivePath;
                fx = FrameworkNameUtility.ParseNuGetFrameworkFromFilePath(path, out effectivePath);
            }
            catch (ArgumentException)
            {
                fx = null;
            }

            // return false if the framework is Null or Unsupported
            return fx != null && fx.Framework != NuGetFramework.UnsupportedFramework.Framework;
        }

        internal static bool IsDottedFrameworkVersion(string path)
        {
            foreach (string knownFolder in PackagingConstants.Folders.Known)
            {
                string folderPrefix = knownFolder + System.IO.Path.DirectorySeparatorChar;
                if (knownFolder == "contentFiles")
                {
                    folderPrefix += "any" + System.IO.Path.DirectorySeparatorChar;
                }
                if (path.Length > folderPrefix.Length &&
                    path.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string frameworkPart = path.Substring(folderPrefix.Length);

                    try
                    {
                        string effectivePath = null;
                        NuGetFramework framework = FrameworkNameUtility.ParseNuGetFrameworkFolderName(
                            frameworkPart,
                            strictParsing: knownFolder == PackagingConstants.Folders.Lib,
                            effectivePath: out effectivePath);

                        if (framework == null)
                        {
                            return true;
                        }

                        string targetFrameworkString = Path.GetDirectoryName(path).Split(Path.DirectorySeparatorChar).ElementAt(1);

                        var isNet5EraTfm = framework.Version.Major >= 5 && StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.NetCoreApp, framework.Framework);

                        if (isNet5EraTfm)
                        {
                            var dotIndex = targetFrameworkString.IndexOf('.');
                            var dashIndex = targetFrameworkString.IndexOf('-');
                            var frameworkVersionHasDots = (dashIndex > -1 && dotIndex > -1 && dotIndex < dashIndex) || (dashIndex == -1 && dotIndex > -1);
                            return frameworkVersionHasDots;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // if the parsing fails, we treat it as if this file
                        // doesn't have target framework.
                        return false;
                    }
                }
            }
            return false;
        }

        internal static bool IsValidCultureName(PackageArchiveReader builder, string name)
        {
            // starting from NuGet 1.8, we support localized packages, which
            // can have a culture folder under lib, e.g. lib\fr-FR\strings.resources.dll
            var nuspecReader = builder.NuspecReader;
            if (string.IsNullOrEmpty(nuspecReader.GetLanguage()))
            {
                return false;
            }

            // the folder name is considered valid if it matches the package's Language property.
            return name.Equals(nuspecReader.GetLanguage(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
