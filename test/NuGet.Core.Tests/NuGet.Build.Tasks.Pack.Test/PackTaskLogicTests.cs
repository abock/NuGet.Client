// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NuGet.Commands;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Build.Tasks.Pack.Test
{
    public class PackTaskLogicTests
    {
        [Fact]
        public void PackTaskLogic_ProducesBasicPackage()
        {
            // This test uses the ...\NuGet.Build.Tasks.Pack.Test\compiler\resources\project.assets.json assets file.
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);

                // Act
                tc.BuildPackage();

                // Assert
                Assert.True(File.Exists(tc.NuspecPath), "The intermediate .nuspec file is not in the expected place.");
                Assert.True(File.Exists(tc.NupkgPath), "The output .nupkg file is not in the expected place.");
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the .nuspec.
                    Assert.Equal(tc.Request.PackageId, nuspecReader.GetId());
                    Assert.Equal(tc.Request.PackageVersion, nuspecReader.GetVersion().ToFullString());
                    Assert.Equal(string.Join(",", tc.Request.Authors), nuspecReader.GetAuthors());
                    Assert.Equal("", nuspecReader.GetOwners());
                    Assert.Equal(tc.Request.Description, nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/net45/a.dll" }, libItems[0].Items);

                    var dependencyGroups = nuspecReader.GetDependencyGroups().ToList();
                    var dependencyGroup = dependencyGroups.First();
                    var dependencyGroupFramework = dependencyGroup.TargetFramework.Framework;
                    var dependentPackages = dependencyGroup.Packages.ToList();
                    var centralTransitiveDependentPackage = dependentPackages
                        .Where(p => p.Id.Equals("Newtonsoft.Json", StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault();
                    Assert.Equal(1, dependencyGroups.Count);
                    Assert.Equal(".NETStandard", dependencyGroupFramework);
                    Assert.NotNull(centralTransitiveDependentPackage);
                    Assert.Equal(new List<string> { "Analyzers", "Build", "Runtime" }, centralTransitiveDependentPackage.Exclude);
                }
            }
        }

        [Fact]
        public void PackTaskLogic_ProducesPackageUsingNuspec()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                // Arrange
                string nuspec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package >
  <metadata>
    <id>bar</id>
    <version>0.0.0</version>
    <title>bartitle</title>
    <authors>kat</authors>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
    <license type=""expression"">MIT</license>
    <description>desc</description>
    <releaseNotes>release notes</releaseNotes>
    <copyright>msft</copyright>
    <tags>foo bar</tags>
    <dependencies>
        <group targetFramework=""net5.0-windows7.0"">
            <dependency id=""Newtonsoft.Json"" version=""12.0.3""/>
        </group>
        <group targetFramework=""net5.0"">
            <dependency id=""Newtonsoft.Json"" version=""12.0.1""/>
        </group>
    </dependencies>
  </metadata>
</package>";
                string nuspecPath = Path.Combine(testDir, ".nuspec");
                File.WriteAllText(nuspecPath, nuspec);

                var tc = new TestContext(testDir);
                tc.Request.NuspecFile = nuspecPath;
                tc.Request.NuspecBasePath = testDir;

                var net50DllDir = Path.Combine(testDir, "lib", "net5.0");
                var net50DllPath = Path.Combine(net50DllDir, "a.dll");

                Directory.CreateDirectory(net50DllDir);
                File.WriteAllBytes(net50DllPath, new byte[0]);

                var net50WinDllDir = Path.Combine(testDir, "lib", "net5.0-windows7.0");
                var net50WinDllPath = Path.Combine(net50WinDllDir, "a.dll");

                Directory.CreateDirectory(net50WinDllDir);
                File.WriteAllBytes(net50WinDllPath, new byte[0]);

                // Act
                tc.BuildPackage();

                // Assert
                string nupkgPath = Path.Combine(testDir, "bar.0.0.0.nupkg");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg file is not in the expected place.");
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the .nuspec.
                    Assert.Equal("bar", nuspecReader.GetId());
                    Assert.Equal("0.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("kat", nuspecReader.GetAuthors());
                    Assert.Equal("", nuspecReader.GetOwners());
                    Assert.Equal("desc", nuspecReader.GetDescription());
                    Assert.True(nuspecReader.GetRequireLicenseAcceptance());

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(2, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net50, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/net5.0/a.dll" }, libItems[0].Items);
                    Assert.Equal(NuGetFramework.Parse("net5.0-windows7.0"), libItems[1].TargetFramework);
                    Assert.Equal(new[] { "lib/net5.0-windows7.0/a.dll" }, libItems[1].Items);

                    var dependencyGroups = nuspecReader.GetDependencyGroups().ToList();
                    var dependencyGroup = dependencyGroups.First();
                    var dependencyGroupFramework = dependencyGroup.TargetFramework.Framework;
                    var dependentPackages = dependencyGroup.Packages.ToList();
                    var centralTransitiveDependentPackage = dependentPackages
                        .Where(p => p.Id.Equals("Newtonsoft.Json", StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault();
                    Assert.Equal(2, dependencyGroups.Count);
                    Assert.Equal(".NETCoreApp", dependencyGroupFramework);
                }
            }
        }

        [Fact]
        public void PackTaskLogic_ErrorsOnBadFrameworkPlatform()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir, "net5.0-windows");

                // Act & Assert
                Assert.Throws<PackagingException>(() => tc.BuildPackage());
            }
        }

        [Fact]
        public void PackTaskLogic_ErrorsOnBadFrameworkPlatformUsingNuspec()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                // Arrange
                string nuspec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package >
  <metadata>
    <id>bar</id>
    <version>0.0.0</version>
    <title>bartitle</title>
    <authors>kat</authors>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
    <license type=""expression"">MIT</license>
    <description>desc</description>
    <releaseNotes>release notes</releaseNotes>
    <copyright>msft</copyright>
    <tags>foo bar</tags>
    <dependencies>
        <group targetFramework=""net5.0-windows"">
            <dependency id=""Newtonsoft.Json"" version=""12.0.3""/>
        </group>
    </dependencies>
  </metadata>
</package>";
                string nuspecPath = Path.Combine(testDir, ".nuspec");
                File.WriteAllText(nuspecPath, nuspec);

                var tc = new TestContext(testDir);
                tc.Request.NuspecFile = nuspecPath;
                tc.Request.NuspecBasePath = testDir;

                var net50WinDllDir = Path.Combine(testDir, "lib", "net5.0-windows");
                var net50WinDllPath = Path.Combine(net50WinDllDir, "a.dll");

                Directory.CreateDirectory(net50WinDllDir);
                File.WriteAllBytes(net50WinDllPath, new byte[0]);

                // Act & Assert
                Assert.Throws<PackagingException>(() => tc.BuildPackage());
            }
        }

        [Fact]
        public void PackTaskLogic_WarnsMissingDot()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir, "net50");

                // Act
                tc.BuildPackage();

                // Assert
                var logger = (TestLogger)tc.Request.Logger;
                var messages = logger.WarningMessages.ToArray();
                Assert.True(messages[0].Contains("net50"));
                Assert.True(messages[0].Contains("include dots"));
            }
        }

        [Fact]
        public void PackTaskLogic_WhenDotInPlatformOnly_WarnsMissingDot()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir, "net50-windows7.0");

                // Act
                tc.BuildPackage();

                // Assert
                var logger = (TestLogger)tc.Request.Logger;
                var messages = logger.WarningMessages.ToArray();
                Assert.True(messages[0].Contains("net50-windows7.0"));
                Assert.True(messages[0].Contains("include dots"));
            }
        }

        [Fact]
        public void PackTaskLogic_WarnsMissingDot_UsingNuspec()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                // Arrange
                string nuspec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package >
  <metadata>
    <id>bar</id>
    <version>0.0.0</version>
    <title>bartitle</title>
    <authors>kat</authors>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
    <license type=""expression"">MIT</license>
    <description>desc</description>
    <releaseNotes>release notes</releaseNotes>
    <copyright>msft</copyright>
    <tags>foo bar</tags>
    <dependencies>
        <group targetFramework=""net50"">
            <dependency id=""Newtonsoft.Json"" version=""12.0.3""/>
        </group>
    </dependencies>
  </metadata>
</package>";
                string nuspecPath = Path.Combine(testDir, "bar.nuspec");
                File.WriteAllText(nuspecPath, nuspec);

                var tc = new TestContext(testDir);
                tc.Request.NuspecFile = nuspecPath;
                tc.Request.NuspecBasePath = testDir;

                var net50WinDllDir = Path.Combine(testDir, "lib", "net50");
                var net50WinDllPath = Path.Combine(net50WinDllDir, "a.dll");

                Directory.CreateDirectory(net50WinDllDir);
                File.WriteAllBytes(net50WinDllPath, new byte[0]);

                // Act
                tc.BuildPackage();

                // Assert
                var logger = (TestLogger)tc.Request.Logger;
                var messages = logger.WarningMessages.ToArray();
                Assert.True(messages[0].Contains("net50"));
                Assert.True(messages[0].Contains("include dots"));
            }
        }

        [Fact]
        public void PackTaskLogic_WhenDotInPlatformOnly_WarnsMissingDot_UsingNuspec()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                // Arrange
                string nuspec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package >
  <metadata>
    <id>bar</id>
    <version>0.0.0</version>
    <title>bartitle</title>
    <authors>kat</authors>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
    <license type=""expression"">MIT</license>
    <description>desc</description>
    <releaseNotes>release notes</releaseNotes>
    <copyright>msft</copyright>
    <tags>foo bar</tags>
    <dependencies>
        <group targetFramework=""net50-windows7.0"">
            <dependency id=""Newtonsoft.Json"" version=""12.0.3""/>
        </group>
    </dependencies>
  </metadata>
</package>";
                string nuspecPath = Path.Combine(testDir, "bar.nuspec");
                File.WriteAllText(nuspecPath, nuspec);

                var tc = new TestContext(testDir);
                tc.Request.NuspecFile = nuspecPath;
                tc.Request.NuspecBasePath = testDir;

                var net50WinDllDir = Path.Combine(testDir, "lib", "net50-windows7.0");
                var net50WinDllPath = Path.Combine(net50WinDllDir, "a.dll");

                Directory.CreateDirectory(net50WinDllDir);
                File.WriteAllBytes(net50WinDllPath, new byte[0]);

                // Act
                tc.BuildPackage();

                // Assert
                var logger = (TestLogger)tc.Request.Logger;
                var messages = logger.WarningMessages.ToArray();
                Assert.True(messages[0].Contains("net50-windows7.0"));
                Assert.True(messages[0].Contains("include dots"));
            }
        }

        [Fact]
        public void PackTaskLogic_SplitsTags()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);
                tc.Request.Tags = new[]
                {
                    "tagA",
                    "  tagB ",
                    null,
                    "tagC1;tagC2",
                    "tagD1,tagD2",
                    "tagE1 tagE2"
                };

                // Act
                tc.BuildPackage();

                // Assert
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    Assert.Equal("tagA   tagB   tagC1;tagC2 tagD1,tagD2 tagE1 tagE2", nuspecReader.GetTags());
                }
            }
        }

        [Fact]
        public void PackTaskLogic_CanSkipProducingTheNupkg()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);
                tc.Request.ContinuePackingAfterGeneratingNuspec = false;

                // Act
                tc.BuildPackage();

                // Assert
                Assert.True(File.Exists(tc.NuspecPath), "The intermediate .nuspec file is not in the expected place.");
                Assert.False(File.Exists(tc.NupkgPath), "The output .nupkg file is not in the expected place.");
            }
        }

        [Fact]
        public void PackTaskLogic_SupportsContentTargetFolders()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);
                var msbuildItem = tc.AddContentToProject("", "abc.txt", "hello world");
                tc.Request.ContentTargetFolders = new string[] { "folderA", "folderB" };
                tc.Request.PackageFiles = new MSBuildItem[] { msbuildItem };
                // Act
                tc.BuildPackage();

                // Assert
                Assert.True(File.Exists(tc.NuspecPath), "The intermediate .nuspec file is not in the expected place.");
                Assert.True(File.Exists(tc.NupkgPath), "The output .nupkg file is not in the expected place.");
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the .nuspec.
                    Assert.Equal(tc.Request.PackageId, nuspecReader.GetId());
                    Assert.Equal(tc.Request.PackageVersion, nuspecReader.GetVersion().ToFullString());
                    Assert.Equal(string.Join(",", tc.Request.Authors), nuspecReader.GetAuthors());
                    Assert.Equal("", nuspecReader.GetOwners());
                    Assert.Equal(tc.Request.Description, nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/net45/a.dll" }, libItems[0].Items);

                    // Validate the content items
                    foreach (var contentTargetFolder in tc.Request.ContentTargetFolders)
                    {
                        var contentItems = nupkgReader.GetFiles(contentTargetFolder).ToList();
                        Assert.Equal(1, contentItems.Count);
                        Assert.Equal(new[] { contentTargetFolder + "/abc.txt" }, contentItems);
                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(null, "abc.txt", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("", "abc.txt", "abc.txt")]
        [InlineData("folderA", "abc.txt", "folderA/abc.txt")]
        [InlineData("folderA", "abc", "folderA/abc")]
        [InlineData("folderA\\xyz.txt", "abc.txt", "folderA/xyz.txt")]
        [InlineData("folderA/xyz.txt", "abc.txt", "folderA/xyz.txt")]
        [InlineData("folderA;folderB", "abc.txt", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("folderA;folderB\\subFolderA", "abc.txt", "folderA/abc.txt;folderB/subFolderA/abc.txt")]
        [InlineData("folderA;folderB\\subFolderA;\\", "abc.txt", "folderA/abc.txt;folderB/subFolderA/abc.txt;abc.txt")]
        public void PackTaskLogic_SupportsPackagePath_OnContentWindows(string packagePath, string fileName, string expectedPackagePaths)
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);

                var metadata = packagePath != null ? new Dictionary<string, string>()
                {
                    {"PackagePath", packagePath},
                }
                :
                null;

                var msbuildItem = tc.AddContentToProject("", fileName, "hello world", metadata);
                tc.Request.PackageFiles = new MSBuildItem[] { msbuildItem };
                tc.Request.ContentTargetFolders = new string[] { "folderA", "folderB" };
                // Act
                tc.BuildPackage();

                // Assert
                Assert.True(File.Exists(tc.NuspecPath), "The intermediate .nuspec file is not in the expected place.");
                Assert.True(File.Exists(tc.NupkgPath), "The output .nupkg file is not in the expected place.");
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    // Validate the content items
                    var contentItems = nupkgReader.GetFiles().ToList();
                    foreach (var expectedPackagePath in expectedPackagePaths.Split(';'))
                    {
                        Assert.True(contentItems.Contains(expectedPackagePath));
                    }
                }
            }
        }

        [PlatformTheory(Platform.Darwin)]
        [InlineData(null, "abc.txt", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("", "abc.txt", "abc.txt")]
        [InlineData("folderA", "abc.txt", "folderA/abc.txt")]
        [InlineData("folderA", "abc", "folderA/abc")]
        [InlineData("folderA/xyz.txt", "abc.txt", "folderA/xyz.txt")]
        [InlineData("folderA;folderB", "abc.txt", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("folderA;folderB/subFolderA", "abc.txt", "folderA/abc.txt;folderB/subFolderA/abc.txt")]
        [InlineData("folderA;folderB/subFolderA;/", "abc.txt", "folderA/abc.txt;folderB/subFolderA/abc.txt;abc.txt")]
        public void PackTaskLogic_SupportsPackagePath_OnContentMac(string packagePath, string fileName, string expectedPackagePaths)
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);

                var metadata = packagePath != null ? new Dictionary<string, string>()
                {
                    {"PackagePath", packagePath},
                }
                    :
                    null;

                var msbuildItem = tc.AddContentToProject("", fileName, "hello world", metadata);
                tc.Request.PackageFiles = new MSBuildItem[] { msbuildItem };
                tc.Request.ContentTargetFolders = new string[] { "folderA", "folderB" };
                // Act
                tc.BuildPackage();

                // Assert
                Assert.True(File.Exists(tc.NuspecPath), "The intermediate .nuspec file is not in the expected place.");
                Assert.True(File.Exists(tc.NupkgPath), "The output .nupkg file is not in the expected place.");
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    // Validate the content items
                    var contentItems = nupkgReader.GetFiles().ToList();
                    foreach (var expectedPackagePath in expectedPackagePaths.Split(';'))
                    {
                        Assert.True(contentItems.Contains(expectedPackagePath));
                    }
                }
            }
        }

        [PlatformTheory(Platform.Linux)]
        [InlineData(null, "abc.txt", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("", "abc.txt", "abc.txt")]
        [InlineData("folderA", "abc.txt", "folderA/abc.txt")]
        [InlineData("folderA", "abc", "folderA/abc")]
        [InlineData("folderA/xyz.txt", "abc.txt", "folderA/xyz.txt")]
        [InlineData("folderA;folderB", "abc.txt", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("folderA;folderB/subFolderA", "abc.txt", "folderA/abc.txt;folderB/subFolderA/abc.txt")]
        [InlineData("folderA;folderB/subFolderA;/", "abc.txt", "folderA/abc.txt;folderB/subFolderA/abc.txt;abc.txt")]
        public void PackTaskLogic_SupportsPackagePath_OnContentLinux(string packagePath, string fileName, string expectedPackagePaths)
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);

                var metadata = packagePath != null ? new Dictionary<string, string>()
                {
                    {"PackagePath", packagePath},
                }
                    :
                    null;

                var msbuildItem = tc.AddContentToProject("", fileName, "hello world", metadata);
                tc.Request.PackageFiles = new MSBuildItem[] { msbuildItem };
                tc.Request.ContentTargetFolders = new string[] { "folderA", "folderB" };
                // Act
                tc.BuildPackage();

                // Assert
                Assert.True(File.Exists(tc.NuspecPath), "The intermediate .nuspec file is not in the expected place.");
                Assert.True(File.Exists(tc.NupkgPath), "The output .nupkg file is not in the expected place.");
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    // Validate the content items
                    var contentItems = nupkgReader.GetFiles().ToList();
                    foreach (var expectedPackagePath in expectedPackagePaths.Split(';'))
                    {
                        Assert.True(contentItems.Contains(expectedPackagePath));
                    }
                }
            }
        }

        [Fact]
        public void PackTaskLogic_SupportsContentFiles_DefaultBehavior()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);

                var metadata = new Dictionary<string, string>()
                {
                    { "BuildAction", "Content" },
                };

                var msbuildItem = tc.AddContentToProject("", "abc.txt", "hello world", metadata);
                tc.Request.PackageFiles = new MSBuildItem[] { msbuildItem };
                tc.Request.ContentTargetFolders = new string[] { "content", "contentFiles" };
                // Act
                tc.BuildPackage();

                // Assert
                Assert.True(File.Exists(tc.NuspecPath), "The intermediate .nuspec file is not in the expected place.");
                Assert.True(File.Exists(tc.NupkgPath), "The output .nupkg file is not in the expected place.");
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    var contentFiles = nuspecReader.GetContentFiles().ToList();

                    Assert.Equal(contentFiles.Count, 1);
                    Assert.Equal(contentFiles[0].BuildAction, "Content", StringComparer.Ordinal);
                    Assert.Equal(contentFiles[0].Include, "any/net45/abc.txt", StringComparer.Ordinal);

                    // Validate the content items
                    var contentItems = nupkgReader.GetFiles("content").ToList();
                    var contentFileItems = nupkgReader.GetFiles("contentFiles").ToList();
                    Assert.Equal(contentItems.Count, 1);
                    Assert.Equal(contentFileItems.Count, 1);
                    Assert.Contains("content/abc.txt", contentItems, StringComparer.Ordinal);
                    Assert.Contains("contentFiles/any/net45/abc.txt", contentFileItems, StringComparer.Ordinal);
                }
            }
        }

        [Fact]
        public void PackTaskLogic_BuildOutputWithoutFinalOutputPath_FallbackToIdentity()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);

                var metadata = new Dictionary<string, string>()
                {
                    { "BuildAction", "None" },
                    { "Identity", Path.Combine(testDir.Path, "abc.dll") },
                    { "TargetFramework", "net45" }
                };

                var msbuildItem = tc.AddContentToProject("", "abc.dll", "hello world", metadata);
                tc.Request.BuildOutputInPackage = new MSBuildItem[] { msbuildItem };
                tc.Request.ContentTargetFolders = new string[] { "content", "contentFiles" };
                // Act
                tc.BuildPackage();

                // Assert
                Assert.True(File.Exists(tc.NuspecPath), "The intermediate .nuspec file is not in the expected place.");
                Assert.True(File.Exists(tc.NupkgPath), "The output .nupkg file is not in the expected place.");
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/net45/abc.dll" }, libItems[0].Items);
                }
            }
        }

        [Fact]
        public void PackTaskLogic_BuildOutputWithCustomExtension_IncludedInNupkgIfSpecified()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);

                var metadata = new Dictionary<string, string>()
                {
                    { "BuildAction", "None" },
                    { "Identity", Path.Combine(testDir.Path, "abc.abc") },
                    { "TargetFramework", "net45" }
                };

                var msbuildItem = tc.AddContentToProject("", "abc.abc", "hello world", metadata);

                var metadata2 = new Dictionary<string, string>()
                {
                    { "BuildAction", "None" },
                    { "Identity", Path.Combine(testDir.Path, "abc.abd") },
                    { "TargetFramework", "net45" }
                };

                var msbuildItem2 = tc.AddContentToProject("", "abc.abd", "hello world", metadata);

                tc.Request.BuildOutputInPackage = new MSBuildItem[] { msbuildItem, msbuildItem2 };
                tc.Request.ContentTargetFolders = new string[] { "content", "contentFiles" };
                tc.Request.AllowedOutputExtensionsInPackageBuildOutputFolder = new string[] { ".abc" };
                // Act
                tc.BuildPackage();

                // Assert
                Assert.True(File.Exists(tc.NuspecPath), "The intermediate .nuspec file is not in the expected place.");
                Assert.True(File.Exists(tc.NupkgPath), "The output .nupkg file is not in the expected place.");
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/net45/abc.abc" }, libItems[0].Items);
                }
            }
        }

        [Fact]
        public void PackTaskLogic_SupportsContentFiles_WithPackagePath()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);

                var metadata = new Dictionary<string, string>()
                {
                    {"BuildAction", "EmbeddedResource"},
                    {"PackagePath", "contentFiles" },
                };

                var msbuildItem = tc.AddContentToProject("", "abc.txt", "hello world", metadata);
                tc.Request.PackageFiles = new MSBuildItem[] { msbuildItem };
                tc.Request.ContentTargetFolders = new string[] { "content", "contentFiles" };
                // Act
                tc.BuildPackage();

                // Assert
                Assert.True(File.Exists(tc.NuspecPath), "The intermediate .nuspec file is not in the expected place.");
                Assert.True(File.Exists(tc.NupkgPath), "The output .nupkg file is not in the expected place.");
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    var contentFiles = nuspecReader.GetContentFiles().ToList();

                    Assert.Equal(contentFiles.Count, 1);
                    Assert.Equal(contentFiles[0].BuildAction, "EmbeddedResource", StringComparer.Ordinal);
                    Assert.Equal(contentFiles[0].Include, "abc.txt", StringComparer.Ordinal);

                    // Validate the content items
                    var contentItems = nupkgReader.GetFiles("content").ToList();
                    var contentFileItems = nupkgReader.GetFiles("contentFiles").ToList();
                    Assert.Equal(contentItems.Count, 0);
                    Assert.Equal(contentFileItems.Count, 1);
                    Assert.Contains("contentFiles/abc.txt", contentFileItems, StringComparer.Ordinal);
                }
            }
        }

        [Fact]
        public void PackTaskLogic_SupportsContentFiles_WithPackageCopyToOutput()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);

                var metadata = new Dictionary<string, string>()
                {
                    {"BuildAction", "None"},
                    {"PackageCopyToOutput", "true" },
                };

                var msbuildItem = tc.AddContentToProject("", "abc.txt", "hello world", metadata);
                tc.Request.PackageFiles = new MSBuildItem[] { msbuildItem };
                tc.Request.ContentTargetFolders = new string[] { "content", "contentFiles" };
                // Act
                tc.BuildPackage();

                // Assert
                Assert.True(File.Exists(tc.NuspecPath), "The intermediate .nuspec file is not in the expected place.");
                Assert.True(File.Exists(tc.NupkgPath), "The output .nupkg file is not in the expected place.");
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    var contentFiles = nuspecReader.GetContentFiles().ToList();

                    Assert.Equal(contentFiles.Count, 1);
                    Assert.Equal(contentFiles[0].BuildAction, "None", StringComparer.Ordinal);
                    Assert.Equal(contentFiles[0].Include, "any/net45/abc.txt", StringComparer.Ordinal);
                    Assert.Equal(contentFiles[0].CopyToOutput, true);

                    // Validate the content items
                    var contentItems = nupkgReader.GetFiles("content").ToList();
                    var contentFileItems = nupkgReader.GetFiles("contentFiles").ToList();
                    Assert.Equal(contentItems.Count, 1);
                    Assert.Equal(contentFileItems.Count, 1);
                    Assert.Contains("content/abc.txt", contentItems, StringComparer.Ordinal);
                    Assert.Contains("contentFiles/any/net45/abc.txt", contentFileItems, StringComparer.Ordinal);
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(true)]
        [InlineData(false)]
        public void PackTaskLogic_SupportsNoDefaultExcludes(bool noDefaultExcludes)
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);

                var metadata = new Dictionary<string, string>()
                {
                    {"BuildAction", "None"},
                    {"PackageCopyToOutput", "true" },
                    {"Pack", "true" }
                };

                var msbuildItem = tc.AddContentToProject("", ".prefercliruntime", "hello world", metadata);
                tc.Request.PackageFiles = new MSBuildItem[] { msbuildItem };
                tc.Request.ContentTargetFolders = new string[] { "content" };
                tc.Request.NoDefaultExcludes = noDefaultExcludes;

                // Act
                tc.BuildPackage();

                // Assert
                Assert.True(File.Exists(tc.NuspecPath), "The intermediate .nuspec file is not in the expected place.");
                Assert.True(File.Exists(tc.NupkgPath), "The output .nupkg file is not in the expected place.");
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;


                    // Validate the content items
                    var contentItems = nupkgReader.GetFiles("content").ToList();
                    if (noDefaultExcludes)
                    {
                        Assert.Equal(contentItems.Count, 1);
                        Assert.Contains("content/.prefercliruntime", contentItems, StringComparer.Ordinal);
                    }
                    else
                    {
                        Assert.Equal(contentItems.Count, 0);
                    }
                }
            }
        }

        [Fact]
        public void PackTaskLogic_SupportsContentFiles_WithPackageFlatten()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);

                var metadata = new Dictionary<string, string>()
                {
                    {"BuildAction", "None"},
                    {"PackageFlatten", "true" },
                };

                var msbuildItem = tc.AddContentToProject("", "abc.txt", "hello world", metadata);
                tc.Request.PackageFiles = new MSBuildItem[] { msbuildItem };
                tc.Request.ContentTargetFolders = new string[] { "content", "contentFiles" };
                // Act
                tc.BuildPackage();

                // Assert
                Assert.True(File.Exists(tc.NuspecPath), "The intermediate .nuspec file is not in the expected place.");
                Assert.True(File.Exists(tc.NupkgPath), "The output .nupkg file is not in the expected place.");
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    var contentFiles = nuspecReader.GetContentFiles().ToList();

                    Assert.Equal(contentFiles.Count, 1);
                    Assert.Equal(contentFiles[0].BuildAction, "None", StringComparer.Ordinal);
                    Assert.Equal(contentFiles[0].Include, "any/net45/abc.txt", StringComparer.Ordinal);
                    Assert.Equal(contentFiles[0].Flatten, true);

                    // Validate the content items
                    var contentItems = nupkgReader.GetFiles("content").ToList();
                    var contentFileItems = nupkgReader.GetFiles("contentFiles").ToList();
                    Assert.Equal(contentItems.Count, 1);
                    Assert.Equal(contentFileItems.Count, 1);
                    Assert.Contains("content/abc.txt", contentItems, StringComparer.Ordinal);
                    Assert.Contains("contentFiles/any/net45/abc.txt", contentFileItems, StringComparer.Ordinal);
                }
            }
        }

        [Fact]
        public void PackTaskLogic_EmbedInteropAssembly()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);
                tc.Request.BuildOutputFolders = new[]
                {
                    "lib",
                    "embed"
                };

                // Act
                tc.BuildPackage();

                // Assert
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    // Validate the content items
                    foreach (var buildTargetFolder in tc.Request.BuildOutputFolders)
                    {
                        var compileItems = nupkgReader.GetFiles(buildTargetFolder).ToList();
                        Assert.Equal(1, compileItems.Count);
                        Assert.Equal(new[] { buildTargetFolder + "/net45/a.dll" }, compileItems);
                    }
                }
            }
        }

        private class TestContext
        {
            public TestContext(TestDirectory testdir)
                : this(testdir, "net45")
            {
            }

            public TestContext(TestDirectory testDir, string tfm)
            {
                var fullPath = Path.Combine(testDir, "project.csproj");
                var rootDir = Path.GetPathRoot(testDir);
                var dllDir = Path.Combine(testDir, "bin", "Debug", tfm);
                var dllPath = Path.Combine(dllDir, "a.dll");

                Directory.CreateDirectory(dllDir);
                Directory.CreateDirectory(Path.Combine(testDir, "obj"));
                File.WriteAllBytes(dllPath, new byte[0]);
                var path = string.Join(".", typeof(PackTaskLogicTests).Namespace, "compiler.resources", "project.assets.json");
                using (var reader = new StreamReader(GetType().GetTypeInfo().Assembly.GetManifestResourceStream(path)))
                {
                    var contents = reader.ReadToEnd();
                    File.WriteAllText(Path.Combine(testDir, "obj", "project.assets.json"), contents);
                }

                TestDir = testDir;
                Request = new PackTaskRequest
                {
                    PackageId = "SomePackage",
                    PackageVersion = "3.0.0-beta",
                    Authors = new[] { "NuGet Team", "Unit test" },
                    AllowedOutputExtensionsInPackageBuildOutputFolder = new[] { ".dll", ".exe", ".winmd", ".json", ".pri", ".xml" },
                    AllowedOutputExtensionsInSymbolsPackageBuildOutputFolder = new[] { ".dll", ".exe", ".winmd", ".json", ".pri", ".xml", ".pdb", ".mdb" },
                    Description = "A test package.",
                    PackItem = new MSBuildItem("project.csproj", new Dictionary<string, string>
                    {
                        { "RootDir", rootDir },
                        { "Directory", testDir.ToString().Substring(rootDir.Length) },
                        { "FileName", Path.GetFileNameWithoutExtension(fullPath) },
                        { "Extension", Path.GetExtension(fullPath) },
                        { "FullPath", fullPath }
                    }),
                    BuildOutputFolders = new string[] { "lib" },
                    NuspecOutputPath = "obj",
                    IncludeBuildOutput = true,
                    RestoreOutputPath = Path.Combine(testDir, "obj"),
                    ContinuePackingAfterGeneratingNuspec = true,
                    TargetFrameworks = new[] { tfm },
                    BuildOutputInPackage = new[] { new MSBuildItem(dllPath, new Dictionary<string, string>
                    {
                        {"FinalOutputPath", dllPath },
                        {"TargetFramework", tfm }
                    })},
                    Logger = new TestLogger(),
                    SymbolPackageFormat = "symbols.nupkg",
                    FrameworkAssemblyReferences = new MSBuildItem[] { },
                };
            }

            public TestDirectory TestDir { get; }
            public PackTaskRequest Request { get; }

            public string NuspecPath
            {
                get
                {
                    return Path.Combine(
                        TestDir,
                        Request.NuspecOutputPath,
                        $"{Request.PackageId}.{Request.PackageVersion}.nuspec");
                }
            }

            public string NupkgPath
            {
                get
                {
                    return Path.Combine(
                        TestDir,
                        $"{Request.PackageId}.{Request.PackageVersion}.nupkg");
                }
            }

            internal MSBuildItem AddContentToProject(string relativePathToDirectory, string fileName, string content, IDictionary<string, string> itemMetadata = null)
            {
                var relativePathToFile = Path.Combine(relativePathToDirectory, fileName);
                var fullpath = Path.Combine(TestDir, relativePathToFile);
                var pathToDirectory = Path.Combine(TestDir, relativePathToDirectory);

                if (!Directory.Exists(pathToDirectory))
                {
                    Directory.CreateDirectory(pathToDirectory);
                }

                if (!File.Exists(fullpath))
                {
                    // Create a file to write to.
                    using (var sw = File.CreateText(fullpath))
                    {
                        sw.WriteLine(content);
                    }
                }

                var metadata = itemMetadata ?? new Dictionary<string, string>();
                if (!metadata.ContainsKey("Identity"))
                {
                    metadata["Identity"] = relativePathToFile;
                }

                metadata["FullPath"] = fullpath;

                if (!metadata.ContainsKey("BuildAction"))
                {
                    metadata["BuildAction"] = "Content";
                }

                return new MSBuildItem(relativePathToFile, metadata);

            }

            public void BuildPackage()
            {
                // Arrange
                var target = new PackTaskLogic();

                // Act
                var packArgs = target.GetPackArgs(Request);
                var packageBuilder = target.GetPackageBuilder(Request);
                var runner = target.GetPackCommandRunner(Request, packArgs, packageBuilder);
                target.BuildPackage(runner);
            }
        }
    }
}
