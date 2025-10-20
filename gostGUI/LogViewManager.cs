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
        private readonly Dictionary<string, TextBox> _logTextBoxes = new Dictionary<string, TextBox>();
        private readonly Control _parentControl;
        private readonly TextBox _templateTextBox;

        public LogViewManager(Control parentControl, TextBox templateTextBox)
        {
            _parentControl = parentControl ?? throw new ArgumentNullException(nameof(parentControl));
            _templateTextBox = templateTextBox ?? throw new ArgumentNullException(nameof(templateTextBox));

            // The template from the designer should not be visible.
            _templateTextBox.Visible = false;
        }

        public void AddLogView(string itemName)
        {
            if (_logTextBoxes.ContainsKey(itemName)) return;

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

            _logTextBoxes[itemName] = textBoxLog;
            _parentControl.Controls.Add(textBoxLog);
        }

        public void RemoveLogView(string itemName)
        {
            if (_logTextBoxes.TryGetValue(itemName, out TextBox textBox))
            {
                _parentControl.Controls.Remove(textBox);
                _logTextBoxes.Remove(itemName);
                textBox.Dispose();
            }
        }

        public void RenameLogView(string oldName, string newName)
        {
            if (_logTextBoxes.TryGetValue(oldName, out TextBox textBox))
            {
                _logTextBoxes[newName] = textBox;
                _logTextBoxes.Remove(oldName);
            }
        }

        public void ShowLogView(string itemName)
        {
            foreach (var entry in _logTextBoxes)
            {
                entry.Value.Visible = (entry.Key == itemName);
            }
        }

        public void AppendText(string itemName, string text)
        {
            if (_logTextBoxes.TryGetValue(itemName, out TextBox textBox))
            {
                if (textBox.InvokeRequired)
                {
                    try
                    {
                        textBox.Invoke(new Action(() => AppendTextInternal(textBox, text)));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Invoke failed in LogViewManager.AppendText: {ex.Message}");
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
            textBox.AppendText(text);
            textBox.ScrollToCaret();
        }

        public void ClearLog(string itemName)
        {
            if (_logTextBoxes.TryGetValue(itemName, out TextBox textBox))
            {
                textBox.Clear();
            }
        }
    }
}