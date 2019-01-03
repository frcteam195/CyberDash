using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CyberDash.Utilities
{
    public class ThreadRateControl
    {
        private double StartTime = 0;
        private double EndTime = 0;
        private int ElapsedTimeMS = 0;
        private double PrevStartTime = 0;
        private double LoopTimeMS = 0;
        private MovingAverage InternalAverageLoopTime = new MovingAverage(20);
        private Stopwatch stopwatch = Stopwatch.StartNew();

        public ThreadRateControl() { }

        public void Start()
        {
            stopwatch.Reset();
            stopwatch.Start();
            StartTime = stopwatch.ElapsedMilliseconds / 1000.0;
            PrevStartTime = StartTime;
        }

        public void Stop()
        {
            stopwatch.Reset();
            stopwatch.Stop();
            InternalAverageLoopTime.Reset();
        }

        public void DoRateControl(int minLoopTime)
        {
            LoopTimeMS = (StartTime - PrevStartTime) * 1000;
            InternalAverageLoopTime.AddEntry(LoopTimeMS);
            do
            {
                EndTime = stopwatch.ElapsedMilliseconds / 1000.0;
                ElapsedTimeMS = (int)((EndTime - StartTime) * 1000);
                if (ElapsedTimeMS < minLoopTime)
                {
                    try
                    {
                        Thread.Sleep(Math.Abs(minLoopTime - ElapsedTimeMS));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
            } while (ElapsedTimeMS < minLoopTime);
            PrevStartTime = StartTime;
            StartTime = stopwatch.ElapsedMilliseconds / 1000.0;
        }

        public double LoopTime
        {
            get
            {
                return LoopTimeMS;
            }
        }

        public double AverageLoopTime
        {
            get
            {
                return InternalAverageLoopTime.Average;
            }
        }

    }
}
