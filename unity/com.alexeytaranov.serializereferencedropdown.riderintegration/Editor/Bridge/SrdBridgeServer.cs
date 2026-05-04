using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace SerializeReferenceDropdownBridge.Bridge
{
    [InitializeOnLoad]
    public static class SrdBridgeServer
    {
        private const string PipeName = "SerializeReferenceDropdownIntegration";
        private const string OpenAssetCommand = "OpenAsset";
        private const string ShowSearchTypeWindowCommand = "ShowSearchTypeWindow";
        private const int CommandExecutionTimeoutMs = 5000;

        private static readonly ConcurrentQueue<PendingCommand> MainThreadActions = new ConcurrentQueue<PendingCommand>();
        private static readonly object ListenerLock = new object();
        private static Thread listenerThread;
        private static volatile bool shouldStop;
        private static volatile bool isListening;
        private static bool isEnabled = true;

        public static event Action<Type> SearchTypeWindowRequested;
        public static event Action<string> SearchTypeWindowRequestedByName;

        static SrdBridgeServer()
        {
            EditorApplication.update += ExecuteMainThreadActions;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;

            if (isEnabled)
            {
                StartListener();
            }
        }
        
        private static void Stop()
        {
            StopListener();

            EditorApplication.update -= ExecuteMainThreadActions;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorApplication.quitting -= Stop;
        }

        public static void SetEnabled(bool enabled)
        {
            isEnabled = enabled;
            if (isEnabled)
            {
                StartListener();
                return;
            }

            StopListener();
            Log.DevLog("Rider integration bridge is disabled.");
        }

        private static void StartListener()
        {
            lock (ListenerLock)
            {
                if (isListening && listenerThread != null && listenerThread.IsAlive)
                {
                    return;
                }

                shouldStop = false;
                isListening = true;
                listenerThread = new Thread(Listen)
                {
                    IsBackground = true,
                    Name = "SRD bridge pipe listener"
                };
                listenerThread.Start();
            }

            Log.DevLog($"Bridge listener started on pipe '{PipeName}'.");
        }

        private static void StopListener()
        {
            lock (ListenerLock)
            {
                if (!isListening)
                {
                    return;
                }

                shouldStop = true;
            }

            WakeListener();
        }

        private static void WakeListener()
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    client.Connect(100);
                }
            }
            catch
            {
                // The listener can already be stopped or not yet waiting for a connection.
            }
        }

        private static void Listen()
        {
            while (!shouldStop)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                               PipeTransmissionMode.Byte, PipeOptions.None))
                    {
                        Log.DevLog($"Waiting for pipe connection '{PipeName}'.");
                        pipe.WaitForConnection();
                        Log.DevLog($"Pipe connected '{PipeName}'.");
                        if (shouldStop)
                        {
                            continue;
                        }

                        var commandJson = ReadCommand(pipe);
                        if (!string.IsNullOrWhiteSpace(commandJson))
                        {
                            Log.DevLog($"Received command JSON: {commandJson}");
                            var command = ParseCommand(commandJson);
                            if (command == null)
                            {
                                continue;
                            }

                            DispatchCommand(command);
                        }
                    }
                }
                catch (Exception exception)
                {
                    if (!shouldStop)
                    {
                        Log.DevWarning($"Pipe listener failed: {exception}");
                    }
                }
            }

            isListening = false;
        }

        private static void DispatchCommand(SrdBridgeCommand command)
        {
            var thread = new Thread(() =>
            {
                var response = ExecuteCommandWithResponse(command);
                WriteResponse(command.replyPipe, response);
            })
            {
                IsBackground = true,
                Name = "SRD bridge command executor"
            };
            thread.Start();
        }

        private static string ReadCommand(Stream stream)
        {
            using (var memory = new MemoryStream())
            {
                while (true)
                {
                    var read = stream.ReadByte();
                    if (read < 0 || read == '\n')
                    {
                        break;
                    }

                    memory.WriteByte((byte)read);
                }

                Log.DevLog($"Read {memory.Length} bytes from pipe.");
                return Encoding.UTF8.GetString(memory.ToArray());
            }
        }

        private static SrdBridgeCommand ParseCommand(string commandJson)
        {
            try
            {
                return JsonUtility.FromJson<SrdBridgeCommand>(commandJson);
            }
            catch (Exception exception)
            {
                Log.Error($"Received invalid JSON command: {commandJson}\n{exception}");
                return null;
            }
        }

        private static SrdBridgeResponse ExecuteCommandWithResponse(SrdBridgeCommand command)
        {
            var pendingCommand = new PendingCommand(command);
            MainThreadActions.Enqueue(pendingCommand);
            if (!pendingCommand.Wait(CommandExecutionTimeoutMs))
            {
                return SrdBridgeResponse.Create("Timeout", "Unity bridge timed out while executing the command.");
            }

            return pendingCommand.Response;
        }

        private static void WriteResponse(string replyPipeName, SrdBridgeResponse response)
        {
            if (string.IsNullOrWhiteSpace(replyPipeName))
            {
                Log.DevWarning("Cannot send bridge response because reply pipe is empty.");
                return;
            }

            try
            {
                using (var client = new NamedPipeClientStream(".", replyPipeName, PipeDirection.Out))
                {
                    client.Connect(1000);
                    var responseJson = JsonUtility.ToJson(response) + "\n";
                    var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                    client.Write(responseBytes, 0, responseBytes.Length);
                    client.Flush();
                    Log.DevLog($"Sent response JSON to '{replyPipeName}': {responseJson.TrimEnd()}");
                }
            }
            catch (Exception exception)
            {
                Log.Error($"Failed to send bridge response to '{replyPipeName}': {exception}");
            }
        }

        private static void ExecuteMainThreadActions()
        {
            while (MainThreadActions.TryDequeue(out var pendingCommand))
            {
                try
                {
                    pendingCommand.SetResponse(ExecuteCommand(pendingCommand.Command));
                }
                catch (Exception exception)
                {
                    Log.Error($"Command execution failed: {exception}");
                    pendingCommand.SetResponse(SrdBridgeResponse.Create("Error", exception.Message));
                }
            }
        }

        private static SrdBridgeResponse ExecuteCommand(SrdBridgeCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.command))
            {
                Log.Error("Received an empty command.");
                return SrdBridgeResponse.Create("EmptyCommand", "Unity bridge received an empty command.");
            }

            switch (command.command)
            {
                case OpenAssetCommand:
                    Log.DevLog($"Executing {OpenAssetCommand}: {command.payload}");
                    return OpenAsset(command.payload);
                case ShowSearchTypeWindowCommand:
                    Log.DevLog($"Executing {ShowSearchTypeWindowCommand}: {command.payload}");
                    return ShowSearchTypeWindow(command.payload);
                default:
                    Log.Error($"Received unknown command: {command.command}");
                    return SrdBridgeResponse.Create("UnknownCommand", $"Unity bridge received unknown command: {command.command}");
            }
        }

        private static SrdBridgeResponse ShowSearchTypeWindow(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                Log.Error("Cannot open search window because type name is empty.");
                return SrdBridgeResponse.Create("EmptyCommand", "Cannot open search window because type name is empty.");
            }

            var type = ResolveType(typeName);
            if (type != null)
            {
                SearchTypeWindowRequested?.Invoke(type);
                return SrdBridgeResponse.Create("Ok", $"Search type window requested for {type.FullName}.");
            }

            Log.DevWarning($"Could not resolve type '{typeName}'. Falling back to raw type name event.");
            SearchTypeWindowRequestedByName?.Invoke(typeName);
            return SrdBridgeResponse.Create("TypeNotResolved",
                $"Unity bridge could not resolve type '{typeName}', but raw type-name event was raised.");
        }

        private static Type ResolveType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(typeName);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                    // Some dynamic/editor assemblies can throw while resolving types; skip them.
                }
            }

            return null;
        }

        private static SrdBridgeResponse OpenAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                Log.Error("Cannot open asset because path is empty.");
                return SrdBridgeResponse.Create("EmptyCommand", "Cannot open asset because path is empty.");
            }

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
            {
                Log.Error($"Could not find asset: {assetPath}");
                return SrdBridgeResponse.Create("AssetNotFound", $"Unity asset was not found: {assetPath}");
            }

            Selection.activeObject = asset;
            EditorUtility.FocusProjectWindow();
            EditorGUIUtility.PingObject(asset);
            return SrdBridgeResponse.Create("Ok", $"Unity asset was selected: {assetPath}");
        }

        private sealed class PendingCommand
        {
            private readonly ManualResetEventSlim completed = new ManualResetEventSlim(false);

            public PendingCommand(SrdBridgeCommand command)
            {
                Command = command;
            }

            public SrdBridgeCommand Command { get; }
            public SrdBridgeResponse Response { get; private set; }

            public bool Wait(int timeoutMs)
            {
                return completed.Wait(timeoutMs);
            }

            public void SetResponse(SrdBridgeResponse response)
            {
                Response = response;
                completed.Set();
            }
        }
    }

    internal static class Log
    {
        private const string LogPrefix = "[SRD]";

        public static void Error(object error)
        {
            UnityEngine.Debug.LogError($"{LogPrefix} {error}");
        }

        [Conditional("SRD_DEV")]
        public static void DevError(object error)
        {
            UnityEngine.Debug.LogError($"{LogPrefix} {error}");
        }

        [Conditional("SRD_DEV")]
        public static void DevWarning(object warning)
        {
            UnityEngine.Debug.LogWarning($"{LogPrefix} {warning}");
        }

        [Conditional("SRD_DEV")]
        public static void DevLog(object log)
        {
            UnityEngine.Debug.Log($"{LogPrefix} {log}");
        }
    }
}
