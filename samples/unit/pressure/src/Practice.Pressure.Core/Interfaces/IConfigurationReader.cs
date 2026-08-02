namespace Practice.Pressure.Core.Interfaces;

public interface IConfigurationReader
{
    bool GetBool(string key, bool defaultValue);
}
