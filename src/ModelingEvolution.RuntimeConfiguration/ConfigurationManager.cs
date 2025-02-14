using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Logging;

namespace ModelingEvolution.RuntimeConfiguration;

sealed class RuntimeConfiguration : IRuntimeConfiguration
{
    private readonly IConfigurationRoot _configRoot;
    private readonly IConfigurationRoot _defaultConfig;
    private readonly string _runtimeConfigPath;
    private readonly ILogger<RuntimeConfiguration> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public RuntimeConfiguration(
        IConfiguration configuration,
        ILogger<RuntimeConfiguration> logger,
        ConfigurationManagerOptions? options = null)
    {
        _configRoot = (IConfigurationRoot)configuration;
        _logger = logger;
        _runtimeConfigPath = ConfigurationHelper.ResolveRuntimePath(
            _configRoot.Providers,
            options?.RuntimeConfigPath);

        var defaultBuilder = new ConfigurationBuilder();
        foreach (var provider in _configRoot.Providers)
        {
            switch (provider)
            {
                case JsonConfigurationProvider jsonProvider when
                    Path.GetFileName(jsonProvider.Source.Path) == Constants.RuntimeFileName:
                    continue;

                case JsonConfigurationProvider jsonProvider:
                    defaultBuilder.AddJsonFile(
                        jsonProvider.Source.Path,
                        jsonProvider.Source.Optional,
                        jsonProvider.Source.ReloadOnChange);
                    break;

                case EnvironmentVariablesConfigurationProvider:
                    defaultBuilder.AddEnvironmentVariables();
                    break;

                case CommandLineConfigurationProvider:
                    defaultBuilder.AddCommandLine(Environment.GetCommandLineArgs());
                    break;
            }
        }

        _defaultConfig = defaultBuilder.Build();
    }
    
    public async Task Save<T>(string key, T value)
    {
        await Save(string.Empty, key, value);
    }
    public async Task Save<T>(string section, string key, T value)
    {
        // First check if the value is default
        if (IsDefaultValue<T>(section, key, value))
        {
            // If it's default, we should remove it from runtime config if it exists
            await RemoveValueIfExists(section, key);
            return;
        }

        try
        {
            Dictionary<string, JsonElement> config;

            if (File.Exists(_runtimeConfigPath))
            {
                var jsonString = await File.ReadAllTextAsync(_runtimeConfigPath);
                config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    jsonString) ?? new();
            }
            else
            {
                config = new();
            }

            // Handle section path
            if (string.IsNullOrEmpty(section))
            {
                // Root level value
                var valueJson = JsonSerializer.SerializeToElement(value);
                config[key] = valueJson;
            }
            else
            {
                // Section level value
                if (!config.TryGetValue(section, out var sectionElement))
                {
                    // Create new section if it doesn't exist
                    var sectionDict = new Dictionary<string, JsonElement>
                    {
                        [key] = JsonSerializer.SerializeToElement(value)
                    };
                    config[section] = JsonSerializer.SerializeToElement(sectionDict);
                }
                else
                {
                    // Update existing section
                    var sectionDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sectionElement);
                    sectionDict![key] = JsonSerializer.SerializeToElement(value);
                    config[section] = JsonSerializer.SerializeToElement(sectionDict);
                }
            }

            var updatedJson = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(_runtimeConfigPath, updatedJson);

            _configRoot.Reload();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating setting {Section}.{Key}", section, key);
            throw;
        }
    }

    private async Task RemoveValueIfExists(string section, string key)
    {
        if (!File.Exists(_runtimeConfigPath))
            return;

        try
        {
            var jsonString = await File.ReadAllTextAsync(_runtimeConfigPath);
            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonString);

            if (config == null) return;

            bool modified = false;

            if (string.IsNullOrEmpty(section))
            {
                // Remove from root
                modified = config.Remove(key);
            }
            else if (config.TryGetValue(section, out var sectionElement))
            {
                var sectionDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sectionElement);
                if (sectionDict != null)
                {
                    modified = sectionDict.Remove(key);
                    if (modified)
                    {
                        if (sectionDict.Count == 0)
                            config.Remove(section);
                        else
                            config[section] = JsonSerializer.SerializeToElement(sectionDict);
                    }
                }
            }

            if (modified)
            {
                if (config.Count == 0)
                {
                    // If config is empty, delete the file
                    File.Delete(_runtimeConfigPath);
                }
                else
                {
                    var updatedJson = JsonSerializer.Serialize(config, _jsonOptions);
                    await File.WriteAllTextAsync(_runtimeConfigPath, updatedJson);
                }
                _configRoot.Reload();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing setting {Section}.{Key}", section, key);
            throw;
        }
    }

    public bool IsDefaultValue<T>(string section, string key, T value)
    {
        try
        {
            T? defaultValue;

            if (string.IsNullOrEmpty(section))
            {
                defaultValue = _defaultConfig.GetValue<T>(key);
            }
            else
            {
                defaultValue = _defaultConfig.GetSection(section).GetValue<T>(key);
            }

            if (value == null && defaultValue == null)
                return true;
            if (value == null || defaultValue == null)
                return false;

            if (!typeof(T).IsPrimitive && typeof(T) != typeof(string))
            {
                var valueJson = JsonSerializer.Serialize(value, _jsonOptions);
                var defaultJson = JsonSerializer.Serialize(defaultValue, _jsonOptions);
                return valueJson == defaultJson;
            }

            return EqualityComparer<T>.Default.Equals(value, defaultValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing values for {Section}.{Key}", section, key);
            throw;
        }
    }

    public async Task ResetToDefaultAsync(string section, string key)
    {
        if (!File.Exists(_runtimeConfigPath))
            return;

        try
        {
            var jsonString = await File.ReadAllTextAsync(_runtimeConfigPath);
            var config = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(
                jsonString);

            if (config != null && config.ContainsKey(section))
            {
                var sectionDict = config[section];
                if (sectionDict.Remove(key))
                {
                    if (sectionDict.Count == 0)
                    {
                        config.Remove(section);
                    }

                    var updatedJson = JsonSerializer.Serialize(config, _jsonOptions);
                    await File.WriteAllTextAsync(_runtimeConfigPath, updatedJson);

                    _configRoot.Reload();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting setting {Section}.{Key}", section, key);
            throw;
        }
    }
}