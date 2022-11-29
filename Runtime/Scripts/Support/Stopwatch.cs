using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System;

namespace Support
{
    public class Stopwatch
    {
        public struct Entry
        {
            public readonly string label;
            public readonly TimeSpan time;

            internal Entry(string label, TimeSpan time)
            {
                this.label = label;
                this.time = time;
            }
        }

        public string label;
        public TimeSpan Start { get; private set; }
        public List<Entry> entrys = new();
        private readonly System.Diagnostics.Stopwatch watch;

        internal Stopwatch(string label)
        {
            watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            this.label = label;
            this.Start = watch.Elapsed;
        }

        internal void Split(string label)
        {
            if (!watch.IsRunning)
            {
                watch.Start();
            }

            entrys.Add(new Entry(label, watch.Elapsed));
        }

        public override string ToString()
        {
            List<string> e = new();
            var startTime = this.Start;
            entrys.ForEach((entry) =>
            {
                var diff = entry.time - Start;
                startTime = entry.time;

                e.Add($"{entry.label} +{Math.Round(diff.TotalSeconds, 2)}s");
            });

            e.Add($"total {Math.Round((startTime - this.Start).TotalSeconds, 2)}s");

            watch.Stop();

            return $"Stopwatch({label}, {string.Join(", ", e.ToArray())})";
        }
    }
}

