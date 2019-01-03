using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CyberDash.Utilities
{
    public class MovingAverage
    {
        private Queue<double> Entries = new Queue<double>();
        private readonly int MaxSize = 8;
        private decimal Accum = 0;

        public double Average { get; private set; }

        public MovingAverage(int maxSize)
        {
            this.MaxSize = maxSize;
        }

        public void AddEntry(double entry)
        {
            Accum += (decimal)entry;
            Entries.Enqueue(entry);
            if (Entries.Count > MaxSize)
            {
                Accum -= (decimal)Entries.Dequeue();
            }
            Average = (double)(Accum / Entries.Count);
        }

        public void Reset()
        {
            Accum = 0;
            Entries.Clear();
        }
    }
}
