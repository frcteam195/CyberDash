using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElgatoStreamDeckSupport
{
    public class VJoyAccessException : Exception
    {
        public VJoyAccessException(string message) : base(message)
        {

        }
    }
}
