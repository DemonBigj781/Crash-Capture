using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace CrashCatcher
{
    internal static class CallTrail
    {
        private static readonly object gate = new object();
        private static readonly Queue<TrailEntry> entries = new Queue<TrailEntry>(CrashCatcherConfig.DefaultRollingCallCount);
        private static int capacity = CrashCatcherConfig.DefaultRollingCallCount;

        internal static void SetCapacity(int value)
        {
            try
            {
                lock (gate)
                {
                    capacity = Mathf.Clamp(value, CrashCatcherConfig.MinRollingCallCount, CrashCatcherConfig.MaxRollingCallCount);
                    while (entries.Count > capacity)
                    {
                        entries.Dequeue();
                    }
                }
            }
            catch
            {
            }
        }

        internal static void Record(string phase, string call, string detail = null)
        {
            try
            {
                lock (gate)
                {
                    if (entries.Count >= capacity)
                    {
                        entries.Dequeue();
                    }

                    entries.Enqueue(new TrailEntry
                    {
                        Tick = Find.TickManager != null ? Find.TickManager.TicksGame : -1,
                        Phase = phase,
                        Call = call,
                        Detail = detail
                    });

                    TickSequenceRecorder.RecordFromCallTrail(phase, call, detail);
                }
            }
            catch
            {
            }
        }

        internal static string Dump()
        {
            try
            {
                lock (gate)
                {
                    var sb = new StringBuilder();
                    foreach (var entry in entries)
                    {
                        sb.AppendLine($"{entry.Tick}\t[{entry.Phase}] {entry.Call}{(entry.Detail.NullOrEmpty() ? "" : " :: " + entry.Detail)}");
                    }

                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                return "[CrashCatcher] Failed to dump call trail:\n" + ex;
            }
        }

        private sealed class TrailEntry
        {
            public int Tick;
            public string Phase;
            public string Call;
            public string Detail;
        }
    }
}
