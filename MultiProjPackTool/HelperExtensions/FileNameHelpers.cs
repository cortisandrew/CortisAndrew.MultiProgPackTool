// Copyright (c) 2021 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

using MultiProjPackTool.SettingHandling;
using NuGet.Versioning;
using System;

namespace MultiProjPackTool.HelperExtensions
{
    public static class FileNameHelpers
    {
        public static string FormNupkgFilename(this allsettings settings)
        {
            var configuredVersion = settings?.metadata?.version;
            if (!NuGetVersion.TryParse(configuredVersion, out var nuGetVersion))
            {
                throw new ArgumentException(
                    $"'{configuredVersion}' is not a valid NuGet package version.",
                    nameof(settings));
            }

            return $"{settings.metadata.id}.{nuGetVersion.ToNormalizedString()}.nupkg";
        }
    }
}