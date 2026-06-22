/*
 * Copyright 2026 Julien Bombled
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections.ObjectModel;
using System.Diagnostics;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Services.PowerShell;

/// <summary>
/// Wrapper that mimics PowerShell SDK API but uses external process execution.
/// This is needed because the PowerShell SDK doesn't work in single-file deployments.
/// </summary>
internal class PowerShellProcessWrapper : IDisposable
{
    private readonly string _psPath;
    private readonly string _workingDir;
    private readonly List<string> _scripts = new();
    private readonly List<string> _errors = new();
    private readonly ILoggingService? _logger;
    private bool _hadErrors;

    /// <summary>
    /// Default timeout for script execution in milliseconds (5 minutes).
    /// </summary>
    private const int ProcessTimeoutMs = 300000;

    public PowerShellProcessWrapper(string psPath, string workingDir, ILoggingService? logger = null)
    {
        _psPath = psPath;
        _workingDir = workingDir;
        _logger = logger;
    }

    public bool HadErrors => _hadErrors;
    public PowerShellStreams Streams => new(_errors);

    public PowerShellProcessWrapper AddCommand(string command)
    {
        _scripts.Add(command);
        return this;
    }

    public PowerShellProcessWrapper AddParameter(string name, object? value = null)
    {
        if (_scripts.Count > 0)
        {
            string lastScript = _scripts[^1];
            if (value != null)
            {
                string valueStr = PowerShellValidation.EscapeForPowerShell(value.ToString() ?? "");
                _scripts[^1] = $"{lastScript} -{name} '{valueStr}'";
            }
            else
            {
                // Switch parameter (no value)
                _scripts[^1] = $"{lastScript} -{name}";
            }
        }
        return this;
    }

    public PowerShellProcessWrapper AddScript(string script)
    {
        _scripts.Add(script);
        return this;
    }

    public Collection<PSObject> Invoke()
    {
        Collection<PSObject> result = new Collection<PSObject>();

        if (_scripts.Count == 0)
            return result;

        string fullScript = string.Join("; ", _scripts);
        string encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(fullScript));

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = _psPath,
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -EncodedCommand {encodedScript}",
            WorkingDirectory = _workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using Process process = new Process { StartInfo = startInfo };
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            // Wait with timeout (5 minutes for script execution)
            if (!process.WaitForExit(ProcessTimeoutMs))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Process kill failed (best effort): {ex.Message}");
                }
                throw new TimeoutException($"Script execution timed out after {ProcessTimeoutMs / 1000} seconds");
            }

            if (!string.IsNullOrEmpty(error))
            {
                _errors.Add(error);
                _hadErrors = true;
            }

            if (!string.IsNullOrEmpty(output))
            {
                foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    result.Add(new PSObject(line.Trim()));
                }
            }
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
            _hadErrors = true;
        }

        return result;
    }

    public void Clear()
    {
        _scripts.Clear();
    }

    public PowerShellCommands Commands => new(this);

    public void Dispose()
    {
        _scripts.Clear();
        _errors.Clear();
    }
}

/// <summary>
/// Wrapper for PowerShell streams.
/// </summary>
internal class PowerShellStreams
{
    private readonly List<string> _errors;

    public PowerShellStreams(List<string> errors)
    {
        _errors = errors;
    }

    public IEnumerable<PowerShellErrorRecord> Error => _errors.Select(e => new PowerShellErrorRecord(e));
    public PowerShellDataCollection Information => new();
    public PowerShellDataCollection Warning => new();
    public PowerShellDataCollection Verbose => new();
}

/// <summary>
/// Wrapper for error records.
/// </summary>
internal class PowerShellErrorRecord
{
    private readonly string _message;

    public PowerShellErrorRecord(string message)
    {
        _message = message;
    }

    public override string ToString() => _message;
}

/// <summary>
/// Wrapper for data collection with DataAdded event.
/// </summary>
internal class PowerShellDataCollection
{
    private readonly List<string> _items = new();

    public event EventHandler<PowerShellDataAddedEventArgs>? DataAdded;

    public string? this[int index] => index >= 0 && index < _items.Count ? _items[index] : null;

    public void Add(string item)
    {
        _items.Add(item);
        DataAdded?.Invoke(this, new PowerShellDataAddedEventArgs { Index = _items.Count - 1 });
    }
}

/// <summary>
/// Event args for data added.
/// </summary>
internal class PowerShellDataAddedEventArgs : EventArgs
{
    public int Index { get; set; }
}

/// <summary>
/// Wrapper for commands collection.
/// </summary>
internal class PowerShellCommands
{
    private readonly PowerShellProcessWrapper _wrapper;

    public PowerShellCommands(PowerShellProcessWrapper wrapper)
    {
        _wrapper = wrapper;
    }

    public void Clear() => _wrapper.Clear();
}

/// <summary>
/// Simple PSObject replacement for process-based execution.
/// </summary>
internal class PSObject
{
    public object? BaseObject { get; }

    public PSObject(object? baseObject = null)
    {
        BaseObject = baseObject;
    }

    public PSObjectProperties Properties => new();

    public override string? ToString() => BaseObject?.ToString();
}

/// <summary>
/// Properties collection for PSObject.
/// </summary>
internal class PSObjectProperties
{
    public PSObjectProperty? this[string name] => null;
}

/// <summary>
/// Property for PSObject.
/// </summary>
internal class PSObjectProperty
{
    public object? Value { get; set; }
}
