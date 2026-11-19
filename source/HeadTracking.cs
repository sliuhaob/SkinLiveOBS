using System;
using System.Drawing;

namespace SkinLive
{
    internal enum LookMode { Auto, Desktop, Fps, Off }

    internal sealed class HeadTracking
    {
        const double CountsToFullTurn = 700, ReturnSeconds = .55, FollowRate = 11;
        double rawX, rawY, decayTime, lastSampleTime, cursorMovedAt;
        double lastMouseMove = double.NegativeInfinity;
        bool sampled, lockedCursor;
        Point lastCursor;
        public LookMode Mode { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }

        public HeadTracking() { Mode = LookMode.Desktop; }
        public void SetMode(LookMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            sampled = lockedCursor = false;
            lastMouseMove = double.NegativeInfinity;
            X = Y = rawX = rawY = 0;
        }

        static double Clamp(double value) { return Math.Max(-1, Math.Min(1, value)); }
        void Decay(double now)
        {
            double factor = Math.Exp(-Math.Max(0, now - decayTime) / ReturnSeconds);
            rawX *= factor; rawY *= factor; decayTime = now;
        }

        public void MoveMouse(int dx, int dy, double now)
        {
            Decay(now);
            rawX = Clamp(rawX + dx / CountsToFullTurn);
            rawY = Clamp(rawY + dy / CountsToFullTurn);
            lastMouseMove = now;
        }

        public void Update(double now, Point cursor, Rectangle display)
        {
            if (Mode == LookMode.Off) return;
            Decay(now);
            double targetX = rawX, targetY = rawY;
            if (Mode == LookMode.Desktop || Mode == LookMode.Auto) {
                if (!sampled || cursor != lastCursor) {
                    lastCursor = cursor; cursorMovedAt = now; lockedCursor = false;
                }
                bool edge = cursor.X <= display.Left + 2 || cursor.X >= display.Right - 3 ||
                            cursor.Y <= display.Top + 2 || cursor.Y >= display.Bottom - 3;
                // A stationary desktop cursor has no further relative events. A game
                // that pins the cursor still sends them. Resume desktop tracking on movement.
                if (Mode == LookMode.Auto && now - lastMouseMove <= 2.5 &&
                    lastMouseMove - cursorMovedAt >= (edge ? .05 : .60))
                    lockedCursor = true;
                if (!lockedCursor) {
                    targetX = Clamp(2.0 * (cursor.X - display.Left) / Math.Max(1, display.Width - 1) - 1);
                    targetY = Clamp(2.0 * (cursor.Y - display.Top) / Math.Max(1, display.Height - 1) - 1);
                }
            }
            double elapsed = sampled ? Math.Max(0, now - lastSampleTime) : 1.0 / 60;
            double blend = 1 - Math.Exp(-FollowRate * elapsed);
            X += (targetX - X) * blend; Y += (targetY - Y) * blend;
            lastSampleTime = now; sampled = true;
        }
    }
}
