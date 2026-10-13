using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace gostGUI
{
    public class ProcessManager : IDisposable
    {
        private readonly Dictionary<ConfigItem, Process> _processes = new Dictionary<ConfigItem, Process>();
        private readonly object _lock = new object();
        private readonly JobObjectManager _jobManager;

        public event Action<ConfigItem, string> OutputReceived;
        public event Action<ConfigItem, int> ProcessExited;
        public event Action<ConfigItem> ProcessStarted;
        public event Action<ConfigItem> ProcessStopped;

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

        public bool IsProcessRunning(ConfigItem item)
        {
            if (item == null) return false;
            lock (_lock)
            {
                if (_processes.TryGetValue(item, out Process p) && p != null)
                {
                    try
                    {
                        return !p.HasExited;
                    }
                    catch
                    {
                        return false;
                    }
                }
                return false;
            }
        }

        public bool StartProcess(ConfigItem configItem)
        {
            if (configItem == null)
            {
                return false;
            }

            lock (_lock)
            {
                if (IsProcessRunning(configItem))
                {
                    // Already running, no action needed.
                    return true;
                }
            }

            if (!File.Exists(configItem.Program))
            {
                OutputReceived?.Invoke(configItem, $"!!! Program file does not exist: {configItem.Program} !!!");
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

            p.OutputDataReceived += (sender, e) => { if (e.Data != null) OutputReceived?.Invoke(configItem, e.Data + Environment.NewLine); };
            p.ErrorDataReceived += (sender, e) => { if (e.Data != null) OutputReceived?.Invoke(configItem, e.Data + Environment.NewLine); };
            p.Exited += (sender, e) =>
            {
                int exitCode = -1;
                try
                {
                    if (sender is Process proc)
                    {
                        exitCode = proc.ExitCode;
                    }
                }
                catch { }

                lock (_lock)
                {
                    _processes.Remove(configItem);
                }
                ProcessExited?.Invoke(configItem, exitCode);
            };

            try
            {
                if (p.Start())
                {
                    _jobManager.AddProcess(p);
                    lock (_lock)
                    {
                        _processes[configItem] = p;
                    }
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    ProcessStarted?.Invoke(configItem);
                    return true;
                }
            }
            catch (Exception ex)
            {
                OutputReceived?.Invoke(configItem, $"!!! Failed to start process: {ex.Message} !!!");
            }

            return false;
        }

        public void StopProcess(ConfigItem configItem)
        {
            if (configItem == null) return;
            Process p = null;
            lock (_lock)
            {
                if (_processes.TryGetValue(configItem, out p))
                {
                    _processes.Remove(configItem);
                }
            }

            if (p != null)
            {
                try
                {
                    if (!p.HasExited)
                    {
                        p.Kill();
                    }
                }
                catch (Exception ex)
                {
                    OutputReceived?.Invoke(configItem, $"!!! Failed to stop process: {ex.Message} !!!");
                }
                finally
                {
                    try
                    {
                        p.Dispose();
                    }
                    catch { }
                    ProcessStopped?.Invoke(configItem);
                }
            }
        }

        public void StopAllProcesses()
        {
            List<ConfigItem> runningItems;
            lock (_lock)
            {
                // Create a copy of keys to avoid modification during iteration
                runningItems = new List<ConfigItem>(_processes.Keys);
            }

            foreach (ConfigItem item in runningItems)
            {
                StopProcess(item);
            }
        }

        public void Dispose()
        {
            StopAllProcesses();
            _jobManager?.Dispose();
        }
    }
}