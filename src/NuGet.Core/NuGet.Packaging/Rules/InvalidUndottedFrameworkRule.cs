// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;

namespace NuGet.Packaging.Rules
{
    internal class InvalidUndottedFrameworkRule : IPackageRule
    {
        public string MessageFormat { get; }

        public InvalidUndottedFrameworkRule(string messageFormat)
        {
            MessageFormat = messageFormat;
        }

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string knownFolder in PackagingConstants.Folders.Known)
            {
                foreach (var file in builder.GetFiles().Select(t => PathUtility.GetPathWithDirectorySeparator(t)))
                {
                    var parts = file.Split(Path.DirectorySeparatorChar);
                    if (parts.Length >= 3 && parts[0].Equals(knownFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        set.Add(file);
                    }
                }
            }

            return set.Where(s => !FrameworkNameValidatorUtility.IsDottedFrameworkVersion(s))
                      .Select(CreatePackageIssue);
        }

        private PackagingLogMessage CreatePackageIssue(string target)
        {
            return PackagingLogMessage.CreateWarning(
                string.Format(CultureInfo.CurrentCulture, MessageFormat, target),
                NuGetLogCode.NU5132);
        }
    }
}

