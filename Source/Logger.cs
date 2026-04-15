using System;
using System.IO;
using Verse;

namespace CrashCatcher
{
    internal static class Logger
    {
        private const string Prefix = "[CrashCatcher]";

        internal static void Message(string message)
        {
            Log.Message($"{Prefix} {message}");
        }

        internal static void Warning(string message)
        {
            Log.Warning($"{Prefix} {message}");
        }

        internal static void Error(string message)
        {
            Log.Error($"{Prefix} {message}");
        }

        internal static void Error(Exception exception, string message)
        {
            Log.Error($"{Prefix} {message}\n{exception}");
        }

        internal static void WriteAllText(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);
        }

        internal static void AppendAllText(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.AppendAllText(path, contents);
        }
    }
}
