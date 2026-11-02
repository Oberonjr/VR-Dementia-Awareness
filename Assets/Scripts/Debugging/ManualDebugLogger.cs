using UnityEngine;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

//A script meant to write manual logs into the VR's internal folders to check for a standalone build's status
//This was mostly made due to issues with accessing the device's actual logs
//so I made this to get the information that I need
//This was initially made to check issues with the standalone device connecting to the AI providers
public static class ManualDebugLogger
{
    /// <summary>
    /// Switch to toggle if manual logs should be written to the device's memory for checking.
    /// </summary>
    private static readonly bool Enabled = true;

    private const string LogFileName = "newest_game_log.txt";
    private const string PreviousLogFileName = "previous_game_log.txt";

    private static StreamWriter writer;
    private static readonly object writeLock = new object();
    
    // Run automatically at the very start of the application, before any script fires or scene loads
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (!Enabled) return;
        
        // Unsubscribe to avoid duplicate handler adding
        Application.logMessageReceivedThreaded -= HandleLog;
        AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;
        TaskScheduler.UnobservedTaskException -= HandleUnobservedTaskException;
        Application.lowMemory -= HandleLowMemory;
        Application.quitting -= HandleQuitting;
        CloseWriter();

        string[] folders = { Application.persistentDataPath, Application.temporaryCachePath };
        string failures = "";

        foreach (string folder in folders)
        {
            if (string.IsNullOrEmpty(folder))
            {
                failures += "(folder path was empty) | ";
                continue;
            }

            try
            {
                Directory.CreateDirectory(folder);
                string logPath = Path.Combine(folder, LogFileName);
                string previousPath = Path.Combine(folder, PreviousLogFileName);

                //Keep previous session log
                if (File.Exists(logPath))
                {
                    if (File.Exists(previousPath)) File.Delete(previousPath);
                    File.Move(logPath, previousPath);
                }

                //AutoFlush hands every line to the system immediately, so it survives a crash
                writer = new StreamWriter(logPath, false) { AutoFlush = true };
                break;
            }
            catch (Exception e)
            {
                writer = null;
                failures += $"{folder}: {e.GetType().Name} - {e.Message} | ";
            }
        }

        //Skip file logging if no file could be created anywhere
        if (writer == null)
        {
            Debug.LogWarning($"[ManualDebugLogger] File logging disabled: {failures}");
            return;
        }

        Application.logMessageReceivedThreaded += HandleLog;
        AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
        TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;
        Application.lowMemory += HandleLowMemory;
        Application.quitting += HandleQuitting;
        
        Write("=== Application Started ===");
        Write($"Version: {Application.version} | Device: {SystemInfo.deviceModel} | OS: {SystemInfo.operatingSystem}");
        Write(
            $"Memory: {SystemInfo.systemMemorySize}MB | CPU: {SystemInfo.processorCount}| Data folder: {Application.persistentDataPath}");
        if (failures != "") Write($"[LOGGER] Earlier location failed: {failures}");
    }

    private static void HandleLog(string message, string stackTrace, LogType type)
    {
       Write($"[{type}] {message}");
       
       // To keep the file readable we give the trace only to errors
       bool isProblem = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
       if (isProblem && !string.IsNullOrEmpty(stackTrace))
       {
           Write(stackTrace);
       }
    }

    private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Write($"[UNHANDLED CRASH] {e.ExceptionObject}");
    }

    private static void HandleUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        Write($"[UNOBSERVED TASK ERROR] {e.Exception}");
    }

    private static void HandleLowMemory()
    {
        Write("[LOW MEMORY] The headset reports low memory. The system might close the game.");
    }

    private static void HandleQuitting()
    {
        Write("=== Game closed normally===");
        CloseWriter();
    }
    
    private static void Write(string line)
    {
        if (writer == null) return;
        try
        {
            lock (writeLock)
            {
                writer?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {line}");
            }
        }
        catch
        {
            //Stop logging in case of writing failure
            writer = null;
        }
    }

    private static void CloseWriter()
    {
        lock (writeLock)
        {
            try{ writer?.Dispose();} catch{}
            writer = null;
        }
    }
}
