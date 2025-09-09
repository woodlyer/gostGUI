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
        ConfigData configData;
        // Dictionaries to hold the running process and log textbox for each configuration item.
        private Dictionary<string, Process> processes = new Dictionary<string, Process>();
        private Dictionary<string, TextBox> textBoxLogs = new Dictionary<string, TextBox>();
        private JobObjectManager _jobManager;

        // for listbox edit
        TextBox listbox_txtBox = new TextBox();

        public FormMain()
        {
            InitializeComponent();

            // Create a job object. All child processes will be assigned to this job.
            // If the main application terminates (even crashes), all processes in the job will be killed by the OS.
            try
            {
                _jobManager = new JobObjectManager();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to create Job Object for process management. Child processes may not terminate automatically on crash.\n\nError: " + ex.Message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            initFromConfig();
            checkAutoStartStatus();
            
            this.textBox1.Leave += new System.EventHandler(this.textBox1_Leave);
            this.textBox_Arg.Leave += new System.EventHandler(this.textBox_Arg_Leave);
            Application.ApplicationExit += new EventHandler(this.OnApplicationExit);
            listbox_txtBox.Leave += new System.EventHandler(this.listbox_txtBox_Leave);
            listbox_txtBox.KeyDown += new KeyEventHandler(listbox_txtBox_KeyDown);
        }
        void initFromConfig()
        {
            string exeDirPath = (Common.GetApplicationPath());
            string path = exeDirPath + "/config.json";

            Common.LoadJson(path, out configData);

            if (configData != null)
            {
                //textBox1.Text = configData.Program;
                listBox1.Items.Clear();
                foreach (var item in configData.Items)
                { 
                    listBox1.Items.Add(item.Name);

                    // Create a new textBox_log for each item
                    TextBox textBoxLog = new TextBox();
                    textBoxLog.Location = textBox_log.Location;
                    textBoxLog.Size = textBox_log.Size;
                    textBoxLog.BackColor = textBox_log.BackColor;
                    textBoxLog.ForeColor = textBox_log.ForeColor;
                    textBoxLog.Multiline = true;
                    textBoxLog.ReadOnly = true;
                    textBoxLog.ScrollBars = textBox_log.ScrollBars; // Copy scrollbar settings
                    textBoxLog.WordWrap = textBox_log.WordWrap;   // Copy word wrap settings
                    textBoxLog.TextAlign = textBox_log.TextAlign;   // Copy text alignment
                    textBoxLog.BorderStyle = textBox_log.BorderStyle; // Copy border style
                    textBoxLog.Visible = true; // Initially hide all textBox_log
                    textBoxLogs[item.Name] = textBoxLog;

                    // Add the textBoxLog to the form (you might need to adjust the location and size)
                    this.groupBox1.Controls.Add(textBoxLog);
                    //this.groupBox1.Controls.SetChildIndex(textBoxLog, 0);
                }

                // old
                textBox_log.Visible = false;
                // Select the first item by default
                if (listBox1.Items.Count > 0)
                {
                    listBox1.SelectedIndex = 0;
                }
            }
        }

        void saveCfgToFile()
        {
            string cfgFile = Common.GetApplicationPath() + "/config.json";
            Common.SaveToFile(configData, cfgFile);
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            stopAll();
            // This ensures the job object handle is closed cleanly on normal exit.
            _jobManager?.Dispose();
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
            ConfigItem selectedConfigItem = configData.Items.Find(item => item.Name == selectedItemName);
            if (selectedConfigItem == null)
            {
                MessageBox.Show("Selected item not found in the configuration.");
                return "";
            }
            return selectedItemName;
        }

        void start(string selectedItemName)
        {
            ConfigItem selectedConfigItem = configData.Items.Find(item => item.Name == selectedItemName);
            if (selectedConfigItem == null)
            {
                outputAdd($"!!! Configuration for '{selectedItemName}' not found.", selectedItemName);
                return;
            }

            string exeFilePath = selectedConfigItem.Program;
            string argsstr = selectedConfigItem.Args;

            if (!File.Exists(exeFilePath))
            {
                outputAdd("!!! Program file does not exist !!!", selectedItemName);
                return;
            }

            // 如果正在运行，就直接返回。 
            // Stop the existing process if it is running
            stop(selectedItemName);

            // Create a new process
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.WorkingDirectory = Common.GetApplicationPath();
            p.StartInfo.RedirectStandardInput = false;
            p.StartInfo.RedirectStandardOutput = true;
            // Set the encoding for the output streams to handle non-ASCII characters (like Chinese) correctly.
            p.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
            // Pass the selectedItemName to the event handler
            p.OutputDataReceived += new DataReceivedEventHandler((sender, e) => p_OutputDataReceived(sender, e, selectedItemName));
            p.StartInfo.RedirectStandardError = true;
            // Set the encoding for the error streams as well.
            p.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
            // Pass the selectedItemName to the event handler
            p.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => p_OutputDataReceived(sender, e, selectedItemName));
            p.EnableRaisingEvents = true;
            p.Exited += new EventHandler((sender, e) => p_Exit(sender, e, selectedItemName));

            // Set the program and arguments
            p.StartInfo.FileName = exeFilePath;
            p.StartInfo.Arguments = argsstr;
            bool startOK = false;
            try
            {
                startOK = p.Start();

            }
            catch (Exception ex)
            {
                update(ex.Message, selectedItemName);
            }

            if (startOK)
            {
                // Add the new process to the job object to ensure it's terminated if the parent crashes.
                _jobManager?.AddProcess(p);

                selectedConfigItem.Enable = true;
                saveCfgToFile();

                outputAdd("run sucess.", selectedItemName);
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (listBox1.SelectedItem?.ToString() == selectedItemName)
                {
                    //button_start.Enabled = false;
                    //button_stop.Enabled = true;
                }

                // Store the process in the dictionary
                processes[selectedItemName] = p;
                listBox1.Invalidate(); // Redraw listbox to show running icon
            }
            else
            {
                outputAdd("run failed.", selectedItemName);
            }
            outputAdd("---------------------------------------------------------", selectedItemName);
        }

        void stop()
        {
            string selectedItemName = getCurrentItem();
            if (string.IsNullOrEmpty(selectedItemName)) return;

            stop(selectedItemName);
        }

        void stop(string itemName)
        {
            if (!processes.ContainsKey(itemName))
            {
                return;
            }

            Process p = processes[itemName];
            if (p == null || p.HasExited)
            {
                processes.Remove(itemName);
                updateStatus(itemName);
                return;
            }

            try
            {
                p.Kill();
                p.Close();
                p.Dispose();
            }
            catch (Exception ex)
            {
                update(ex.Message, itemName);
            }

            processes.Remove(itemName);
            outputAdd("!!! stop program !!!", itemName);
            listBox1.Invalidate(); // Redraw listbox to show stopped icon
        }

        // 进程结束的事件响应函数
        private void p_Exit(object sender, System.EventArgs e, string itemName)
        {
            ConfigItem configItem = configData.Items.Find(item => item.Name == itemName);
            if (configItem != null)
            {
                configItem.Enable = false;
                saveCfgToFile();
            }

            Process p = sender as Process;
            string exitMessage = $"!!! program exits !!!";
            
            System.Threading.Thread.Sleep(50);// ms
            update(exitMessage + Environment.NewLine, itemName);
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

        void p_OutputDataReceived(object sender, DataReceivedEventArgs e, string itemName)
        {
            if (e.Data != null)
            {
                update(e.Data + Environment.NewLine, itemName);
            }
        }

        void update(string msg, string itemName)
        {
            if (this.InvokeRequired)
            {
                try
                {
                    Invoke(new Action(() => update(msg, itemName)));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Invoke failed in update: {ex.Message}");
                }
            }
            else
            {
                if (textBoxLogs.ContainsKey(itemName))
                {
                    TextBox textBoxLog = textBoxLogs[itemName];
                    // Get the current vertical scroll position
                    textBoxLog.AppendText(msg);
                    if (textBoxLog.Text.Length > textBoxLog.MaxLength)
                        textBoxLog.Clear();

                    // Set the vertical scroll position to the bottom
                    textBoxLog.ScrollToCaret();
                }
            }
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
            try
            {
                textBoxLogs[itemName].AppendText(str);
                textBoxLogs[itemName].AppendText(Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to add output for item {itemName}: {ex.Message}");
            }
        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            stopAll();
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
                // Find the selected item in the config data
                ConfigItem selectedConfigItem = configData.Items.Find(item => item.Name == selectedItemName);
                if (selectedConfigItem != null)
                {
                    if (selectedConfigItem.Program != path)
                    {
                        // Update the Program property of the selected item
                        selectedConfigItem.Program = path;
                        saveCfgToFile();
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
                if (textBoxLogs.ContainsKey(selectedItemName))
                {
                    textBoxLogs[selectedItemName].Clear();
                }
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

                if (configData.Items.Exists(item => item.Name == newName))
                {
                    MessageBox.Show("An item with this name already exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                ConfigItem itemToRename = configData.Items.Find(item => item.Name == oldName);
                if (itemToRename != null)
                {
                    itemToRename.Name = newName;

                    // Update dictionaries
                    if (processes.ContainsKey(oldName))
                    {
                        processes[newName] = processes[oldName];
                        processes.Remove(oldName);
                    }
                    if (textBoxLogs.ContainsKey(oldName))
                    {
                        textBoxLogs[newName] = textBoxLogs[oldName];
                        textBoxLogs.Remove(oldName);
                    }

                    listBox1.Items[listBox1.SelectedIndex] = newName;
                    saveCfgToFile();
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
            bool isRunning = processes.ContainsKey(itemName) && processes[itemName] != null && !processes[itemName].HasExited;

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
            int itemSelected = listBox1.SelectedIndex;
            string itemText = listBox1.Items[itemSelected].ToString();

            Rectangle rect = listBox1.GetItemRectangle(itemSelected);
            listbox_txtBox.Parent = listBox1;
            rect.Height += 5;
            listbox_txtBox.Bounds = rect;

            listbox_txtBox.Multiline = true;
            listbox_txtBox.Visible = true;
            listbox_txtBox.Text = itemText;
            listbox_txtBox.Focus();
            listbox_txtBox.SelectAll();
        }

        private void listBox1_MouseClick(object sender, MouseEventArgs e)
        {
            // This method used to hide the edit box, effectively cancelling the edit.
            // Now, the Leave event of the textbox handles committing the changes,
            // so this method is no longer needed for that purpose.
            // A single click on the listbox will cause the textbox to lose focus,
            // which correctly triggers the Leave event.
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
                if (configData.Items.Exists(item => item.Name == newName))
                {
                    MessageBox.Show("An item with this name already exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    listbox_txtBox.Focus(); // Keep the textbox focused for correction
                    return; // Do not hide the textbox
                }

                // Commit the changes
                ConfigItem itemToRename = configData.Items.Find(item => item.Name == oldName);
                if (itemToRename != null)
                {
                    itemToRename.Name = newName;

                    // Update dictionaries
                    if (processes.ContainsKey(oldName))
                    {
                        processes[newName] = processes[oldName];
                        processes.Remove(oldName);
                    }
                    if (textBoxLogs.ContainsKey(oldName))
                    {
                        textBoxLogs[newName] = textBoxLogs[oldName];
                        textBoxLogs.Remove(oldName);
                    }

                    listBox1.Items[selectedIndex] = newName;
                    saveCfgToFile();
                }
            }

            listbox_txtBox.Visible = false;
        }
        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listBox1_DoubleClick(sender, e);
        }
        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string newItemName = "new item";
            int counter = 1;
            while (configData.Items.Exists(item => item.Name == newItemName))
            {
                newItemName = $"new item {counter++}";
            }

            ConfigItem newItem = new ConfigItem
            {
                Name = newItemName,
                Enable = true,
                Program = "gost.exe",
                Args = ""
            };

            configData.Items.Add(newItem);

            // Create and add a new log textbox for the new item
            TextBox textBoxLog = new TextBox();
            textBoxLog.Location = this.textBox_log.Location;
            textBoxLog.Size = this.textBox_log.Size;
            textBoxLog.BackColor = this.textBox_log.BackColor;
            textBoxLog.ForeColor = this.textBox_log.ForeColor;
            textBoxLog.Multiline = true;
            textBoxLog.ReadOnly = true;
            textBoxLog.ScrollBars = this.textBox_log.ScrollBars;
            textBoxLog.WordWrap = this.textBox_log.WordWrap;
            textBoxLog.TextAlign = this.textBox_log.TextAlign;
            textBoxLog.BorderStyle = this.textBox_log.BorderStyle;
            textBoxLog.Visible = false; // Hide it initially
            textBoxLogs[newItem.Name] = textBoxLog;
            this.groupBox1.Controls.Add(textBoxLog);

            saveCfgToFile();
            listBox1.Items.Add(newItem.Name);
            listBox1.SelectedItem = newItem.Name; // Select the new item
        }


        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int itemSelected = listBox1.SelectedIndex;
            if (itemSelected < 0) return;

            string itemText = listBox1.Items[itemSelected].ToString();
            stop(itemText); // Stop the process if it's running
            configData.Items.RemoveAll(item => item.Name == itemText);
            this.groupBox1.Controls.Remove(textBoxLogs[itemText]);
            textBoxLogs.Remove(itemText);
            saveCfgToFile();
            listBox1.Items.Remove(itemText);
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                string selectedItemName = listBox1.SelectedItem.ToString();

                // Find the selected item in the config data
                ConfigItem selectedConfigItem = configData.Items.Find(item => item.Name == selectedItemName);

                if (selectedConfigItem != null)
                {
                    // Update the textboxes with the selected item's data
                    textBox1.Text = selectedConfigItem.Program;
                    textBox_Arg.Text = selectedConfigItem.Args;

                    // 检查所选进程的运行状态，与 listBox1_DrawItem 中的逻辑保持一致
                    //bool isRunning = processes.ContainsKey(selectedItemName) && processes[selectedItemName] != null && !processes[selectedItemName].HasExited;

                    // 更新按钮状态以反映所选项的真实状态
                    //button_start.Enabled = !isRunning;
                    //button_stop.Enabled = isRunning;

                    // Show the selected textBox_log and hide others
                    foreach (var entry in textBoxLogs)
                    {
                        entry.Value.Visible = (entry.Key == selectedItemName);
                    }
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
                ConfigItem selectedConfigItem = configData.Items.Find(item => item.Name == selectedItemName);
                if (selectedConfigItem != null && selectedConfigItem.Args != textBox_Arg.Text)
                {
                    selectedConfigItem.Args = textBox_Arg.Text; 
                    saveCfgToFile();
                }
            }
        }

        private void button_Exit_Click(object sender, EventArgs e)
        {
            exitToolStripMenuItem_Click(sender, e);
        }
        private void startAll()
        {
            foreach (ConfigItem item in configData.Items)
            {
                if (item.Enable && (!processes.ContainsKey(item.Name) || (processes.ContainsKey(item.Name) && processes[item.Name].HasExited)))
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
            List<string> runningItems = new List<string>(processes.Keys);
            foreach (string itemName in runningItems)
            {
                stop(itemName);
            }
        }

        private void button_stopAll_Click(object sender, EventArgs e)
        {
            stopAll();
        }

        private void checkAutoStartStatus()
        {
            RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            Object obj = rkApp.GetValue("gostGUI");
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

        }
  
        private void checkBox1_autostart_Click(object sender, EventArgs e)
        {
            RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (checkBox1_autostart.Checked)
            {
                rkApp.SetValue("gostGUI", Application.ExecutablePath.ToString());
                MessageBox.Show("active auto start sucess.");
            }
            else
            {
                // no auto start
                rkApp.DeleteValue("gostGUI", false);
                MessageBox.Show("disable auto start.");
            }
        }
    }
}
