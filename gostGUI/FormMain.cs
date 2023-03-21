using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace gostGUI
{
    public partial class FormMain : Form
    {
        Process p;
        bool startOK = false;

        Dictionary<string, string> cfgDic;
        
        // for listbox edit
        TextBox txtEdit = new TextBox();

        public FormMain()
        {
            InitializeComponent();
            initFromConfig();
            Application.ApplicationExit += new EventHandler(this.OnApplicationExit);

            txtEdit.KeyDown += new KeyEventHandler(txtEdit_KeyDown);
        }
        void initFromConfig()
        {
            //string exeDirPath = System.IO.Path.GetDirectoryName(Common.GetApplicationPath());
            string exeDirPath = (Common.GetApplicationPath());
            string path = exeDirPath + "/cmd.conf";

            Common.loadIni(path, out cfgDic);
            if (cfgDic.ContainsKey("program"))
                textBox1.Text = cfgDic["program"];
            else
                textBox1.Text = "gost.exe";

            listBox1.Items.Clear();
            foreach (var cfg in cfgDic)
            {
                if (cfg.Key == "program")
                    continue;
                listBox1.Items.Add(cfg.Key);
            }
            //if (cfgDic.ContainsKey("args"))
            //    textBox_Arg.Text = cfgDic["args"];
            //else
            //    textBox_Arg.Text = "-L 127.0.0.1:1080 ";
        }

        bool changeCfgKey(string oldKey, string newKey)
        {
            if (cfgDic.ContainsKey(newKey) || !cfgDic.ContainsKey(oldKey))
                return false;
            string argStr = cfgDic[oldKey];
            cfgDic.Remove(oldKey);
            cfgDic.Add(newKey, argStr);
            
            return true;
        }   
        void saveCfgToFile()
        {
            string cfgStr = "program=" + textBox1.Text + Environment.NewLine;

            foreach (var cfg in cfgDic)
            {
                if (cfg.Key == "program")
                    continue;

                cfgStr += cfg.Key +"=" + cfg.Value + Environment.NewLine;
            }
            string cfgFile = Common.GetApplicationPath() + "/cmd.conf";
            Common.SaveToFile(System.Text.Encoding.ASCII.GetBytes(cfgStr), cfgFile);
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            stop();
        }

        void start()
        {
            string exeFilePath = textBox1.Text;
            string argsstr = textBox_Arg.Text;

            if (!File.Exists(exeFilePath))
            {
                //  MessageBox.(fie.FileName);
                DialogResult diagorel = MessageBox.Show(this,
                    "Please inpult right exe path, Program file does not exist!",
                    "File does not exist!",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            saveCfgToFile();

            stop();
            p = null;
            p = new Process();
            // 自定义shell
            p.StartInfo.UseShellExecute = false;
            // 避免显示原始窗口
            p.StartInfo.CreateNoWindow = true;
            // 重定向标准输入（原来是CON）
            p.StartInfo.WorkingDirectory = Common.GetApplicationPath();

            p.StartInfo.RedirectStandardInput = false;// true;
            // 重定向标准输出
            p.StartInfo.RedirectStandardOutput = true;
            // 数据接收事件（标准输出重定向至此）
            p.OutputDataReceived += new DataReceivedEventHandler(p_OutputDataReceived);

            p.StartInfo.RedirectStandardError = true;
            p.ErrorDataReceived += new DataReceivedEventHandler(p_OutputDataReceived);

            //support process exit event
            p.EnableRaisingEvents = true;
            p.Exited += new EventHandler(p_Exit);

            // 界面按钮互锁
            button_stop.Enabled = false;

            p.StartInfo.FileName = textBox1.Text;
            p.StartInfo.Arguments = textBox_Arg.Text;

            startOK = p.Start();
            if (startOK)
            {
                outputAdd("run sucess.");
                // 重定向输入
                //input = p.StandardInput;
                // 开始监控输出（异步读取）
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                // 界面按钮互锁
                button_start.Enabled = false;
                button_stop.Enabled = true;
                listBox1.Enabled = false;
            }
            else
            {
                outputAdd("run failed.");
            }
            outputAdd("---------------------------------------------------------");
        }

        void stop()
        {
            if (p == null)
                return;
            try
            {
                p.Kill();
                p.Close();
                p.Dispose();
               
            }
            catch (Exception e)
            {
                update(e.Message);
            }
            p = null;
            outputAdd("stop program !!!");
        }

        private void p_Exit(object sender, System.EventArgs e)
        {
            //double runtime  = (p.ExitTime - p.StartTime).TotalMilliseconds;
            System.Threading.Thread.Sleep(10);
            update("!!! program exits !!!" + Environment.NewLine);
            updateButton();
        }
        delegate void buttonDelegate();
        void updateButton()
        {
            if (this.InvokeRequired)
            {
                try
                {
                    Invoke(new buttonDelegate(updateButton), new object[] { });
                }
                catch (Exception)
                {
                    // nothing to do
                }
            }
            else
            {
                button_start.Enabled = true;
                
            }
        }

        void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            update(e.Data + Environment.NewLine);
        }



        delegate void updateDelegate(string msg);
        void update(string msg)
        {
            if (this.InvokeRequired)
            {
                try
                {
                    Invoke(new updateDelegate(update), new object[] { msg });
                }
                catch (Exception)
                {
                    // nothing to do
                }
            }
            else
            {
                textBox_log.AppendText(msg);
                if (textBox_log.Text.Length > textBox_log.MaxLength)
                    textBox_log.Clear();

                textBox_log.SelectionStart = textBox_log.Text.Length - 1;
                textBox_log.ScrollToCaret();
            }
        }

        // start
        private void buttonStart_Click(object sender, EventArgs e)
        {
            start();
        }

        private void outputAdd(string str)
        {
            textBox_log.AppendText(str);
            textBox_log.AppendText(Environment.NewLine);
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
            if (startOK)
            {
                stop();
            }
            button_start.Enabled = true;
            button_stop.Enabled = false;
            listBox1.Enabled = true;
            
        }

        private void textBox1_DragDrop(object sender, DragEventArgs e)
        {
            string path = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();       //获得路径
            textBox1.Text = path;
        }

        private void textBox1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Link;
            else
                e.Effect = DragDropEffects.None;
        }

        //private void FormMain_DragDrop(object sender, DragEventArgs e)
        //{
        //    string path = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();       //获得路径
        //    textBox1.Text = path;
        //}

        private void button3_Click(object sender, EventArgs e)
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
                //用户点击 打开 后要执灯的代码
                textBox1.Text = fie.FileName;
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

                bool ret = changeCfgKey(oldText, newText);
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
            //MessageBox.Show("Edit");
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
            int itemSelected = listBox1.SelectedIndex;
            if (itemSelected < 0)
            {
                itemSelected = 0;
                this.listBox1.SelectedValue = 0;
            }
            string itemText = listBox1.Items[itemSelected].ToString();
            string argStr = "";
            bool ret = getArg(itemText, out argStr);
            if (!ret)
                return;
            
            textBox_Arg.Text = argStr;
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
            listBox1.SelectedIndex = 0;
        }
    }
}
