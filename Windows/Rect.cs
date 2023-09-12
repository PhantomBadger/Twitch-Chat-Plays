using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TwitchPlaysBot.Windows
{
    public struct Rect
    {
        // the x-coordinate of the upper-left corner of the rectangle
        public int Left { get; set; }
        // the y-coordinate of the upper-left corner of the rectangle
        public int Top { get; set; }
        // the x-coordinate of the lower-right corner of the rectangle
        public int Right { get; set; }
        // the y-coordinate of the lower-right corner of the rectangle
        public int Bottom { get; set; }
    }
}
