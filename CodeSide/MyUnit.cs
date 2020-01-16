using AiCup2019.Model;
using System;

namespace AiCup2019
{
    public class MyUnit
    {
        public bool Friendly;
        public MyRectangle Rectangle;
        public int Health;
        public int ID;
        public bool CanJump;
        public bool CanCancelJump;
        public int JumpMaxTime;
        public double MoveSpeed;
        public double JumpSpeed;
        public MyWeaponType GunType = MyWeaponType.NONE;
        public MyAction[] Actions = new MyAction[5];
        public bool PadJump => CanJump && !CanCancelJump;
        public bool Ammo;
        public bool GotHeal = false;
        public bool GotMine = false;
        public bool Reloading;
        private bool turnPad;

        public MyUnit(Unit unit, bool mine)
        {
            this.Friendly = mine;
            Rectangle = new MyRectangle(unit.Position.X - unit.Size.X / 2, unit.Position.Y, unit.Size.X, unit.Size.Y);
            this.Health = unit.Health;
            this.ID = unit.Id;
            this.MoveSpeed = 10.0 / 60;
            this.CanJump = unit.JumpState.CanJump;
            this.CanCancelJump = unit.JumpState.CanCancel;
            this.JumpMaxTime = (int)Math.Round(60 * unit.JumpState.MaxTime);
            this.JumpSpeed = unit.JumpState.Speed / 60;
            if (unit.Weapon.HasValue)
            {
                GunType = (MyWeaponType)unit.Weapon.Value.Typ;
                Ammo = !unit.Weapon.Value.FireTimer.HasValue || unit.Weapon.Value.FireTimer.Value <= 1.0 / 60;
                Reloading = unit.Weapon.Value.FireTimer.HasValue && unit.Weapon.Value.FireTimer.Value > unit.Weapon.Value.Parameters.Recoil;
            }
            UnstuckEdge();
        }

        public MyUnit(MyUnit unit)
        {
            Rectangle = new MyRectangle(unit.Rectangle);
            this.Friendly = unit.Friendly;
            this.Health = unit.Health;
            this.ID = unit.ID;
            this.MoveSpeed = unit.MoveSpeed;
            this.CanJump = unit.CanJump;
            this.CanCancelJump = unit.CanCancelJump;
            this.JumpMaxTime = unit.JumpMaxTime;
            this.JumpSpeed = unit.JumpSpeed;
            this.GunType = unit.GunType;
            this.Ammo = unit.Ammo;
            this.Reloading = unit.Reloading;
        }

        public double Dist(MyRectangle rect)
        {
            return Dist(rect.XCenter, rect.YCenter);
        }

        public double Dist(double x, double y)
        {
            int targetX = (int)x;
            int targetY = (int)y;
            int cellX = (int)Rectangle.XCenter;
            int cellY = (int)Rectangle.YCenter;
            double result = MyGamestate.Dist[cellX, cellY, targetX, targetY];
            if (cellX > 0 && MyGamestate.Dist[cellX - 1, cellY, targetX, targetY] < MyGamestate.Dist[cellX, cellY, targetX, targetY])
                result += (Rectangle.XCenter - cellX) * MyGamestate.DistX;
            if (cellY > 0 && MyGamestate.Dist[cellX, cellY - 1, targetX, targetY] < MyGamestate.Dist[cellX, cellY, targetX, targetY])
                result += (Rectangle.YCenter - cellY) * MyGamestate.DistY;
            if (cellX < MyGamestate.Width - 1 && MyGamestate.Dist[cellX + 1, cellY, targetX, targetY] < MyGamestate.Dist[cellX, cellY, targetX, targetY])
                result += (cellX + 1 - Rectangle.XCenter) * MyGamestate.DistX;
            if (cellY < MyGamestate.Height - 1 && MyGamestate.Dist[cellX, cellY + 1, targetX, targetY] < MyGamestate.Dist[cellX, cellY, targetX, targetY])
                result += (cellY + 1 - Rectangle.YCenter) * MyGamestate.DistY;
            return result;
        }

        public void InitMove(int depth)
        {
            turnPad = false;
            if (Actions[depth] == null) Actions[depth] = MyAction.Default;
            if (Actions[depth].Horizontal == HorizontalMovement.STAY) Rectangle.VX = 0;
            if (Actions[depth].Horizontal == HorizontalMovement.LEFT) Rectangle.VX = -10.0 / 60;
            if (Actions[depth].Horizontal == HorizontalMovement.RIGHT) Rectangle.VX = 10.0 / 60;

            if (JumpMaxTime == 0 || !PadJump && Actions[depth].Vertical != VerticalMovement.UP)
            {
                CanJump = false;
                CanCancelJump = true;
                JumpMaxTime = 0;
            }
            JumpMaxTime = Math.Max(0, JumpMaxTime - 1);

            bool onLadder = OnLadder();
            if (!PadJump && onLadder)
            {
                CanJump = true;
                JumpMaxTime = 32;
                JumpSpeed = 10.0 / 60;

            }
            Rectangle.VY = -10.0 / 60;
            if (Actions[depth].Vertical == VerticalMovement.UP && JumpMaxTime > 0 || PadJump) Rectangle.VY = JumpSpeed;
            if (!PadJump && onLadder && Actions[depth].Vertical == VerticalMovement.STAY) Rectangle.VY = 0;
        }

        public MyCollision NextCollision(int depth, MyUnit partner)
        {
            MyCollision result = null;

            if (partner != null) result = new MyCollision(Rectangle, partner.Rectangle, Tile.Empty) { Unit = this, Unit2 = partner };

            foreach (MyRectangle wall in MyGamestate.WallMap[(int)Rectangle.XCenter, (int)Rectangle.YCenter])
            {
                MyCollision collision = new MyCollision(Rectangle, wall, Tile.Wall) { Unit = this };
                if (result == null || result.CollisionTime > collision.CollisionTime) result = collision;
            }

            if (!PadJump)
            {
                if (!turnPad)
                {
                    foreach (MyRectangle pad in MyGamestate.JumpPadMap[(int)Rectangle.XCenter, (int)Rectangle.YCenter])
                    {
                        MyCollision collision = new MyCollision(Rectangle, pad, Tile.JumpPad) { Unit = this };
                        if (result == null || result.CollisionTime > collision.CollisionTime) result = collision;
                    }
                }

                if (Actions[depth].Vertical == VerticalMovement.STAY)
                {
                    foreach (MyRectangle platform in MyGamestate.PlatformMap[(int)Rectangle.XCenter, (int)Rectangle.YCenter])
                    {
                        MyCollision collision = new MyCollision(Rectangle, platform, Tile.Platform) { Unit = this };
                        if (result == null || result.CollisionTime > collision.CollisionTime) result = collision;
                    }
                }
            }

            return result;
        }

        private void StopJump(bool fall)
        {
            JumpMaxTime = 0;
            CanJump = false;
            CanCancelJump = false;
            JumpSpeed = fall ? -10.0 / 60 : 0;
            Rectangle.VY = JumpSpeed;
        }

        public void Collide(MyCollision collision)
        {
            if (collision.Unit2 != null)
            {
                MyUnit partner = collision.Unit2;
                if (collision.Vertical)
                {
                    this.StopJump(false);
                    partner.StopJump(false);
                    if (this.Rectangle.Y < partner.Rectangle.Y)
                    {
                        this.Rectangle.Y -= 1e-9;
                        partner.Rectangle.Y += 1e-9;
                    }
                    if (partner.Rectangle.Y < this.Rectangle.VY)
                    {
                        partner.Rectangle.Y -= 1e-9;
                        this.Rectangle.Y += 1e-9;
                    }
                }
                if (collision.Horizontal)
                {
                    this.Rectangle.VX = 0;
                    partner.Rectangle.VX = 0;
                    if (this.Rectangle.X < partner.Rectangle.X)
                    {
                        this.Rectangle.X -= 1e-9;
                        partner.Rectangle.X += 1e-9;
                    }
                    else
                    {
                        partner.Rectangle.X -= 1e-9;
                        this.Rectangle.X += 1e-9;
                    }
                }
                this.UnstuckEdge();
                partner.UnstuckEdge();
                return;
            }

            if (collision.Vertical && collision.Tile == Tile.Wall)
            {
                StopJump(true);
                if (collision.R2.Y < Rectangle.Y)
                {
                    JumpMaxTime = 32;
                    JumpSpeed = 10.0 / 60;
                    Rectangle.VY = 0;
                    CanJump = true;
                    CanCancelJump = true;
                }
            }
            if (collision.Vertical && !PadJump && collision.Tile == Tile.Platform)
            {
                Rectangle.VY = 0;
                CanJump = true;
                CanCancelJump = true;
                JumpMaxTime = 32;
                JumpSpeed = 10.0 / 60;
            }
            if (collision.Tile == Tile.JumpPad)
            {
                Rectangle.VY = 20.0 / 60;
                JumpSpeed = Rectangle.VY;
                CanJump = true;
                CanCancelJump = false;
                JumpMaxTime = 32;
                turnPad = true;
            }
            if (collision.Horizontal && collision.Tile == Tile.Wall) Rectangle.VX = 0;

            UnstuckEdge();
        }

        public UnitAction GetGameAction()
        {
            UnitAction action = new UnitAction();
            action.Jump = Actions[0].Vertical == VerticalMovement.UP;
            action.JumpDown = Actions[0].Vertical == VerticalMovement.DOWN;
            action.Velocity = 0;
            if (Actions[0].Horizontal == HorizontalMovement.LEFT) action.Velocity = -10;
            if (Actions[0].Horizontal == HorizontalMovement.RIGHT) action.Velocity = 10;
            return action;
        }

        public void Hit(int damage)
        {
            Health = Math.Max(0, Health - damage);
        }

        public bool OnLadder()
        {
            foreach (MyRectangle ladder in MyGamestate.LadderMap[(int)Rectangle.XCenter, (int)Rectangle.YCenter])
            {
                if (ladder.DistToPoint(Rectangle.XCenter, Rectangle.YCenter) == 0 || ladder.DistToPoint(Rectangle.XCenter, Rectangle.Y) == 0) return true;
            }
            return false;
        }

        private void UnstuckEdge()
        {
            if (Math.Abs(Rectangle.X - Math.Round(Rectangle.X)) < 1e-9) Rectangle.X = Math.Round(Rectangle.X) + 1e-9;
            if (Math.Abs(Rectangle.Y - Math.Round(Rectangle.Y)) < 1e-9) Rectangle.Y = Math.Round(Rectangle.Y) + 1e-9;
            if (Math.Abs(Rectangle.XRight - Math.Round(Rectangle.XRight)) < 1e-9) Rectangle.X = Math.Round(Rectangle.XRight) - Rectangle.Width - 1e-9;
            if (Math.Abs(Rectangle.YTop - Math.Round(Rectangle.YTop)) < 1e-9) Rectangle.Y = Math.Round(Rectangle.YTop) - Rectangle.Height - 1e-9;
        }
    }
}
