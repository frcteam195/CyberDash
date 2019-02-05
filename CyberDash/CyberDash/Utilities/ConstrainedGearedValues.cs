using LiveCharts.Geared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CyberDash.Utilities
{
    public class ConstrainedGearedValues<T> : GearedValues<T>
    {
        private int maxSize;

        public ConstrainedGearedValues(int maxSize) : base()
        {
            this.maxSize = maxSize;
        }

        new public void Add(T item)
        {
            base.Add(item);
            if (this.Count > maxSize)
                base.RemoveAt(0);
        }
    }
}
