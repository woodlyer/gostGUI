using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
namespace gostGUI
{
    public partial class FormMain : Form
    {
        private ProcessManager _processManager;
        private ConfigurationManager _configManager;
        private LogViewManager _logViewManager;

        private const string AppRegistryKey = "gostGUI";
        private const string RunRegistryPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

        // for listbox edit
        TextBox listbox_txtBox = new TextBox();

        public FormMain()
        {
            InitializeComponent();

            try
            {
                _processManager = new ProcessManager();
                _processManager.OutputReceived += OnProcessOutputReceived;
                _processManager.ProcessExited += OnProcessExited;
                _processManager.ProcessStarted += OnProcessStarted;
                _processManager.ProcessStopped += OnProcessStopped;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize process manager: {ex.Message}\n\nChild processes may not terminate automatically on crash.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            _configManager = new ConfigurationManager();
            _configManager.ItemAdded += OnConfigItemAdded;
            _configManager.ItemRenamed += OnConfigItemRenamed;
            _configManager.ItemDeleted += OnConfigItemDeleted;
            _configManager.ItemUpdated += OnConfigItemUpdated;

            _logViewManager = new LogViewManager(this.groupBox1, this.textBox_log);


            initFromConfig();
            
            this.textBox1.Leave += new System.EventHandler(this.textBox1_Leave);
            this.textBox_Arg.Leave += new System.EventHandler(this.textBox_Arg_Leave);
            Application.ApplicationExit += new EventHandler(this.OnApplicationExit);
            this.listBox1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.listBox1_MouseDown);
            listbox_txtBox.Leave += new System.EventHandler(this.listbox_txtBox_Leave);
            listbox_txtBox.KeyDown += new KeyEventHandler(listbox_txtBox_KeyDown);
            checkAutoStartStatus();
            startAll();
        }
        void initFromConfig()
        {
            if (_configManager.Items.Count > 0)
            {
                listBox1.Items.Clear();
                foreach (var item in _configManager.Items)
                { 
                    // Let the managers handle UI creation
                    OnConfigItemAdded(item.Name);
                }

                // Select the first item by default
                if (listBox1.Items.Count > 0)
                {
                    listBox1.SelectedIndex = 0;
                }
            }
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            // The ProcessManager will handle stopping all processes.
            _processManager?.Dispose();
        }

        private string getCurrentItem()
        {
            string selectedItemName = listBox1.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedItemName))
            {
                //MessageBox.Show("Please select an item from the list.");
                return "";
            }

            // Find the selected item in the config data
            ConfigItem selectedConfigItem = _configManager.GetItem(selectedItemName);
            if (selectedConfigItem == null)
            {
                MessageBox.Show("Selected item not found in the configuration.");
                return "";
            }
            return selectedItemName;
        }

        void start(string selectedItemName)
        {
            ConfigItem configItem = _configManager.GetItem(selectedItemName);
            _processManager?.StartProcess(configItem);
        }

        void stop()
        {
            string selectedItemName = getCurrentItem();
            if (string.IsNullOrEmpty(selectedItemName)) return;
            _processManager?.StopProcess(selectedItemName);
        }

        void stop(string itemName)
        {
            _processManager?.StopProcess(itemName);
        }

        // 进程结束的事件响应函数
        private void OnProcessExited(string itemName, int exitCode)
        {
            ConfigItem configItem = _configManager.GetItem(itemName);
            if (configItem != null)
            {
                configItem.Enable = false;
                _configManager.UpdateItem(configItem);
            }

            string exitMessage = $"!!! program exited with code {exitCode} !!!";
            
            System.Threading.Thread.Sleep(50);// ms
            update(exitMessage + Environment.NewLine, itemName);
            updateStatus(itemName); 
        }

        private void OnProcessStarted(string itemName)
        {
            ConfigItem configItem = _configManager.GetItem(itemName);
            if (configItem != null)
            {
                configItem.Enable = true;
                _configManager.UpdateItem(configItem);
            }
            outputAdd("run success.", itemName);
            outputAdd("---------------------------------------------------------", itemName);
            updateStatus(itemName);
        }
        private void OnProcessStopped(string itemName)
        {
            outputAdd("!!! stop program !!!", itemName);
            updateStatus(itemName);
        }
        // 刷新界面的显示
        void updateStatus(string itemName)
        {
            if (this.InvokeRequired)
            {
                try
                {
                    Invoke(new Action(() => updateStatus(itemName)));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Invoke failed in updateStatus: {ex.Message}");
                }
            }
            else
            {
                
                listBox1.Invalidate(); // Redraw listbox to update status for all items
            }
        }

        void OnProcessOutputReceived(string itemName, string data)
        {
            update(data, itemName);
        }

        void update(string msg, string itemName)
        {
            _logViewManager.AppendText(itemName, msg);
        }

        

        // start
        private void buttonStart_Click(object sender, EventArgs e)
        {
            string selectedItemName = getCurrentItem();
            if (!string.IsNullOrEmpty(selectedItemName))
            {
                start(selectedItemName);
            }
        }
        private void outputAdd(string str)
        {
            string itemName = getCurrentItem();
            outputAdd(str,itemName);
        }
        private void outputAdd(string str, string itemName)
        {
            _logViewManager.AppendText(itemName, str + Environment.NewLine);
        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            _processManager?.Dispose();
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.ShowInTaskbar = false;
                this.Hide();
            }
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                //this.ShowInTaskbar = false;
                contextMenuStrip1.Show(Control.MousePosition);
            }

            if (e.Button == MouseButtons.Left)
            {
                this.Visible = true;
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
            }
        }


        //stop 
        private void buttonStop_Click(object sender, EventArgs e)
        {
            string selectedItemName = getCurrentItem();
            if (!string.IsNullOrEmpty(selectedItemName))
            {
                stop(selectedItemName);
            }
        }

        //拖入应用
        private void textBox1_DragDrop(object sender, DragEventArgs e)
        {
            string path = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();       //获得路径
            textBox1.Text = path;
            update_textBox1(textBox1.Text);
        }

        private void textBox1_Leave(object sender, EventArgs e)
        {
            update_textBox1(textBox1.Text);
        }

        private void textBox1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Link;
            else
                e.Effect = DragDropEffects.None;
        }

        private void update_textBox1(string path)
        {
            string selectedItemName = listBox1.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selectedItemName))
            {
                ConfigItem selectedConfigItem = _configManager.GetItem(selectedItemName);
                if (selectedConfigItem != null)
                {
                    if (selectedConfigItem.Program != path) // Only update if changed
                    {
                        _configManager.UpdateItemProgram(selectedItemName, path);
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select an item from the list before dragging and dropping.");
            }
        }


        private void button_select_Click(object sender, EventArgs e)
        {
            OpenFileDialog fie = new OpenFileDialog();
            fie.Title = "select .exe program";

            string cdstr = System.Environment.CurrentDirectory;
            fie.InitialDirectory = cdstr;
            //对话框的初始目录
            fie.Filter = "exe|*.exe|all|*.*";
            //设置文件类型
            string str = fie.FileName;
            //获取选择的路径
            if (fie.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = fie.FileName;
                update_textBox1(textBox1.Text);
            }
        }

        private void clearButton_Click(object sender, EventArgs e)
        {
            string selectedItemName = getCurrentItem();
            if (!string.IsNullOrEmpty(selectedItemName))
            {
                _logViewManager.ClearLog(selectedItemName);
            }
        }

        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            startAll();
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            stopAll();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            stopAll();
            notifyIcon1.Dispose();
            // 整个程序退出
            Application.Exit();
        }

        private void listbox_txtBox_KeyDown(object sender, KeyEventArgs e)
        {
            //Enter键 更新项并隐藏编辑框   
            if (e.KeyCode == Keys.Enter)
            {
                string oldName = listBox1.Items[listBox1.SelectedIndex].ToString();
                string newName = listbox_txtBox.Text.Trim();

                if (string.IsNullOrEmpty(newName) || newName == oldName)
                {
                    listbox_txtBox.Visible = false;
                    return;
                }

                if (_configManager.RenameItem(oldName, newName))
                {
                    listbox_txtBox.Visible = false;
                }
            }
            //Esc键 直接隐藏编辑框   
            if (e.KeyCode == Keys.Escape)
                listbox_txtBox.Visible = false;
        }

        private void listBox1_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                return;
            }

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            e.DrawBackground();

            string itemName = listBox1.Items[e.Index].ToString();

            // Check if the process for this item is running
            bool isRunning = _processManager.IsProcessRunning(itemName);

            // Determine the color for the icon based on the selection state
            Color iconColor;
            if (isSelected)
            {
                // When selected, the background is blue. Use the same color as the text (usually white) for good contrast.
                iconColor = e.ForeColor;
            }
            else
            {
                // When not selected, use custom colors. Using a brighter green for better visibility.
                iconColor = isRunning ? Color.LimeGreen : Color.Gray;
            }

            // Define icon properties
            int iconSize = 10;
            int iconMargin = 4;
            Rectangle iconRect = new Rectangle(e.Bounds.Left + iconMargin, e.Bounds.Top + (e.Bounds.Height - iconSize) / 2, iconSize, iconSize);

            // Draw the status icon
            using (SolidBrush iconBrush = new SolidBrush(iconColor))
            {
                if (isRunning)
                {
                    // Draw a triangle (play icon)
                    Point[] points = { new Point(iconRect.Left, iconRect.Top), new Point(iconRect.Right, iconRect.Top + iconRect.Height / 2), new Point(iconRect.Left, iconRect.Bottom) };
                    e.Graphics.FillPolygon(iconBrush, points);
                }
                else
                {
                    // Draw a square (stop icon)
                    e.Graphics.FillRectangle(iconBrush, iconRect);
                }
            }

            // Define the bounds for the text, shifted to the right of the icon
            Rectangle textRect = new Rectangle(iconRect.Right + iconMargin, e.Bounds.Top, e.Bounds.Width - iconRect.Right - iconMargin * 2, e.Bounds.Height);
            // The system automatically sets e.ForeColor to the correct color for the state (selected/not selected)
            TextRenderer.DrawText(e.Graphics, itemName, e.Font, textRect, e.ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            e.DrawFocusRectangle();
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            int index = listBox1.SelectedIndex;
            if (index != ListBox.NoMatches)
            {
                string itemName = listBox1.Items[index].ToString();
                bool isRunning = _processManager.IsProcessRunning(itemName);

                // Toggle start/stop
                if (isRunning) stop(itemName); else start(itemName);
            }
        }

        private void listBox1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int index = listBox1.IndexFromPoint(e.Location);
            if (index != ListBox.NoMatches)
            {
                Rectangle itemBounds = listBox1.GetItemRectangle(index);
                
                // Recreate the icon rectangle logic from the DrawItem event
                int iconSize = 10;
                int iconMargin = 4;
                Rectangle iconRect = new Rectangle(itemBounds.Left + iconMargin, itemBounds.Top + (itemBounds.Height - iconSize) / 2, iconSize, iconSize);

                // Check if the click was inside the icon's bounds
                if (iconRect.Contains(e.Location))
                {
                    string itemName = listBox1.Items[index].ToString();
                    bool isRunning = _processManager.IsProcessRunning(itemName);

                    // Toggle start/stop
                    if (isRunning) stop(itemName); else start(itemName);
                }
            }
        }

        private void listBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                int index = listBox1.IndexFromPoint(e.Location);
                if (index != ListBox.NoMatches)
                {
                    listBox1.SelectedIndex = index;
                }
            }
        }

        private void selectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //MessageBox.Show("Active");
        }

        private void listbox_txtBox_Leave(object sender, EventArgs e)
        {
            // Commit changes when the textbox loses focus.
            EndListBoxEdit(true);
        }
        private void EndListBoxEdit(bool commitChanges)
        {
            // If the textbox is not visible, there is nothing to do.
            if (!listbox_txtBox.Visible)
            {
                return;
            }

            if (commitChanges)
            {
                int selectedIndex = listBox1.SelectedIndex;
                if (selectedIndex < 0)
                {
                    listbox_txtBox.Visible = false;
                    return;
                }

                string oldName = listBox1.Items[selectedIndex].ToString();
                string newName = listbox_txtBox.Text.Trim();

                // If name is unchanged or empty, just hide the textbox.
                if (string.IsNullOrEmpty(newName) || newName == oldName)
                {
                    listbox_txtBox.Visible = false;
                    return;
                }

                // Check for duplicates
                if (_configManager.RenameItem(oldName, newName))
                {
                    listbox_txtBox.Visible = false; // Success, hide the box
                }
                else
                {
                    listbox_txtBox.Focus(); // Keep the textbox focused for correction
                }
            }

            listbox_txtBox.Visible = false;
        }
        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int itemSelected = listBox1.SelectedIndex;
            if (itemSelected < 0)
            {
                return;
            }
            string itemText = listBox1.Items[itemSelected].ToString();

            Rectangle rect = listBox1.GetItemRectangle(itemSelected);
            listbox_txtBox.Parent = listBox1;
            listbox_txtBox.Bounds = rect;

            listbox_txtBox.Multiline = false; // A single line is enough for the name
            listbox_txtBox.Visible = true;
            listbox_txtBox.Text = itemText;
            listbox_txtBox.Focus();
            listbox_txtBox.SelectAll();
        }
        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string newItemName = _configManager.AddNewItem();
            listBox1.SelectedItem = newItemName; // Select the new item
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int itemSelected = listBox1.SelectedIndex;
            if (itemSelected < 0) return;

            string itemText = listBox1.Items[itemSelected].ToString();
            stop(itemText); // Stop the process if it's running
            _configManager.DeleteItem(itemText);
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                string selectedItemName = listBox1.SelectedItem.ToString();

                // Find the selected item in the config data
                ConfigItem selectedConfigItem = _configManager.GetItem(selectedItemName);

                if (selectedConfigItem != null)
                {
                    // Update the textboxes with the selected item's data
                    textBox1.Text = selectedConfigItem.Program;
                    textBox_Arg.Text = selectedConfigItem.Args;

                    // Show the selected textBox_log and hide others
                    _logViewManager.ShowLogView(selectedItemName);
                }
            }
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            //listBox1.SelectedIndex = 0;
        }

        private void textBox_Arg_Leave(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                string selectedItemName = listBox1.SelectedItem.ToString();
                ConfigItem selectedConfigItem = _configManager.GetItem(selectedItemName);
                if (selectedConfigItem != null && selectedConfigItem.Args != textBox_Arg.Text)
                {
                    selectedConfigItem.Args = textBox_Arg.Text; 
                    _configManager.UpdateItem(selectedConfigItem);
                }
            }
        }

        private void contextMenuStrip_listbox_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (listBox1.SelectedItem == null)
            {
                e.Cancel = true;
                return;
            }

            string selectedItemName = listBox1.SelectedItem.ToString();
            bool isRunning = _processManager.IsProcessRunning(selectedItemName);

            startToolStripMenuItem_listbox.Enabled = !isRunning;
            stopToolStripMenuItem_listbox.Enabled = isRunning;
        }

        private void startToolStripMenuItem_listbox_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                start(listBox1.SelectedItem.ToString());
            }
        }

        private void stopToolStripMenuItem_listbox_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                stop(listBox1.SelectedItem.ToString());
            }
        }
        #region ConfigurationManager Event Handlers

        private void OnConfigItemAdded(string itemName)
        {
            _logViewManager.AddLogView(itemName);
            listBox1.Items.Add(itemName);
        }

        private void OnConfigItemRenamed(string oldName, string newName)
        {
            _logViewManager.RenameLogView(oldName, newName);

            int index = listBox1.Items.IndexOf(oldName);
            if (index != -1)
            {
                listBox1.Items[index] = newName;
            }
        }

        private void OnConfigItemDeleted(string itemName)
        {
            _logViewManager.RemoveLogView(itemName);
            listBox1.Items.Remove(itemName);
        }

        private void OnConfigItemUpdated(ConfigItem item)
        {
            // This event can be used to refresh specific UI parts if needed in the future.
            // For now, most updates are handled directly.
        }

        #endregion
        private void button_Exit_Click(object sender, EventArgs e)
        {
            exitToolStripMenuItem_Click(sender, e);
        }
        private void startAll()
        {
            foreach (ConfigItem item in _configManager.Items)
            {
                if (item.Enable && !_processManager.IsProcessRunning(item.Name))
                {
                    start(item.Name);
                }
            }
        }
        private void button_StartAll_Click(object sender, EventArgs e)
        {
            startAll();
        }

        private void stopAll()
        {
            _processManager?.StopAllProcesses();
        }

        private void button_stopAll_Click(object sender, EventArgs e)
        {
            stopAll();
        }

        private void checkAutoStartStatus()
        {
            RegistryKey rkApp = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false);

            Object obj = rkApp?.GetValue(AppRegistryKey);
            if (obj == null)
                return;

            String value = obj.ToString();
            if (value == null)
            {
                checkBox1_autostart.Checked = false;
                return;
            }

            if (value == Application.ExecutablePath.ToString())
            {
                checkBox1_autostart.Checked = true;
            }
            rkApp.Close();
        }
  
        private void checkBox1_autostart_Click(object sender, EventArgs e)
        {
            try
            {
                using (RegistryKey rkApp = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
                {
                    if (checkBox1_autostart.Checked)
                    {
                        rkApp.SetValue(AppRegistryKey, Application.ExecutablePath);
                        MessageBox.Show("Auto-start enabled successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        rkApp.DeleteValue(AppRegistryKey, false);
                        MessageBox.Show("Auto-start disabled.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to update auto-start setting: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
