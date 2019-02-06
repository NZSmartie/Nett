using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nett.Attributes
{
    public sealed class TomlMember : Attribute
    {
        public TomlMember()
        {
        }

        public string Key { get; set; } = null;
    }
}
