using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CyberDash.Utilities
{
    public class CyberDataItem : IComparable<CyberDataItem>
    {
        public string Key { get; set; }
        public string Value { get; set; }

        public CyberDataItem(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public int CompareTo(CyberDataItem other)
        {
            return Key.CompareTo(other.Key);
        }
    }
}
