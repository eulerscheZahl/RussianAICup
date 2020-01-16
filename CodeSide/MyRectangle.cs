using AiCup2019.Model;
using System;
namespace AiCup2019
{
    public class MyRectangle
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
        public double VX;
        public double VY;

        public double XRight => X + Width;
        public double YTop => Y + Height;
        public double XCenter => X + Width / 2;
        public double YCenter => Y + Height / 2;


        public MyRectangle(double x, double y, double width, double height)
        {
            this.X = x;
            this.Y = y;
            this.Width = width;
            this.Height = height;
        }

        public MyRectangle(MyRectangle rectangle)
        {
            this.X = rectangle.X;
            this.Y = rectangle.Y;
            this.Width = rectangle.Width;
            this.Height = rectangle.Height;
            this.VX = rectangle.VX;
            this.VY = rectangle.VY;
        }

        public bool Collide(MyRectangle rectangle)
        {
            return this.X < rectangle.XRight && this.XRight > rectangle.X &&
                this.Y < rectangle.YTop && this.YTop > rectangle.Y;
        }

        public double DistToPoint(double x, double y)
        {
            double dx = Math.Min(Math.Abs(x - X), Math.Abs(x - XRight));
            if (X < x && x < XRight) dx = 0;
            double dy = Math.Min(Math.Abs(y - Y), Math.Abs(y - YTop));
            if (Y < y && y < YTop) dy = 0;
            return dx + dy;
        }

        public void Move(double time)
        {
            X += VX * time;
            Y += VY * time;
        }

        public void Draw(ColorFloat color)
        {
            MyStrategy.Debug.Draw(new CustomData.Line(new Vec2Float((float)X, (float)Y), new Vec2Float((float)XRight, (float)Y), 0.05f, color));
            MyStrategy.Debug.Draw(new CustomData.Line(new Vec2Float((float)X, (float)YTop), new Vec2Float((float)XRight, (float)YTop), 0.05f, color));
            MyStrategy.Debug.Draw(new CustomData.Line(new Vec2Float((float)X, (float)Y), new Vec2Float((float)X, (float)YTop), 0.05f, color));
            MyStrategy.Debug.Draw(new CustomData.Line(new Vec2Float((float)XRight, (float)Y), new Vec2Float((float)XRight, (float)YTop), 0.05f, color));
        }

        static bool DoSegmentsIntersect(double p0_x, double p1_x, double p2_x, double p3_x,
                                    double p0_y, double p1_y, double p2_y, double p3_y)
        {
            double s1_x = p1_x - p0_x; double s1_y = p1_y - p0_y;
            double s2_x = p3_x - p2_x; double s2_y = p3_y - p2_y;

            double s = (-s1_y * (p0_x - p2_x) + s1_x * (p0_y - p2_y)) / (-s2_x * s1_y + s1_x * s2_y);
            double t = (s2_x * (p0_y - p2_y) - s2_y * (p0_x - p2_x)) / (-s2_x * s1_y + s1_x * s2_y);

            if (s >= 0 && s <= 1 && t >= 0 && t <= 1)
            {
                return true;
            }
            return false;
        }

        internal bool IsBlocking(MyPoint p1, MyPoint p2)
        {
            if (DoSegmentsIntersect(p1.X, p2.X, X, X, p1.Y, p2.Y, Y, YTop)) return true;
            if (DoSegmentsIntersect(p1.X, p2.X, XRight, XRight, p1.Y, p2.Y, Y, YTop)) return true;
            if (DoSegmentsIntersect(p1.X, p2.X, X, XRight, p1.Y, p2.Y, Y, Y)) return true;
            if (DoSegmentsIntersect(p1.X, p2.X, X, XRight, p1.Y, p2.Y, YTop, YTop)) return true;
            return false;
        }

        internal double PythDist(MyRectangle rectangle)
        {
            double dx = this.XCenter - rectangle.XCenter;
            double dy = this.YCenter - rectangle.YCenter;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
