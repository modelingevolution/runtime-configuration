namespace ModelingEvolution.RuntimeConfiguration;

public interface IRuntimeConfiguration
{
    Task Save<T>(string section, string key, T value);
    Task Save<T>(string key, T value);
    bool IsDefaultValue<T>(string section, string key, T value);
    Task ResetToDefaultAsync(string section, string key);
}