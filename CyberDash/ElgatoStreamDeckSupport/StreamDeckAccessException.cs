using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElgatoStreamDeckSupport
{
    public class StreamDeckAccessException : Exception
    {
        public StreamDeckAccessException(string message) : base(message)
        {

        }
    }
}
