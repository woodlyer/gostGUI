using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace gostGUI
{
    public partial class FormMain : Form
    {

        Process p;
        //StreamWriter input;

        bool startOK = false;

        public FormMain()
        {
            InitializeComponent();
            initFromConfig();
            Application.ApplicationExit += new EventHandler(this.OnApplicationExit);
        }
        void initFromConfig()
        {
            //string exeDirPath = System.IO.Path.GetDirectoryName(Common.GetApplicationPath());
            string exeDirPath = (Common.GetApplicationPath());
            string path = exeDirPath + "/cmd.conf";
            Dictionary<string, string> cfgDic;
            Common.loadIni(path, out cfgDic);
            if (cfgDic.ContainsKey("program"))
                textBox1.Text = cfgDic["program"];
            else
                textBox1.Text = "gost.exe";

            if (cfgDic.ContainsKey("args"))
                textBox2.Text = cfgDic["args"];
            else
                textBox2.Text = "-L 127.0.0.1:1080 ";
        }

        void saveConfigToFile()
        {
            string cfgStr = "program=" + textBox1.Text + Environment.NewLine;
            cfgStr += "args=" + textBox2.Text + Environment.NewLine;
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
            string argsstr = textBox2.Text;

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

            saveConfigToFile();

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
            buttonStop.Enabled = false;

            p.StartInfo.FileName = textBox1.Text;
            p.StartInfo.Arguments = textBox2.Text;

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
                button1.Enabled = false;
                buttonStop.Enabled = true;
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
            catch (Exception)
            {
            }
            p = null;
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
                button1.Enabled = true;
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
                textBox3.AppendText(msg);
                if (textBox3.Text.Length > textBox3.MaxLength)
                    textBox3.Clear();

                textBox3.SelectionStart = textBox3.Text.Length - 1;
                textBox3.ScrollToCaret();
            }
        }

        // start
        private void button1_Click(object sender, EventArgs e)
        {
            start();
        }

        private void outputAdd(string str)
        {
            textBox3.AppendText(str);
            textBox3.AppendText(Environment.NewLine);
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
        private void button2_Click(object sender, EventArgs e)
        {
            if (startOK)
            {
                stop();
            }
            button1.Enabled = true;
            buttonStop.Enabled = false;
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
            textBox3.Clear();
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

 
    }
}
