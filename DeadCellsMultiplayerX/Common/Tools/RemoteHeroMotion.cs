using System;
using dc;
using DeadCellsMultiplayerX.Common.Data;
using DeadCellsMultiplayerX.Common.Math;

namespace DeadCellsMultiplayerX.Common.Tools
{
    /// <summary>
    /// 无状态的远端英雄位置应用器
    /// 插值数据来自世界帧时间线,本类只负责把相邻两帧状态应用到实体
    /// </summary>
    internal static class RemoteHeroMotion
    {
        public const double TeleportDistanceSq = 25.0 * 25.0; // 25 tile
        public const double PositionCorrectionStrength = 2.0;
        public const double VelocityResponse = 0.25;

        public static void Apply(Entity target, PosVector previous, PosVector next, double alpha)
        {
            if (target == null || previous == null || next == null) return;

            double snapX = MathX.Lerp(previous.CX + previous.XR, next.CX + next.XR, alpha);
            double snapY = MathX.Lerp(previous.CY + previous.XY, next.CY + next.XY, alpha);
            double netDx = MathX.Lerp(previous.DX, next.DX, alpha);
            double netDy = MathX.Lerp(previous.DY, next.DY, alpha);
            double netBdx = MathX.Lerp(previous.BDX, next.BDX, alpha);
            double netBdy = MathX.Lerp(previous.BDY, next.BDY, alpha);
            int targetDir = alpha < 0.5 ? previous.DIR : next.DIR;

            double actualX = target.cx + target.xr;
            double actualY = target.cy + target.yr;
            double errX = snapX - actualX;
            double errY = snapY - actualY;

            // 瞬移
            if (errX * errX + errY * errY > TeleportDistanceSq)
            {
                SetPosition(target, snapX, snapY);
                target.dx = netDx;
                target.dy = netDy;
                target.bdx = 0;
                target.bdy = 0;
                if (target.dir != next.DIR) target.dir = next.DIR;
                return;
            }

            // 朝向
            if (target.dir != targetDir) target.dir = targetDir;

            // 位置修正,误差驱动目标速度
            double targetDx = netDx + errX * PositionCorrectionStrength;
            double targetDy = netDy + errY * PositionCorrectionStrength;
            target.dx += (targetDx - target.dx) * VelocityResponse;
            target.dy += (targetDy - target.dy) * VelocityResponse;
            target.bdx = netBdx;
            target.bdy = netBdy;
        }

        public static void SnapTo(Entity target, PosVector pos)
        {
            if (target == null || pos == null) return;

            SetPosition(target, pos.CX + pos.XR, pos.CY + pos.XY);
            target.dx = pos.DX;
            target.dy = pos.DY;
            target.bdx = 0;
            target.bdy = 0;
            if (target.dir != pos.DIR) target.dir = pos.DIR;
        }

        private static void SetPosition(Entity target, double x, double y)
        {
            int cx = (int)System.Math.Floor(x);
            int cy = (int)System.Math.Floor(y);
            target.setPosCase(cx, cy, x - cx, y - cy);
        }
    }
}
