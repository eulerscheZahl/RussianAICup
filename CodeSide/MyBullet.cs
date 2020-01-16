using AiCup2019;
using AiCup2019.Model;
using System;
using System.Collections.Generic;

namespace AiCup2019
{
    public class MyBullet
    {
        public MyRectangle Rectangle;
        public int Damage;
        public int OwnerID;
        public int ExplosionDamage;
        public double ExplosionRange;
        private MyRectangle FinalWall;

        public MyBullet(Bullet bullet)
        {
            this.Rectangle = new MyRectangle(bullet.Position.X - bullet.Size / 2, bullet.Position.Y - bullet.Size / 2, bullet.Size, bullet.Size);
            Rectangle.VX = bullet.Velocity.X / 60;
            Rectangle.VY = bullet.Velocity.Y / 60;
            this.OwnerID = bullet.PlayerId;
            if (bullet.ExplosionParameters.HasValue)
            {
                ExplosionRange = bullet.ExplosionParameters.Value.Radius;
                ExplosionDamage = bullet.ExplosionParameters.Value.Damage;
            }
            this.Damage = bullet.Damage;

            MyCollision firstCollision = null;
            foreach (MyRectangle wall in MyGamestate.Walls)
            {
                MyCollision c = new MyCollision(Rectangle, wall, Tile.Wall) { Bullet = this };
                if (firstCollision == null || c != null && c.CollisionTime < firstCollision.CollisionTime) firstCollision = c;
            }
            FinalWall = firstCollision.R2;
        }

        public MyBullet(MyBullet bullet)
        {
            this.Rectangle = new MyRectangle(bullet.Rectangle);
            this.Damage = bullet.Damage;
            this.OwnerID = bullet.OwnerID;
            this.ExplosionDamage = bullet.ExplosionDamage;
            this.ExplosionRange = bullet.ExplosionRange;
            this.FinalWall = bullet.FinalWall;
        }

        public MyCollision NextCollision(List<MyUnit> units)
        {
            MyCollision result = new MyCollision(Rectangle, FinalWall, Tile.Wall) { Bullet = this };
            foreach (MyUnit unit in units)
            {
                MyCollision collision = new MyCollision(Rectangle, unit.Rectangle, Tile.Empty) { Bullet = this, Unit = unit };
                if (result == null || result.CollisionTime > collision.CollisionTime) result = collision;
            }

            return result;
        }

        public void Collide(MyCollision collision, List<MyUnit> units)
        {
            if (collision.Unit != null) collision.Unit.Hit(Damage);
            MyRectangle explosion = new MyRectangle(Rectangle.XCenter - ExplosionRange, Rectangle.YCenter - ExplosionRange, 2 * ExplosionRange, 2 * ExplosionRange);
            foreach (MyUnit unit in units)
            {
                if (unit.Rectangle.Collide(explosion)) unit.Hit(ExplosionDamage);
            }
        }

        public bool Collide(MyUnit unit)
        {
            return this.Rectangle.Collide(unit.Rectangle);
        }

        public void Draw(Debug debug)
        {
            debug.Draw(new CustomData.Rect(new Vec2Float((float)Rectangle.X, (float)Rectangle.Y), new Vec2Float((float)Rectangle.Width, (float)Rectangle.Height), new ColorFloat(1, 0, 0, 1)));
        }

        public void Attack(MyUnit unit)
        {
            unit.Health -= Damage;
        }

        public void Explode(List<MyUnit> units)
        {
            if (ExplosionRange == 0) return;
            MyRectangle explosion = new MyRectangle(Rectangle.XCenter - ExplosionRange, Rectangle.YCenter - ExplosionRange, 2 * ExplosionRange, 2 * ExplosionRange);
            MyRectangle explosion2 = new MyRectangle(Rectangle.X - ExplosionRange - 0.3, Rectangle.Y - ExplosionRange - 0.3, 2 * ExplosionRange + 0.6, 2 * ExplosionRange + 0.6);
            foreach (MyUnit unit in units)
            {
                if (unit.Rectangle.Collide(explosion)) unit.Health -= ExplosionDamage / 2;
                if (unit.Rectangle.Collide(explosion2)) unit.Health -= ExplosionDamage / 2;
            }
        }
    }
}
