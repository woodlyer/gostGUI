using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace gostGUI
{
    /// <summary>
    /// Manages the lifecycle and interaction of the dynamic log TextBox controls.
    /// </summary>
    public class LogViewManager
    {
        private readonly Dictionary<ConfigItem, TextBox> _logTextBoxes = new Dictionary<ConfigItem, TextBox>();
        private readonly Control _parentControl;
        private readonly TextBox _templateTextBox;

        public LogViewManager(Control parentControl, TextBox templateTextBox)
        {
            _parentControl = parentControl ?? throw new ArgumentNullException(nameof(parentControl));
            _templateTextBox = templateTextBox ?? throw new ArgumentNullException(nameof(templateTextBox));

            // The template from the designer should not be visible.
            _templateTextBox.Visible = false;
        }

        public void AddLogView(ConfigItem item)
        {
            if (item == null || _logTextBoxes.ContainsKey(item)) return;

            var textBoxLog = new TextBox
            {
                Location = _templateTextBox.Location,
                Size = _templateTextBox.Size,
                BackColor = _templateTextBox.BackColor,
                ForeColor = _templateTextBox.ForeColor,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = _templateTextBox.ScrollBars,
                WordWrap = _templateTextBox.WordWrap,
                TextAlign = _templateTextBox.TextAlign,
                BorderStyle = _templateTextBox.BorderStyle,
                Visible = false // Initially hidden
            };

            _logTextBoxes[item] = textBoxLog;
            _parentControl.Controls.Add(textBoxLog);
        }

        public void RemoveLogView(ConfigItem item)
        {
            if (item != null && _logTextBoxes.TryGetValue(item, out TextBox textBox))
            {
                _parentControl.Controls.Remove(textBox);
                _logTextBoxes.Remove(item);
                textBox.Dispose();
            }
        }

        public void ShowLogView(ConfigItem item)
        {
            foreach (var entry in _logTextBoxes)
            {
                entry.Value.Visible = (entry.Key == item);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessage")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        private const int EM_GETLINECOUNT = 0x00BA;

        public void AppendText(ConfigItem item, string text)
        {
            if (item != null && _logTextBoxes.TryGetValue(item, out TextBox textBox))
            {
                if (textBox.IsDisposed) return;

                if (textBox.InvokeRequired)
                {
                    try
                    {
                        textBox.BeginInvoke(new Action(() =>
                        {
                            if (!textBox.IsDisposed && textBox.IsHandleCreated)
                            {
                                AppendTextInternal(textBox, text);
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"BeginInvoke failed in LogViewManager.AppendText: {ex.Message}");
                    }
                }
                else
                {
                    AppendTextInternal(textBox, text);
                }
            }
        }

        private void AppendTextInternal(TextBox textBox, string text)
        {
            if (textBox.IsDisposed || !textBox.IsHandleCreated) return;

            int currentLineCount = SendMessage(textBox.Handle, EM_GETLINECOUNT, 0, 0);
            if (currentLineCount > 500)
            {
                // 超过 500 行，清空全部内容
                textBox.Text = string.Empty;
            }

            textBox.AppendText(text);
            textBox.ScrollToCaret();
        }

        public void ClearLog(ConfigItem item)
        {
            if (item != null && _logTextBoxes.TryGetValue(item, out TextBox textBox))
            {
                textBox.Clear();
            }
        }
    }
}