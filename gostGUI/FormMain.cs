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
        Process p;
        bool startOK = false;


        Dictionary<string, string> cfgDic;
        ConfigData configData;
        private Dictionary<string, Process> processes = new Dictionary<string, Process>();
        private Dictionary<string, TextBox> textBoxLogs = new Dictionary<string, TextBox>();

        // for listbox edit
        TextBox txtEdit = new TextBox();

        public FormMain()
        {
            InitializeComponent();
            initFromConfig();
            checkAutoStartStatus();

            Application.ApplicationExit += new EventHandler(this.OnApplicationExit);

            txtEdit.KeyDown += new KeyEventHandler(txtEdit_KeyDown);
			// init前面已经把选项放到listBox1里面了
			//int index=0;
   //         foreach (string s in listBox1.Items)
			//{
			//	if( s == getCfgVal("lastItem"))
			//	{
			//		listBox1.SelectedIndex = index;
			//		start();
			//		break;
			//	}
			//	index++;
			//}

			

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
                    //textBoxLog.Dock = DockStyle.Fill;
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

           

           

            //if (cfgDic.ContainsKey("lastStatus"))
            //    lastStatus = cfgDic["lastStatus"];


            //listBox1.Items.Clear();
            //foreach (var cfg in cfgDic)
            //{
            //    if (cfg.Key == "program")
            //        continue;
            //    if (cfg.Key == "lastItem")
            //        continue;

            //    listBox1.Items.Add(cfg.Key);
            //}
            //if (cfgDic.ContainsKey("args"))
            //    textBox_Arg.Text = cfgDic["args"];
            //else
            //    textBox_Arg.Text = "-L 127.0.0.1:1080 ";
        }

        bool changeCfgKey2NewKey(string oldKey, string newKey)
        {
            if (cfgDic.ContainsKey(newKey) || !cfgDic.ContainsKey(oldKey))
                return false;
            string argStr = cfgDic[oldKey];
            cfgDic.Remove(oldKey);
            cfgDic.Add(newKey, argStr);
            
            return true;
        }
        string getCfgVal(string key)
        {
            if (cfgDic.ContainsKey(key))
                return cfgDic[key];
            else
                return "";
        }
        void saveCfgToFile()
        {
            string cfgFile = Common.GetApplicationPath() + "/config.json";
            Common.SaveToFile(configData, cfgFile);
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            stop();
        }

        private string getCurrentItem()
        {
            string selectedItemName = listBox1.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedItemName))
            {
                MessageBox.Show("Please select an item from the list.");
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

        void start()
        {
            // Get the selected item name
            string selectedItemName = getCurrentItem();

            // Get the program and arguments from the selected item
            string exeFilePath = textBox1.Text;
            string argsstr = textBox_Arg.Text;

            if (!File.Exists(exeFilePath))
            {
                outputAdd("!!! Program file does not exist !!!", selectedItemName);
                return;
            }

            // Stop the existing process if it is running
            stop(selectedItemName);

            // Create a new process
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.WorkingDirectory = Common.GetApplicationPath();
            p.StartInfo.RedirectStandardInput = false;
            p.StartInfo.RedirectStandardOutput = true;
            // Pass the selectedItemName to the event handler
            p.OutputDataReceived += new DataReceivedEventHandler((sender, e) => p_OutputDataReceived(sender, e, selectedItemName));
            p.StartInfo.RedirectStandardError = true;
            // Pass the selectedItemName to the event handler
            p.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => p_OutputDataReceived(sender, e, selectedItemName));
            p.EnableRaisingEvents = true;
            p.Exited += new EventHandler((sender, e) => p_Exit(sender, e, selectedItemName));

            // Set the program and arguments
            p.StartInfo.FileName = exeFilePath;
            p.StartInfo.Arguments = argsstr;
            try
            {
                startOK = p.Start();

            }
            catch (Exception e)
            {
                update(e.Message, selectedItemName);
            }

            if (startOK)
            {
                outputAdd("run sucess.", selectedItemName);
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                //button_start.Enabled = false;
                button_stop.Enabled = true;

                // Store the process in the dictionary
                processes[selectedItemName] = p;
            }
            else
            {
                outputAdd("run failed.", selectedItemName);
            }
            outputAdd("---------------------------------------------------------", selectedItemName);
        }

        void stop()
        {
            string selectedItemName = listBox1.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedItemName))
            {
                MessageBox.Show("Please select an item from the list.");
                return;
            }

            // Find the selected item in the config data
            ConfigItem selectedConfigItem = configData.Items.Find(item => item.Name == selectedItemName);
            if (selectedConfigItem == null)
            {
                MessageBox.Show("Selected item not found in the configuration.");
                return;
            }
            stop(selectedItemName);
            outputAdd("!!! stop program !!!", selectedItemName);
        }

        void stop(string itemName)
        {
            if (!processes.ContainsKey(itemName))
            {
                return;
            }

            Process p = processes[itemName];
            if (p == null)
                return;

            if (p.HasExited)
            {
                outputAdd("program is not running.");
                return;
            }

            try
            {
                p.Kill();
                p.Close();
                p.Dispose();
            }
            catch (Exception e)
            {
                update(e.Message, itemName);
            }

            processes.Remove(itemName);
            outputAdd("!!! stop program !!!");
        }

        private void p_Exit(object sender, System.EventArgs e, string itemName)
        {
            System.Threading.Thread.Sleep(10);// ms
            update("!!! program exits !!!" + Environment.NewLine, itemName);
            updateStatus(itemName);
        }
        delegate void buttonDelegate(string itemName);
        void updateStatus(string itemName)
        {
            if (this.InvokeRequired)
            {
                try
                {
                    Invoke(new buttonDelegate(updateStatus), new object[] { itemName });
                }
                catch (Exception)
                {
                    // nothing to do
                }
            }
            else
            {
                //configData[itemName]
                button_start.Enabled = true;
                
            }
        }

        void p_OutputDataReceived(object sender, DataReceivedEventArgs e, string itemName)
        {
            update(e.Data + Environment.NewLine, itemName);
        }



        delegate void updateDelegate(string msg, string itemName);
        void update(string msg, string itemName)
        {
            if (this.InvokeRequired)
            {
                try
                {
                    Invoke(new updateDelegate(update), new object[] { msg, itemName });
                }
                catch (Exception)
                {
                    // nothing to do
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
			// set lastItem
			//String currentItem = listBox1.Items[listBox1.SelectedIndex].ToString();
			//if(currentItem != "")
             //             cfgDic["lastItem"] = currentItem;
			
			
            start();
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
            catch (Exception)
            {
                // do nothing
            }
        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            stop();
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
            string selectedItemName = listBox1.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedItemName))
            {
                MessageBox.Show("Please select an item from the list.");
                return;
            }

            stop(selectedItemName);
            //button_start.Enabled = true;
            //button_stop.Enabled = false;

        }

        private void textBox1_DragDrop(object sender, DragEventArgs e)
        {
            string path = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();       //获得路径
            textBox1.Text = path;
            // Get the selected item name
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
                    // Update the Program property of the selected item
                    selectedConfigItem.Program = path;
                    saveCfgToFile();
                }
            }
            else
            {
                MessageBox.Show("Please select an item from the list before dragging and dropping.");
            }
        }
        //private void FormMain_DragDrop(object sender, DragEventArgs e)
        //{
        //    string path = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();       //获得路径
        //    textBox1.Text = path;
        //}

        private void button_select_Click(object sender, EventArgs e)
        {
            OpenFileDialog fie = new OpenFileDialog();
            //创建对象
            fie.Title = "select .exe program";
            //设置文本框标题

            string cdstr = System.Environment.CurrentDirectory;
            fie.InitialDirectory = cdstr;
            //对话框的初始目录
            fie.Filter = "exe|*.exe|all|*.*";
            //设置文件类型
            string str = fie.FileName;
            //获取选择的路径
            if (fie.ShowDialog() == DialogResult.OK)
            {
                //MessageBox.Show(fie.FileName);
                textBox1.Text = fie.FileName;
                update_textBox1(textBox1.Text);
            }
        }

        private void clearButton_Click(object sender, EventArgs e)
        {
            textBox_log.Clear();
        }

        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            start();
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            stop();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // 结束启动的进程
            if (startOK)
            {
                stop();
            }
            notifyIcon1.Dispose();
            // 整个程序退出
            Application.Exit();
        }

        private void txtEdit_KeyDown(object sender, KeyEventArgs e)
        {
            //Enter键 更新项并隐藏编辑框   
            if (e.KeyCode == Keys.Enter)
            {
                string oldText = listBox1.Items[listBox1.SelectedIndex].ToString();
                string newText = txtEdit.Text;

                bool ret = changeCfgKey2NewKey(oldText, newText);
                if (ret)
                {
                    listBox1.Items[listBox1.SelectedIndex] = txtEdit.Text;
                    txtEdit.Visible = false;
                }
                else
                    MessageBox.Show("key is the same. Please check it!");
                
            }
            //Esc键 直接隐藏编辑框   
            if (e.KeyCode == Keys.Escape)
                txtEdit.Visible = false;

        }
        private void listBox1_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            e.DrawFocusRectangle();
            e.Graphics.DrawString(listBox1.Items[e.Index].ToString(), e.Font, new SolidBrush(e.ForeColor), e.Bounds);
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            int itemSelected = listBox1.SelectedIndex;
            string itemText = listBox1.Items[itemSelected].ToString();

            Rectangle rect = listBox1.GetItemRectangle(itemSelected);
            txtEdit.Parent = listBox1;
            rect.Height += 5;
            txtEdit.Bounds = rect;

            txtEdit.Multiline = true;
            txtEdit.Visible = true;
            txtEdit.Text = itemText;
            txtEdit.Focus();
            txtEdit.SelectAll();
        }

        private void listBox1_MouseClick(object sender, MouseEventArgs e)
        {
            txtEdit.Visible = false;
        }


        private void selectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //MessageBox.Show("Active");
        }

        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listBox1_DoubleClick(sender, e);
        }
        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //MessageBox.Show("New");
            cfgDic["newArg"] = "";
            saveCfgToFile();
            listBox1.Items.Add("newArg");//result in selected change
        }


        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int itemSelected = listBox1.SelectedIndex;
            string itemText = listBox1.Items[itemSelected].ToString();
            cfgDic.Remove(itemText);
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

                    // Show the selected textBox_log and hide others
                    foreach (var itemName in textBoxLogs.Keys)
                    {
                        if (itemName == selectedItemName)
                        {
                            textBoxLogs[itemName].Visible = true;
                        }
                        else
                        {
                            textBoxLogs[itemName].Visible = false;
                        }
                    }
                }
            }
        }

        bool getArg(string key, out string value)
        {
            value = "";
            if (!cfgDic.ContainsKey(key) )
                return false;
            else
                value = cfgDic[key];
            return true;
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            //listBox1.SelectedIndex = 0;
        }


        private void textBox_Arg_KeyDown(object sender, KeyEventArgs e)
        {
            string newText = textBox_Arg.Text;
            int itemSelected = listBox1.SelectedIndex;
            if (itemSelected < 0)
            {
                return;
            }
            string itemText = listBox1.Items[itemSelected].ToString();
            cfgDic[itemText] = newText;
            saveCfgToFile();
        }

        private void button_Exit_Click(object sender, EventArgs e)
        {
            exitToolStripMenuItem_Click(sender, e);
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
