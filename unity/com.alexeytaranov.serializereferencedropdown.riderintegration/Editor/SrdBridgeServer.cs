using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace SerializeReferenceDropdownBridge.Editor
{
    [InitializeOnLoad]
    public static class SrdBridgeServer
    {
        private const string PipeName = "SerializeReferenceDropdownIntegration";
        private const string OpenAssetCommand = "OpenAsset";
        private const string ShowSearchTypeWindowCommand = "ShowSearchTypeWindow";

        private static readonly ConcurrentQueue<Action> MainThreadActions = new ConcurrentQueue<Action>();
        private static Thread listenerThread;
        private static bool shouldStop;
        private static bool isListening;
        private static bool isEnabled = true;

        public static event Action<Type> SearchTypeWindowRequested;
        public static event Action<string> SearchTypeWindowRequestedByName;

        static SrdBridgeServer()
        {
            EditorApplication.update += ExecuteMainThreadActions;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
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
            if (isListening)
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
            Log.DevLog($"Bridge listener started on pipe '{PipeName}'.");
        }

        private static void StopListener()
        {
            if (!isListening)
            {
                return;
            }

            shouldStop = true;
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
                            EnqueueCommand(commandJson);
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

        private static string ReadCommand(Stream stream)
        {
            var buffer = new byte[4096];
            using (var memory = new MemoryStream())
            {
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    memory.Write(buffer, 0, read);
                }

                Log.DevLog($"Read {memory.Length} bytes from pipe.");
                return Encoding.UTF8.GetString(memory.ToArray());
            }
        }

        private static void EnqueueCommand(string commandJson)
        {
            SrdBridgeCommand command;
            try
            {
                command = JsonUtility.FromJson<SrdBridgeCommand>(commandJson);
            }
            catch (Exception exception)
            {
                Log.Error($"Received invalid JSON command: {commandJson}\n{exception}");
                return;
            }

            MainThreadActions.Enqueue(() => ExecuteCommand(command));
        }

        private static void ExecuteMainThreadActions()
        {
            while (MainThreadActions.TryDequeue(out var action))
            {
                action();
            }
        }

        private static void ExecuteCommand(SrdBridgeCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.command))
            {
                Log.Error("Received an empty command.");
                return;
            }

            switch (command.command)
            {
                case OpenAssetCommand:
                    Log.DevLog($"Executing {OpenAssetCommand}: {command.payload}");
                    OpenAsset(command.payload);
                    break;
                case ShowSearchTypeWindowCommand:
                    Log.DevLog($"Executing {ShowSearchTypeWindowCommand}: {command.payload}");
                    ShowSearchTypeWindow(command.payload);
                    break;
                default:
                    Log.Error($"Received unknown command: {command.command}");
                    break;
            }
        }

        private static void ShowSearchTypeWindow(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                Log.Error("Cannot open search window because type name is empty.");
                return;
            }

            var type = ResolveType(typeName);
            if (type != null)
            {
                SearchTypeWindowRequested?.Invoke(type);
                return;
            }

            Log.DevWarning($"Could not resolve type '{typeName}'. Falling back to raw type name event.");
            SearchTypeWindowRequestedByName?.Invoke(typeName);
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

        private static void OpenAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                Log.Error("Cannot open asset because path is empty.");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
            {
                Log.Error($"Could not find asset: {assetPath}");
                return;
            }

            Selection.activeObject = asset;
            EditorUtility.FocusProjectWindow();
            EditorGUIUtility.PingObject(asset);
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
