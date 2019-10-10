using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElgatoStreamDeckSupport
{
    public class VJoyNotEnabledException : Exception
    {
        public VJoyNotEnabledException(string message) : base(message)
        {

        }
    }
}
