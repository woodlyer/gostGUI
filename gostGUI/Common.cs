using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using System.Collections.Generic;
using System.Text.Json.Serialization;



public class ConfigItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("enable")]
    public string Enable { get; set; }

    [JsonPropertyName("program")]
    public string Program { get; set; }

    [JsonPropertyName("args")]
    public string Args { get; set; }
}


public class ConfigData
{
    [JsonPropertyName("autoRun")]
    public string AutoRun { get; set; }

    [JsonPropertyName("items")]
    public List<ConfigItem> Items { get; set; }
}

public class Common
{
    public static string GetApplicationPath()
    {
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    public static bool LoadJson(string path, out ConfigData configData)
    {
        configData = new ConfigData
        {
            AutoRun = "false",
            Items = new List<ConfigItem>()
        };

        try
        {
            string jsonString = File.ReadAllText(path);
            configData = JsonSerializer.Deserialize<ConfigData>(jsonString);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error loading config file: " + ex.Message);
            return false;
        }
    }

    public static void SaveToFile(ConfigData configData, string filePath)
    {
        try
        {
            string jsonString = JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, jsonString);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error saving to file: " + ex.Message);
        }
    }
}

