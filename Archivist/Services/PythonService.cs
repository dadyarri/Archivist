using Archivist.Models;
using CommunityToolkit.Diagnostics;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Archivist.Services;

public class PythonService : IDisposable
{
    private Process? _pythonProcess;
    private CancellationTokenSource _cts = new CancellationTokenSource();

    private RequestSocket? _commandSocket;
    private PullSocket? _progressSocket;

    public PullSocket? ProgressSocket { get => _progressSocket; set => _progressSocket = value; }

    public event Action<ProgressMessage>? ProgressReceived;
    public event Action<ProgressMessage>? ResultReceived;
    public event Action<ProgressMessage>? ErrorReceived;
    public event Action<string>? ServerLogReceived;

    public PythonService()
    {
    }

    public void StartPythonService(string pythonExecutable, string scriptPath)
    {

        Guard.IsNotNullOrEmpty(pythonExecutable, nameof(pythonExecutable));
        Guard.IsNotNullOrEmpty(scriptPath, nameof(scriptPath));

        if (_cts.IsCancellationRequested)
        {
            _cts = new CancellationTokenSource();
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = scriptPath,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true
        };

        _pythonProcess = new Process { StartInfo = processStartInfo };

        _pythonProcess.Start();

        Thread.Sleep(2000);

        InitializeZmqClients();
    }

    private void InitializeZmqClients()
    {
        // Клиент REQ-REP для команд
        _commandSocket = new RequestSocket();
        _commandSocket.Connect("tcp://127.0.0.1:5555");
        ServerLogReceived?.Invoke("NetMQ Command Client (REQ) connected to tcp://127.0.0.1:5555");

        // Клиент PULL для прогресса
        _progressSocket = new PullSocket();
        _progressSocket.Bind("tcp://127.0.0.1:5556"); // WinUI PULL => Python PUSH
        ServerLogReceived?.Invoke("NetMQ Progress Server (PULL) bound to tcp://127.0.0.1:5556");

        // Запускаем слушателя прогресса в отдельном потоке
        Task.Run(() => ListenForProgressMessages(_cts.Token));
    }

    private void ListenForProgressMessages(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_progressSocket == null) continue;

                // Используем TryReceiveFrameString с таймаутом, чтобы поток мог быть отменен
                if (_progressSocket.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out string? messageString))
                {
                    var msg = JsonSerializer.Deserialize<ProgressMessage>(messageString);
                    if (msg == null) continue;

                    switch (msg.Type)
                    {
                        case "progress":
                            ProgressReceived?.Invoke(msg);
                            break;
                        case "finish":
                            ResultReceived?.Invoke(msg);
                            if (_commandSocket is not null)
                            {
                                _commandSocket.Close();
                                _commandSocket.Dispose();
                                _commandSocket = null;
                            }
                            if (_progressSocket is not null)
                            {
                                _progressSocket.Close();
                                _progressSocket.Dispose();
                                _progressSocket = null;
                            }
                            break;
                        case "error":
                            ErrorReceived?.Invoke(msg);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                ServerLogReceived?.Invoke($"Error in progress listener: {ex.Message}");
                // В случае критической ошибки можно попытаться переподключиться или сообщить UI
            }
        }
    }

    public async Task<bool> SendStartProcessingCommand(PythonInputData inputData)
    {
        if (_commandSocket == null)
        {
            ServerLogReceived?.Invoke("Command socket not initialized.");
            return false;
        }

        try
        {
            var jsonCommand = JsonSerializer.Serialize(inputData);
            _commandSocket.SendFrame(jsonCommand);

            var reply = await Task.Run(() => _commandSocket.ReceiveFrameString());
            var replyData = JsonSerializer.Deserialize<Dictionary<string, object>>(reply);

            // The Python script doesn't send a specific "processing_started" response
            // It just starts processing, so we consider it successful if we get any response
            return replyData != null;
        }
        catch (Exception ex)
        {
            ServerLogReceived?.Invoke($"Error sending start command: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PingPythonService()
    {
        if (_commandSocket == null) return false;
        try
        {
            _commandSocket.SendFrame(JsonSerializer.Serialize(new { command = "ping" }));
            var reply = await Task.Run(() => _commandSocket.ReceiveFrameString());
            var replyData = JsonSerializer.Deserialize<Dictionary<string, object>>(reply);
            return replyData?.TryGetValue("type", out var type) == true && type?.ToString() == "pong";
        }
        catch (Exception ex)
        {
            ServerLogReceived?.Invoke($"Ping failed: {ex.Message}");
            return false;
        }
    }

    public void StopPythonService()
    {
        _cts.Cancel(); // Отменяем слушателя прогресса

        if (_commandSocket != null)
        {
            try
            {
                // Отправляем команду на выключение серверу
                _commandSocket.SendFrame(JsonSerializer.Serialize(new { command = "stop" }));
                _commandSocket.ReceiveFrameString(); // Ждем подтверждения
            }
            catch (Exception ex)
            {
                ServerLogReceived?.Invoke($"Error sending shutdown command to Python: {ex.Message}");
            }
            finally
            {
                _commandSocket.Close();
                _commandSocket.Dispose();
                _commandSocket = null;
            }
        }
        if (_progressSocket != null)
        {
            _progressSocket.Close();
            _progressSocket.Dispose();
            _progressSocket = null;
        }


        if (_pythonProcess != null && !_pythonProcess.HasExited)
        {
            _pythonProcess.Kill();
            _pythonProcess.Dispose();
            _pythonProcess = null;
        }
        ServerLogReceived?.Invoke("Python service and ZeroMQ clients stopped.");
    }

    public async Task<bool> StartProcessing(List<FileData> files, string vault, string subdirectory, string format)
    {
        var inputData = new PythonInputData
        {
            Command = "start",
            Files = files,
            Vault = vault,
            Subdirectory = subdirectory,
            Format = format
        };

        return await SendStartProcessingCommand(inputData);
    }

    public void Dispose()
    {
        StopPythonService();
        _commandSocket?.Dispose();
        _progressSocket?.Dispose();
        _cts?.Dispose();
    }
}