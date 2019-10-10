using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElgatoStreamDeckSupport
{
    public class VJoyAcquisitionException : Exception
    {
        public VJoyAcquisitionException(string message) : base(message)
        {

        }
    }
}
