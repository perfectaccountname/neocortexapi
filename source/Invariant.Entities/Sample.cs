using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Invariant.Entities
{
    public class Sample
    {
        public string? Object { get; set; }

        public string? FramePath { get; set; }

        public int[] PixelIndicies { get; set; }

        public Frame? Position { get; set; }
    }
}
