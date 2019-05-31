using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CyberDash.Utilities
{
    public class ElapsedTimer
    {
        private double StartTime = 0;
        private Stopwatch stopwatch = Stopwatch.StartNew();

        public ElapsedTimer() { }

        public void start()
        {
            stopwatch.Reset();
            stopwatch.Start();
            StartTime = stopwatch.ElapsedMilliseconds / 1000.0;
        }

        public double hasElapsed()
        {
            return stopwatch.ElapsedMilliseconds / 1000.0;
        }
    }
}
