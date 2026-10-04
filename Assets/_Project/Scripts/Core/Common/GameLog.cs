using System;
using UnityEngine;

namespace Maze.Core.Common
{
    /// <summary>Log channels from ТЗ §108. The channel is written as a tag so logs can be filtered.</summary>
    public enum LogChannel
    {
        Bootstrap,
        GameFlow,
        Addressables,
        LevelLoading,
        LevelUnloading,
        Validation,
        Save,
        Gameplay,
        Visual,
    }

    /// <summary>
    /// Structured logging (ТЗ §108): "[Maze][Channel] message". Stateless; meant for lifecycle events,
    /// not for per-frame loops.
    /// </summary>
    public static class GameLog
    {
        public static void Info(LogChannel channel, string message) => Debug.Log(Format(channel, message));

        public static void Warning(LogChannel channel, string message) => Debug.LogWarning(Format(channel, message));

        public static void Error(LogChannel channel, string message) => Debug.LogError(Format(channel, message));

        public static void Exception(LogChannel channel, Exception exception, string message)
        {
            Debug.LogError(Format(channel, message + ": " + exception.Message));
            Debug.LogException(exception);
        }

        private static string Format(LogChannel channel, string message) => "[Maze][" + channel + "] " + message;
    }
}
