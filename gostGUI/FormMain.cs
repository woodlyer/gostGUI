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
            checkBox1_autostart.Checked = _configManager.AutoRun;
            if (_configManager.AutoRun)
            {
                startAll();
            }
        }
        void initFromConfig()
        {
            listBox1.Items.Clear();
            if (_configManager.Items.Count > 0)
            {
                foreach (var item in _configManager.Items)
                { 
                    // Let the managers handle UI creation
                    OnConfigItemAdded(item);
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
            _isExiting = true;
            // The ProcessManager will handle stopping all processes.
            _processManager?.Dispose();
        }

        private ConfigItem GetCurrentConfigItem()
        {
            return listBox1.SelectedItem as ConfigItem;
        }

        void start(ConfigItem item)
        {
            if (item != null)
            {
                _processManager?.StartProcess(item);
            }
        }

        void stop()
        {
            ConfigItem item = GetCurrentConfigItem();
            if (item != null)
            {
                _processManager?.StopProcess(item);
            }
        }

        void stop(ConfigItem item)
        {
            if (item != null)
            {
                _processManager?.StopProcess(item);
            }
        }

        private bool _isExiting = false;

        // 进程结束的事件响应函数
        private void OnProcessExited(ConfigItem item, int exitCode)
        {
            if (!_isExiting && item != null)
            {
                item.Enable = false;
                _configManager.UpdateItem(item);
            }

            string exitMessage = $"!!! program exited with code {exitCode} !!!";
            update(exitMessage + Environment.NewLine, item);
            updateStatus(item); 
        }

        private void OnProcessStarted(ConfigItem item)
        {
            if (item != null)
            {
                item.Enable = true;
                _configManager.UpdateItem(item);
            }
            outputAdd("run success.", item);
            outputAdd("---------------------------------------------------------", item);
            updateStatus(item);
        }
        private void OnProcessStopped(ConfigItem item)
        {
            if (!_isExiting && item != null)
            {
                item.Enable = false;
                _configManager.UpdateItem(item);
            }
            outputAdd("!!! stop program !!!", item);
            updateStatus(item);
        }
        // 刷新界面的显示
        void updateStatus(ConfigItem item)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            if (this.InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => updateStatus(item)));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"BeginInvoke failed in updateStatus: {ex.Message}");
                }
            }
            else
            {
                listBox1.Invalidate(); // Redraw listbox to update status for all items
            }
        }

        void OnProcessOutputReceived(ConfigItem item, string data)
        {
            update(data, item);
        }

        void update(string msg, ConfigItem item)
        {
            _logViewManager.AppendText(item, msg);
        }

        

        // start
        private void buttonStart_Click(object sender, EventArgs e)
        {
            ConfigItem item = GetCurrentConfigItem();
            if (item != null)
            {
                start(item);
            }
        }
        private void outputAdd(string str)
        {
            ConfigItem item = GetCurrentConfigItem();
            if (item != null)
            {
                outputAdd(str, item);
            }
        }
        private void outputAdd(string str, ConfigItem item)
        {
            _logViewManager.AppendText(item, str + Environment.NewLine);
        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            _isExiting = true;
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

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const int SW_RESTORE = 9;
        private const byte VK_MENU = 0x12; // Alt key
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private void RestoreAndBringToFront()
        {
            if (!this.Visible)
            {
                this.Visible = true;
            }

            this.ShowInTaskbar = true;

            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }

            ShowWindow(this.Handle, SW_RESTORE);

            // 当窗口未最小化但被其他窗口遮挡时，通过 TopMost 快速置顶并恢复，确保冲破遮挡
            this.TopMost = true;
            this.BringToFront();
            this.Activate();
            this.TopMost = false;

            // 模拟一次 Alt 击键以安全获取 SetForegroundWindow 前台切换权限（彻底消除 AttachThreadInput 跨线程死锁隐患）
            keybd_event(VK_MENU, 0, 0, 0);
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0);
            SetForegroundWindow(this.Handle);
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                //this.ShowInTaskbar = false;
                contextMenuStrip1.Show(Control.MousePosition);
            }
            else if (e.Button == MouseButtons.Left)
            {
                RestoreAndBringToFront();
            }
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            RestoreAndBringToFront();
        }


        //stop 
        private void buttonStop_Click(object sender, EventArgs e)
        {
            stop();
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
            ConfigItem selectedConfigItem = GetCurrentConfigItem();
            if (selectedConfigItem != null)
            {
                if (selectedConfigItem.Program != path) // Only update if changed
                {
                    _configManager.UpdateItemProgram(selectedConfigItem, path);
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
            ConfigItem item = GetCurrentConfigItem();
            if (item != null)
            {
                _logViewManager.ClearLog(item);
            }
        }

        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            startAll(true);
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            stopAll();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _isExiting = true;
            _processManager?.StopAllProcesses();
            notifyIcon1.Dispose();
            // 整个程序退出
            Application.Exit();
        }

        private void listbox_txtBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Enter key commits edit
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                EndListBoxEdit(true);
            }
            // Esc key cancels edit
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                EndListBoxEdit(false);
            }
        }

        private void listBox1_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= listBox1.Items.Count)
            {
                return;
            }

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            e.DrawBackground();

            ConfigItem configItem = listBox1.Items[e.Index] as ConfigItem;
            if (configItem == null) return;

            string itemName = configItem.Name;

            // Check if the process for this item is running
            bool isRunning = _processManager.IsProcessRunning(configItem);

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
            if (index != ListBox.NoMatches && index >= 0 && index < listBox1.Items.Count)
            {
                ConfigItem item = listBox1.Items[index] as ConfigItem;
                if (item != null)
                {
                    bool isRunning = _processManager.IsProcessRunning(item);

                    // Toggle start/stop
                    if (isRunning) stop(item); else start(item);
                }
            }
        }

        private void listBox1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int index = listBox1.IndexFromPoint(e.Location);
            if (index != ListBox.NoMatches && index >= 0 && index < listBox1.Items.Count)
            {
                Rectangle itemBounds = listBox1.GetItemRectangle(index);
                
                // Recreate the icon rectangle logic from the DrawItem event
                int iconSize = 10;
                int iconMargin = 4;
                Rectangle iconRect = new Rectangle(itemBounds.Left + iconMargin, itemBounds.Top + (itemBounds.Height - iconSize) / 2, iconSize, iconSize);

                // Check if the click was inside the icon's bounds
                if (iconRect.Contains(e.Location))
                {
                    ConfigItem item = listBox1.Items[index] as ConfigItem;
                    if (item != null)
                    {
                        bool isRunning = _processManager.IsProcessRunning(item);

                        // Toggle start/stop
                        if (isRunning) stop(item); else start(item);
                    }
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
        private ConfigItem _currentEditingItem = null;

        private void EndListBoxEdit(bool commitChanges)
        {
            // If the textbox is not visible, there is nothing to do.
            if (!listbox_txtBox.Visible)
            {
                _currentEditingItem = null;
                return;
            }

            if (commitChanges && _currentEditingItem != null)
            {
                string oldName = _currentEditingItem.Name ?? "";
                string newName = listbox_txtBox.Text.Trim();

                // If name is unchanged or empty, just hide the textbox.
                if (string.IsNullOrEmpty(newName) || newName == oldName)
                {
                    _currentEditingItem = null;
                    listbox_txtBox.Visible = false;
                    return;
                }

                // Check for duplicates
                if (_configManager.RenameItem(_currentEditingItem, newName))
                {
                    _currentEditingItem = null;
                    listbox_txtBox.Visible = false; // Success, hide the box
                    listBox1.Invalidate();
                    return;
                }
                else
                {
                    listbox_txtBox.Focus(); // Keep the textbox focused for correction
                    return;
                }
            }

            _currentEditingItem = null;
            listbox_txtBox.Visible = false;
        }
        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int itemSelected = listBox1.SelectedIndex;
            if (itemSelected < 0 || itemSelected >= listBox1.Items.Count)
            {
                return;
            }
            ConfigItem item = listBox1.Items[itemSelected] as ConfigItem;
            if (item == null)
            {
                return;
            }

            Rectangle rect = listBox1.GetItemRectangle(itemSelected);
            listbox_txtBox.Parent = listBox1;
            listbox_txtBox.Bounds = rect;

            listbox_txtBox.Multiline = false; // A single line is enough for the name
            _currentEditingItem = item;
            listbox_txtBox.Text = item.Name;
            listbox_txtBox.Visible = true;
            listbox_txtBox.Focus();
            listbox_txtBox.SelectAll();
        }
        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EndListBoxEdit(false);
            ConfigItem newItem = _configManager.AddNewItem();
            listBox1.SelectedItem = newItem; // Select the new item
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EndListBoxEdit(false);
            int itemSelected = listBox1.SelectedIndex;
            if (itemSelected < 0 || itemSelected >= listBox1.Items.Count) return;

            ConfigItem item = listBox1.Items[itemSelected] as ConfigItem;
            if (item == null) return;

            stop(item); // Stop the process if it's running
            _configManager.DeleteItem(item);
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigItem selectedConfigItem = GetCurrentConfigItem();
            if (selectedConfigItem != null)
            {
                // Update the textboxes with the selected item's data
                textBox1.Text = selectedConfigItem.Program ?? "";
                textBox_Arg.Text = selectedConfigItem.Args ?? "";

                // Show the selected textBox_log and hide others
                _logViewManager.ShowLogView(selectedConfigItem);
            }
            else
            {
                textBox1.Text = "";
                textBox_Arg.Text = "";
            }
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            //listBox1.SelectedIndex = 0;
        }

        private void textBox_Arg_Leave(object sender, EventArgs e)
        {
            ConfigItem selectedConfigItem = GetCurrentConfigItem();
            if (selectedConfigItem != null && selectedConfigItem.Args != textBox_Arg.Text)
            {
                selectedConfigItem.Args = textBox_Arg.Text; 
                _configManager.UpdateItem(selectedConfigItem);
            }
        }

        private void contextMenuStrip_listbox_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            EndListBoxEdit(false);
            bool hasItems = listBox1.Items.Count > 0;
            bool hasSelection = listBox1.SelectedItem != null;

            // "New" is always available
            newToolStripMenuItem.Enabled = true;

            // Items that require a selected item
            startToolStripMenuItem_listbox.Enabled = hasSelection;
            stopToolStripMenuItem_listbox.Enabled = hasSelection;
            activeToolStripMenuItem.Enabled = hasSelection;
            editToolStripMenuItem.Enabled = hasSelection;
            deleteToolStripMenuItem.Enabled = hasSelection;

            if (hasSelection)
            {
                ConfigItem item = GetCurrentConfigItem();
                if (item != null)
                {
                    bool isRunning = _processManager.IsProcessRunning(item);
                    startToolStripMenuItem_listbox.Enabled = !isRunning;
                    stopToolStripMenuItem_listbox.Enabled = isRunning;
                }
            }
        }

        private void startToolStripMenuItem_listbox_Click(object sender, EventArgs e)
        {
            ConfigItem item = GetCurrentConfigItem();
            if (item != null)
            {
                start(item);
            }
        }

        private void stopToolStripMenuItem_listbox_Click(object sender, EventArgs e)
        {
            ConfigItem item = GetCurrentConfigItem();
            if (item != null)
            {
                stop(item);
            }
        }
        #region ConfigurationManager Event Handlers

        private void OnConfigItemAdded(ConfigItem item)
        {
            _logViewManager.AddLogView(item);
            listBox1.Items.Add(item);
        }

        private void OnConfigItemRenamed(ConfigItem item, string newName)
        {
            listBox1.Invalidate();
        }

        private void OnConfigItemDeleted(ConfigItem item)
        {
            _logViewManager.RemoveLogView(item);
            listBox1.Items.Remove(item);

            if (listBox1.Items.Count > 0)
            {
                if (listBox1.SelectedIndex < 0)
                {
                    listBox1.SelectedIndex = 0;
                }
            }
            else
            {
                textBox1.Text = "";
                textBox_Arg.Text = "";
            }
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
        private void startAll(bool startAllItems = false)
        {
            foreach (ConfigItem item in _configManager.Items)
            {
                if (startAllItems)
                {
                    if (!item.Enable)
                    {
                        item.Enable = true;
                        _configManager.UpdateItem(item);
                    }
                }

                if (item.Enable && !_processManager.IsProcessRunning(item))
                {
                    start(item);
                }
            }
        }
        private void button_StartAll_Click(object sender, EventArgs e)
        {
            startAll(true);
        }

        private void stopAll()
        {
            foreach (ConfigItem item in _configManager.Items)
            {
                if (item.Enable)
                {
                    item.Enable = false;
                    _configManager.UpdateItem(item);
                }
            }
            _processManager?.StopAllProcesses();
        }

        private void button_stopAll_Click(object sender, EventArgs e)
        {
            stopAll();
        }

        private void checkBox1_autostart_Click(object sender, EventArgs e)
        {
            _configManager.AutoRun = checkBox1_autostart.Checked;
        }
    }
}
