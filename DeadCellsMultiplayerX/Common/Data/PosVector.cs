using System.Runtime.CompilerServices;
using MessagePack;

namespace DeadCellsMultiplayerX.Common.Data
{
    [MessagePackObject]
    public class PosVector
    {
        [Key(0)] public ulong Packed;
        [Key(1)] public ulong PackedVel;

        private const int CX_BITS = 16;
        private const int CY_BITS = 16;
        private const int XR_BITS = 8;
        private const int XY_BITS = 8;

        private const ulong CX_MASK = (1UL << CX_BITS) - 1;
        private const ulong CY_MASK = ((1UL << CY_BITS) - 1) << 16;
        private const ulong DIR_MASK = 1UL << 32;
        private const ulong XR_MASK = ((1UL << XR_BITS) - 1) << 33;
        private const ulong XY_MASK = ((1UL << XY_BITS) - 1) << 41;

        private const int CY_SHIFT = 16;
        private const int DIR_SHIFT = 32;
        private const int XR_SHIFT = 33;
        private const int XY_SHIFT = 41;
        private const int VEL_BITS = 16;
        private const int VEL_SCALE = 4096;

        private const ulong DX_MASK = (1UL << VEL_BITS) - 1;
        private const ulong DY_MASK = ((1UL << VEL_BITS) - 1) << VEL_BITS;
        private const ulong BDX_MASK = ((1UL << VEL_BITS) - 1) << (VEL_BITS * 2);
        private const ulong BDY_MASK = ((1UL << VEL_BITS) - 1) << (VEL_BITS * 3);

        private const int DX_SHIFT = 0;
        private const int DY_SHIFT = VEL_BITS;
        private const int BDX_SHIFT = VEL_BITS * 2;
        private const int BDY_SHIFT = VEL_BITS * 3;

        [IgnoreMember]
        public int CX
        {
            get => (int)(Packed & CX_MASK);
            set => Packed = (Packed & ~CX_MASK) | ((ulong)value & CX_MASK);
        }

        [IgnoreMember]
        public int CY
        {
            get => (int)((Packed & CY_MASK) >> CY_SHIFT);
            set => Packed = (Packed & ~CY_MASK) | (((ulong)value << CY_SHIFT) & CY_MASK);
        }

        [IgnoreMember]
        public double XR
        {
            get => ((Packed & XR_MASK) >> XR_SHIFT) / 255.0;
            set
            {
                byte quant = (byte)(value * 255.0);
                Packed = (Packed & ~XR_MASK) | ((ulong)quant << XR_SHIFT);
            }
        }

        [IgnoreMember]
        public double XY
        {
            get => ((Packed & XY_MASK) >> XY_SHIFT) / 255.0;
            set
            {
                byte quant = (byte)(value * 255.0);
                Packed = (Packed & ~XY_MASK) | ((ulong)quant << XY_SHIFT);
            }
        }

        [IgnoreMember]
        public int DIR
        {
            get => (Packed & DIR_MASK) != 0 ? -1 : 1;
            set
            {
                if (value < 0)
                    Packed |= DIR_MASK;
                else
                    Packed &= ~DIR_MASK;
            }
        }


        [IgnoreMember]
        public double DX
        {
            get => (short)((PackedVel & DX_MASK) >> DX_SHIFT) / (double)VEL_SCALE;
            set => PackedVel = (PackedVel & ~DX_MASK)
                             | ((ulong)(ushort)QuantVel(value) << DX_SHIFT);
        }

        [IgnoreMember]
        public double DY
        {
            get => (short)((PackedVel & DY_MASK) >> DY_SHIFT) / (double)VEL_SCALE;
            set => PackedVel = (PackedVel & ~DY_MASK)
                             | ((ulong)(ushort)QuantVel(value) << DY_SHIFT);
        }

        [IgnoreMember]
        public double BDX
        {
            get => (short)((PackedVel & BDX_MASK) >> BDX_SHIFT) / (double)VEL_SCALE;
            set => PackedVel = (PackedVel & ~BDX_MASK)
                             | ((ulong)(ushort)QuantVel(value) << BDX_SHIFT);
        }

        [IgnoreMember]
        public double BDY
        {
            get => (short)((PackedVel & BDY_MASK) >> BDY_SHIFT) / (double)VEL_SCALE;
            set => PackedVel = (PackedVel & ~BDY_MASK)
                             | ((ulong)(ushort)QuantVel(value) << BDY_SHIFT);
        }

        private static int QuantVel(double v)
        {
            long q = (long)(v * VEL_SCALE);
            if (q > 32767) q = 32767;
            else if (q < -32768) q = -32768;
            return (int)q;
        }

        public PosVector() { }

        public PosVector(int cx, int cy, double xr, double xy, int dir)
        {
            CX = cx;
            CY = cy;
            XR = xr;
            XY = xy;
            DIR = dir;
        }

        public PosVector(int cx, int cy, double xr, double xy, int dir,
                         double dx, double dy, double bdx, double bdy)
        {
            CX = cx;
            CY = cy;
            XR = xr;
            XY = xy;
            DIR = dir;
            DX = dx;
            DY = dy;
            BDX = bdx;
            BDY = bdy;
        }
    }
}