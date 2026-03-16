using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace serialWindowsForms
{
    public struct ColorPoint
    {
        public double X;
        public double Y;
        public double Z;
        public byte R;
        public byte G;
        public byte B;

        public ColorPoint(double x, double y, double z, byte r, byte g, byte b)
        {
            X = x;
            Y = y;
            Z = z;
            R = r;
            G = g;
            B = b;
        }
    }
}
