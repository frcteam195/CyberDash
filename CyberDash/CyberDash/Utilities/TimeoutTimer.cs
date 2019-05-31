using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CyberDash.Utilities
{
    public class TimeoutTimer
    {
        private ElapsedTimer eTimer = new ElapsedTimer();

        private readonly double mTimeout;
        private bool firstRun = true;

        public TimeoutTimer(double timeout)
        {
            mTimeout = timeout;
        }

        public bool isTimedOut()
        {
            if (firstRun)
            {
                eTimer.start();
                firstRun = false;
            }
            return eTimer.hasElapsed() > mTimeout;
        }

        public double getTimeLeft()
        {
            return Math.Max(mTimeout - eTimer.hasElapsed(), 0);
        }

        public void reset()
        {
            firstRun = true;
        }
    }
}