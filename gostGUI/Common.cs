﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;



public class ConfigItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("enable")]
    public bool Enable { get; set; }

    [JsonPropertyName("program")]
    public string Program { get; set; }

    [JsonPropertyName("args")]
    public string Args { get; set; }
}


public class ConfigData
{
    [JsonPropertyName("autoRun")]
    public bool AutoRun { get; set; }

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
            AutoRun = false,
            Items = new List<ConfigItem>()
        };

        try
        {
            string jsonString = File.ReadAllText(path);
            configData = JsonSerializer.Deserialize<ConfigData>(jsonString);
            return true;
        }
        catch (FileNotFoundException)
        {
            // Config file doesn't exist, which is fine on first run.
            // A new one will be created.
            return true; 
        }
        catch (JsonException ex)
        {
            System.Windows.Forms.MessageBox.Show("Error parsing config.json: " + ex.Message, "Config Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            return false;
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show("Error loading config file: " + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            return false;
        }
    }

    public static void SaveToFile(ConfigData configData, string filePath)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };
            string jsonString = JsonSerializer.Serialize(configData, options);
            File.WriteAllText(filePath, jsonString);
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show("Error saving to file: " + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
        }
    }
}
