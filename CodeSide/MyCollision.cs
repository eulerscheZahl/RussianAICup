using System;
using AiCup2019.Model;

namespace AiCup2019
{
    public class MyCollision
    {
        public MyRectangle R1;
        public MyRectangle R2;
        public double CollisionTime;
        public bool Horizontal;
        public bool Vertical;
        public MyUnit Unit;
        public MyUnit Unit2;
        public MyBullet Bullet;
        public Tile Tile;

        public MyCollision(MyRectangle r1, MyRectangle r2, Tile tile)
        {
            this.R1 = r1;
            this.R2 = r2;
            this.Tile = tile;

            double width = r1.Width + r2.Width;
            double height = r1.Height + r2.Height;
            double vx = r2.VX - r1.VX;
            double vy = r2.VY - r1.VY;
            double px = r2.XRight;
            double py = r2.YTop;

            double txa = (r1.X - px) / vx;
            double txb = (r1.X + width - px) / vx;
            double tx1 = Math.Min(txa, txb);
            double tx2 = Math.Max(txa, txb);
            double tya = (r1.Y - py) / vy;
            double tyb = (r1.Y + height - py) / vy;
            double ty1 = Math.Min(tya, tyb);
            double ty2 = Math.Max(tya, tyb);

            double t1 = Math.Max(tx1, ty1);
            double t2 = Math.Min(tx2, ty2);

            Horizontal = t1 == tx1;
            Vertical = t1 == ty1;
            bool hasCollision = t1 >= 0 && t1 < t2;
            CollisionTime = t1;
            if (!Vertical && tile == Tile.Platform) hasCollision = false;
            if (!hasCollision) CollisionTime = double.PositiveInfinity;
        }
    }
}
