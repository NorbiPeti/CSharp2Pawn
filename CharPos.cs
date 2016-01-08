using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharp2PawnLib
{
    public struct CharPos
    {
        public int Column { get; set; }
        public int Line { get; set; }
        public bool IsEmpty()
        {
            return Column == -1 || Line == -1;
        }
    }
}
