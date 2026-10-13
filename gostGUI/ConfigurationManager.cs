using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace gostGUI
{
    public class ConfigurationManager
    {
        private const string ConfigFileName = "config.json";
        private readonly string _configFilePath;
        private ConfigData _configData;

        public IReadOnlyList<ConfigItem> Items => _configData.Items.AsReadOnly();

        public event Action<ConfigItem> ItemAdded;
        public event Action<ConfigItem, string> ItemRenamed;
        public event Action<ConfigItem> ItemDeleted;
        public event Action<ConfigItem> ItemUpdated;

        public ConfigurationManager()
        {
            _configFilePath = Path.Combine(Common.GetApplicationPath(), ConfigFileName);
            Load();
        }

        private void Load()
        {
            Common.LoadJson(_configFilePath, out _configData);
        }

        public void Save()
        {
            Common.SaveToFile(_configData, _configFilePath);
        }

        public ConfigItem AddNewItem()
        {
            string baseName = "new item";
            string newItemName = baseName;
            int counter = 1;
            while (_configData.Items.Exists(item => item.Name == newItemName))
            {
                newItemName = $"{baseName} {counter++}";
            }

            var newItem = new ConfigItem
            {
                Name = newItemName,
                Enable = false, // Start disabled by default
                Program = "gost.exe",
                Args = ""
            };

            _configData.Items.Add(newItem);
            Save();
            ItemAdded?.Invoke(newItem);
            return newItem;
        }

        public void DeleteItem(ConfigItem item)
        {
            if (item == null) return;
            _configData.Items.Remove(item);
            Save();
            ItemDeleted?.Invoke(item);
        }

        public bool RenameItem(ConfigItem item, string newName)
        {
            if (item == null || string.IsNullOrEmpty(newName))
            {
                return false;
            }

            if (item.Name == newName)
            {
                return false;
            }

            if (_configData.Items.Exists(i => !ReferenceEquals(i, item) && i.Name == newName))
            {
                System.Windows.Forms.MessageBox.Show("An item with this name already exists.", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }

            item.Name = newName;
            Save();
            ItemRenamed?.Invoke(item, newName);
            return true;
        }

        public void UpdateItemProgram(ConfigItem item, string programPath)
        {
            if (item != null && item.Program != programPath)
            {
                item.Program = programPath;
                Save();
                ItemUpdated?.Invoke(item);
            }
        }

        public void UpdateItem(ConfigItem item)
        {
            if (item != null)
            {
                Save();
                ItemUpdated?.Invoke(item);
            }
        }
    }
}