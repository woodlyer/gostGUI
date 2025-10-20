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

        public event Action<string> ItemAdded;
        public event Action<string, string> ItemRenamed;
        public event Action<string> ItemDeleted;
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

        private void Save()
        {
            Common.SaveToFile(_configData, _configFilePath);
        }

        public ConfigItem GetItem(string itemName)
        {
            return _configData.Items.FirstOrDefault(item => item.Name == itemName);
        }

        public string AddNewItem()
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
            ItemAdded?.Invoke(newItemName);
            return newItemName;
        }

        public void DeleteItem(string itemName)
        {
            _configData.Items.RemoveAll(item => item.Name == itemName);
            Save();
            ItemDeleted?.Invoke(itemName);
        }

        public bool RenameItem(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(newName) || oldName == newName)
            {
                return false;
            }

            if (_configData.Items.Exists(item => item.Name == newName))
            {
                System.Windows.Forms.MessageBox.Show("An item with this name already exists.", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }

            ConfigItem itemToRename = GetItem(oldName);
            if (itemToRename != null)
            {
                itemToRename.Name = newName;
                Save();
                ItemRenamed?.Invoke(oldName, newName);
                return true;
            }
            return false;
        }

        public void UpdateItemProgram(string itemName, string programPath)
        {
            ConfigItem item = GetItem(itemName);
            if (item != null && item.Program != programPath)
            {
                item.Program = programPath;
                Save();
                ItemUpdated?.Invoke(item);
            }
        }

        public void UpdateItem(ConfigItem updatedItem)
        {
            ConfigItem item = GetItem(updatedItem.Name);
            if (item != null)
            {
                // This could be more granular if needed
                item.Args = updatedItem.Args;
                item.Enable = updatedItem.Enable;
                item.Program = updatedItem.Program;
                Save();
                ItemUpdated?.Invoke(item);
            }
        }
    }
}