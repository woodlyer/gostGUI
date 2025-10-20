using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace gostGUI
{
    public class ProcessManager : IDisposable
    {
        private readonly Dictionary<string, Process> _processes = new Dictionary<string, Process>();
        private readonly JobObjectManager _jobManager;

        public event Action<string, string> OutputReceived;
        public event Action<string, int> ProcessExited;
        public event Action<string> ProcessStarted;
        public event Action<string> ProcessStopped;

        public ProcessManager()
        {
            try
            {
                _jobManager = new JobObjectManager();
            }
            catch (Exception ex)
            {
                // Propagate the exception to be handled by the UI layer.
                throw new InvalidOperationException("Failed to create Job Object for process management.", ex);
            }
        }

        public bool IsProcessRunning(string itemName)
        {
            return _processes.ContainsKey(itemName) && !_processes[itemName].HasExited;
        }

        public bool StartProcess(ConfigItem configItem)
        {
            if (configItem == null)
            {
                return false;
            }

            string itemName = configItem.Name;

            if (IsProcessRunning(itemName))
            {
                // Already running, no action needed.
                return true;
            }

            if (!File.Exists(configItem.Program))
            {
                OutputReceived?.Invoke(itemName, $"!!! Program file does not exist: {configItem.Program} !!!");
                return false;
            }

            Process p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = configItem.Program,
                    Arguments = configItem.Args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Common.GetApplicationPath(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                },
                EnableRaisingEvents = true
            };

            p.OutputDataReceived += (sender, e) => { if (e.Data != null) OutputReceived?.Invoke(itemName, e.Data + Environment.NewLine); };
            p.ErrorDataReceived += (sender, e) => { if (e.Data != null) OutputReceived?.Invoke(itemName, e.Data + Environment.NewLine); };
            p.Exited += (sender, e) =>
            {
                Process exitedProcess = sender as Process;
                _processes.Remove(itemName);
                ProcessExited?.Invoke(itemName, exitedProcess.ExitCode);
            };

            try
            {
                if (p.Start())
                {
                    _jobManager.AddProcess(p);
                    _processes[itemName] = p;
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    ProcessStarted?.Invoke(itemName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                OutputReceived?.Invoke(itemName, $"!!! Failed to start process: {ex.Message} !!!");
            }

            return false;
        }

        public void StopProcess(string itemName)
        {
            if (_processes.TryGetValue(itemName, out Process p) && p != null && !p.HasExited)
            {
                try
                {
                    p.Kill();
                }
                catch (Exception ex)
                {
                    OutputReceived?.Invoke(itemName, $"!!! Failed to stop process: {ex.Message} !!!");
                }
                finally
                {
                    p.Close();
                    p.Dispose();
                    _processes.Remove(itemName);
                    ProcessStopped?.Invoke(itemName);
                }
            }
        }

        public void StopAllProcesses()
        {
            // Create a copy of keys to avoid modification during iteration
            List<string> runningItems = new List<string>(_processes.Keys);
            foreach (string itemName in runningItems)
            {
                StopProcess(itemName);
            }
        }

        public void Dispose()
        {
            StopAllProcesses();
            _jobManager?.Dispose();
        }
    }
}