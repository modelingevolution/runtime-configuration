using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace ModelingEvolution.RuntimeConfiguration;

internal static class ConfigurationHelper
{
    public static string ResolveRuntimePath(
        IEnumerable<IConfigurationProvider> providers,
        string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath))
            return customPath;

        var appSettingsSource = providers
            .OfType<JsonConfigurationProvider>()
            .FirstOrDefault(p => p.Source.Path == "appsettings.json");

        if (appSettingsSource != null)
        {
            var directory = Path.GetDirectoryName(
                appSettingsSource.Source.FileProvider.GetFileInfo(appSettingsSource.Source.Path).PhysicalPath);
            return Path.Combine(directory ?? Directory.GetCurrentDirectory(), Constants.RuntimeFileName);
        }

        return Path.Combine(Directory.GetCurrentDirectory(), Constants.RuntimeFileName);
    }
}