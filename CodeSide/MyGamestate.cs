using AiCup2019.Model;

using System;
using System.Collections.Generic;
using System.Linq;

namespace AiCup2019
{
    public class MyGamestate
    {
        public static int Width;
        public static int Height;
        public static int[,,,] Dist; // x1, y1, x2, y2
        public static int DistX = 3;
        public static int DistY = 2;
        public static List<MyRectangle> Walls;
        public static List<MyRectangle> Ladders;
        public static List<MyRectangle> Platforms;
        public static List<MyRectangle> JumpPads;
        public static List<MyRectangle>[,] WallMap;
        public static List<MyRectangle>[,] LadderMap;
        public static List<MyRectangle>[,] PlatformMap;
        public static List<MyRectangle>[,] JumpPadMap;
        public static List<MyBox> Boxes;
        public List<MyUnit> Units;
        public List<MyBullet> Bullets;
        public static int FrameSkip = 5;
        private double[] cumulativeScore;
        private double[] positionScore;
        private static MyBox[] closestHeal;
        private static MyBox[] closestMine;
        private static MyBox[] closestWeapon;
        public static List<MyUnit> opponents;
        private static bool[] canShoot;
        private static double[] fightX;
        private static double[] fightY;
        private static List<(int x, int y)> reachable = new List<(int x, int y)>();
        private static Tile[][] level;
        public static List<MyBox> Heals;
        private static List<MyRectangle> DangerZones = new List<MyRectangle>();

        public MyGamestate(Game game, Unit unit)
        {
            Units = game.Units.OrderBy(u => (u.Id == unit.Id ? 0 : 1) + (u.PlayerId == unit.PlayerId ? 0 : 10)).Select(u => new MyUnit(u, unit.PlayerId == u.PlayerId)).ToList();
            opponents = Units.Where(u => !u.Friendly).ToList();
            Units = Units.Where(u => u.Friendly).ToList();
            DangerZones.Clear();
            if (Units.Count == 1 && (game.Players.First(p => p.Id != unit.PlayerId).Score + Units[0].Health) % 1000 > game.Players.First(p => p.Id == unit.PlayerId).Score % 1000)
            {
                foreach (Unit u in game.Units.Where(un => un.PlayerId != unit.PlayerId))
                {
                    if (50 * u.Mines >= Units[0].Health) DangerZones.Add(new MyRectangle(u.Position.X - 3.2, u.Position.Y + 0.25 - 3.2, 6.4, 6.4));
                }
            }

            if (Walls == null)
            {
                level = game.Level.Tiles;
                Width = game.Level.Tiles.Length;
                Height = game.Level.Tiles[0].Length;
                Walls = CreateMap(game.Level.Tiles, Tile.Wall);
                Ladders = CreateMap(game.Level.Tiles, Tile.Ladder);
                JumpPads = CreateMap(game.Level.Tiles, Tile.JumpPad);
                Platforms = CreateMap(game.Level.Tiles, Tile.Platform);
                WallMap = CacheMap(Walls);
                LadderMap = CacheMap(Ladders);
                PlatformMap = CacheMap(Platforms);
                JumpPadMap = CacheMap(JumpPads);
                InitDistance(game.Level.Tiles);

                for (int y = Height - 1; y >= 0; y--)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        Console.Write(".#^HT"[(int)game.Level.Tiles[x][y]]);
                        if (game.Level.Tiles[x][y] != Tile.Wall && Dist[x, y, (int)Units[0].Rectangle.XCenter, (int)Units[0].Rectangle.YCenter] != int.MaxValue) reachable.Add((x, y));
                    }
                    Console.WriteLine();
                }
            }

            Boxes = new List<MyBox>();
            foreach (var box in game.LootBoxes)
            {
                Boxes.Add(new MyBox(new MyRectangle(box.Position.X - box.Size.X / 2, box.Position.Y, box.Size.X, box.Size.Y)));
                if (box.Item is Item.Weapon) Boxes.Last().Weapon = (MyWeaponType)(box.Item as Item.Weapon).WeaponType;
                if (box.Item is Item.HealthPack) Boxes.Last().Heal = true;
                if (box.Item is Item.Mine) Boxes.Last().Mine = true;
            }
            Heals = Boxes.Where(b => b.Heal).ToList();
            List<MyBox> weapons = Boxes.Where(b => b.Weapon != MyWeaponType.NONE).ToList();
            int healTreshold = 80;
            if (!opponents.Any(o => o.GunType == MyWeaponType.ROCKET)) healTreshold = 60;

            cumulativeScore = new double[Units.Count];
            positionScore = new double[Units.Count];
            closestHeal = new MyBox[Units.Count];
            closestMine = new MyBox[Units.Count];
            closestWeapon = new MyBox[Units.Count];
            canShoot = new bool[Units.Count];
            for (int i = 0; i < Units.Count; i++)
            {
                positionScore[i] = int.MinValue;
                canShoot[i] = /* !Units[i].Ammo ||*/ opponents.Any(o => CanShoot(Units[i], o, true)) && Units[i].GunType != MyWeaponType.NONE;
                if (Units[i].Health <= healTreshold && Heals.Count > 0)
                {
                    List<MyBox> reachableHeals = Heals.Where(b => !opponents.Any(o => o.Rectangle.Collide(b.Rectangle))).ToList();
                    if (reachableHeals.Count > 0)
                        closestHeal[i] = reachableHeals.OrderBy(b => Units[i].Dist(b.Rectangle) + (opponents.Any(o => o.Dist(b.Rectangle) < Units[i].Dist(b.Rectangle)) ? 100 : 0)).First();
                }
                if (Boxes.Any(b => b.Mine))
                {
                    closestMine[i] = Boxes.Where(b => b.Mine).OrderBy(b => Units[i].Dist(b.Rectangle)).First();
                }
                if (Units[i].GunType == MyWeaponType.NONE) closestWeapon[i] = weapons.OrderBy(w => Units[i].Dist(w.Rectangle)).First();
                else if (Units[i].GunType == MyWeaponType.ROCKET) closestWeapon[i] = weapons.Where(w => w.Weapon != MyWeaponType.ROCKET).OrderBy(w => Units[i].Dist(w.Rectangle)).First();
            }
            if (Units.Count == 2 && closestWeapon[0] != null && closestWeapon[0] == closestWeapon[1])
            {
                int farIndex = Units.IndexOf(Units.OrderBy(u => u.Dist(closestWeapon[0].Rectangle)).Last());
                if (Units[farIndex].GunType == MyWeaponType.NONE) closestWeapon[farIndex] = weapons.Where(w => w != closestWeapon[0]).OrderBy(w => Units[farIndex].Dist(w.Rectangle)).First();
                else if (Units[farIndex].GunType == MyWeaponType.ROCKET) closestWeapon[farIndex] = weapons.Where(w => w != closestWeapon[0] && w.Weapon != MyWeaponType.ROCKET).OrderBy(w => Units[farIndex].Dist(w.Rectangle)).First();
            }

            Bullets = game.Bullets.Select(b => new MyBullet(b)).ToList();

#if DEBUG
            Walls.ForEach(w => w.Draw(new ColorFloat(1, 0, 0, 1)));
            Ladders.ForEach(w => w.Draw(new ColorFloat(0, 1, 0, 1)));
            Platforms.ForEach(w => w.Draw(new ColorFloat(0, 0, 1, 1)));
            JumpPads.ForEach(w => w.Draw(new ColorFloat(1, 1, 0, 1)));

            //for (int i = 0; i < Units.Count; i++)
            //{
            //    MyStrategy.Debug.Draw(new CustomData.Rect(new Vec2Float((float)fightX[i] - 0.2f, (float)fightY[i] - 0.2f), new Vec2Float(0.4f, 0.4f), new ColorFloat(1, 1, 1, 1)));
            //}

            int unitX = (int)Units[0].Rectangle.XCenter;
            int unitY = (int)Units[0].Rectangle.YCenter;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int dist = Dist[Width / 2, Height - 2, x, y];
                    if (dist == int.MaxValue) continue;
                    //MyStrategy.Debug.Draw(new CustomData.PlacedText(dist.ToString(), new Vec2Float(x + 0.5f, y + 0.5f), TextAlignment.Center, 10, new ColorFloat(1, 1, 1, 1)));
                }
            }
#endif
        }

        public MyGamestate(MyGamestate state)
        {
            Units = state.Units.Select(u => new MyUnit(u)).ToList();
            Bullets = state.Bullets.Select(b => new MyBullet(b)).ToList();
            cumulativeScore = new double[Units.Count];
            positionScore = new double[Units.Count];
            for (int i = 0; i < Units.Count; i++)
            {
                positionScore[i] = int.MinValue;
            }
        }

        public void UpdateFightLocations()
        {
            fightX = new double[Units.Count];
            fightY = new double[Units.Count];

            List<MyRectangle> rectBackup = Units.Select(u => new MyRectangle(u.Rectangle)).ToList();
            List<(int x, int y)>[] candidates = new List<(int x, int y)>[Units.Count];
            for (int unitIndex = 0; unitIndex < Units.Count; unitIndex++)
            {
                MyUnit unit = Units[unitIndex];
                int centerX = (int)rectBackup[unitIndex].XCenter;
                int centerY = (int)rectBackup[unitIndex].YCenter;
                candidates[unitIndex] = new List<(int x, int y)>();
                foreach ((int x, int y) cell in reachable)
                {
                    if (opponents.All(o => o.Dist(cell.x, cell.y) > 20)) continue;
                    unit.Rectangle.X = cell.x;
                    unit.Rectangle.Y = cell.y;
                    if (opponents.Any(o => o.Dist(cell.x, cell.y) < Dist[cell.x, cell.y, centerX, centerY] + 5)) continue;
                    if (!opponents.Any(o => CanShoot(unit, o, false))) continue;
                    candidates[unitIndex].Add(cell);
                }
                unit.Rectangle = new MyRectangle(rectBackup[unitIndex]);
                if (candidates[unitIndex].Count == 0)
                {
                    MyUnit opp = opponents.OrderBy(o => o.Dist(unit.Rectangle)).First();
                    foreach ((int x, int y) cell in reachable)
                    {
                        double dist = unit.Dist(cell.x, cell.y) - opp.Dist(cell.x, cell.y);
                        if (dist > 0 && dist <= 10)
                            candidates[unitIndex].Add(cell);
                    }
                }
                if (candidates[unitIndex].Count > 30)
                {
                    candidates[unitIndex] = candidates[unitIndex].OrderBy(c => unit.Dist(c.x, c.y)).Take(30).ToList();
                }

#if DEBUG
                foreach (var cell in candidates[unitIndex])
                {
                    new MyRectangle(cell.x, cell.y, 1, 1).Draw(new ColorFloat(1, 1, 0, 1));
                }
#endif
            }

            double bestScore = int.MinValue;
            foreach ((int x, int y) cell in candidates[0])
            {
                Units[0].Rectangle.X = cell.x;
                Units[0].Rectangle.Y = cell.y;
                if (Units.Count == 1)
                {
                    double tmpScore = ScoreDefense();
#if DEBUG
                    MyStrategy.Debug.Draw(new CustomData.PlacedText(tmpScore.ToString("0.##"), new Vec2Float(cell.x + 0.5f, cell.y + 0.5f), TextAlignment.Center, 12, new ColorFloat(1, 0, 0, 1)));
#endif

                    if (tmpScore > bestScore)
                    {
                        bestScore = tmpScore;
                        fightX[0] = Units[0].Rectangle.X;
                        fightY[0] = Units[0].Rectangle.Y;
                    }
                }
                else
                {
                    foreach ((int x, int y) cell1 in candidates[1])
                    {
                        Units[1].Rectangle.X = cell1.x;
                        Units[1].Rectangle.Y = cell1.y;
                        double tmpScore = ScoreDefense();
                        if (tmpScore > bestScore)
                        {
                            bestScore = tmpScore;
                            fightX[0] = Units[0].Rectangle.X;
                            fightY[0] = Units[0].Rectangle.Y;
                            fightX[1] = Units[1].Rectangle.X;
                            fightY[1] = Units[1].Rectangle.Y;
                        }
                    }
                }
            }
            for (int i = 0; i < Units.Count; i++)
            {
                Units[i].Rectangle = rectBackup[i];
            }
#if DEBUG
            for (int i = 0; i < Units.Count; i++)
            {
                MyStrategy.Debug.Draw(new CustomData.Rect(new Vec2Float((float)fightX[i], (float)fightY[i]), new Vec2Float(1, 1), new ColorFloat(1, 1, 0, 1)));
            }
#endif
        }

        private double ScoreDefense()
        {
            double score = 0;
            foreach (MyUnit me in Units)
            {
                if (opponents.Any(o => CanShoot(me, o, true))) score += 2;
                int x = (int)me.Rectangle.XCenter;
                int y = (int)me.Rectangle.YCenter;
                if (level[x][y] == Tile.Ladder) score += 0.7;
                else if (level[x][y - 1] == Tile.Platform) score += 0.5;
                else if (level[x][y - 1] == Tile.Wall) score += 0.3;
            }
            foreach (MyBox heal in Heals)
            {
                double meDist = Units.Min(u => u.Dist(heal.Rectangle));
                double oppDist = opponents.Min(u => u.Dist(heal.Rectangle));
                if (meDist < oppDist) score += Math.Min(10, (oppDist - meDist));
            }
            if (Units.Count == 2)
            {
                double dist = Units[0].Rectangle.PythDist(Units[1].Rectangle);
                if (dist < 4) score -= 2 * (4 - dist);
            }

            return score;
        }

        public void SimPlan(int firstTurnSteps)
        {
            int factor = 100;
            for (int depth = 0; depth < Units[0].Actions.Length; depth++)
            {
                int steps = depth == 0 ? firstTurnSteps : FrameSkip;
                for (int ticks = 0; ticks < steps; ticks++) Step(depth, factor--);
            }
        }

        private static void InitDistance(Tile[][] map)
        {
            Dist = new int[Width, Height, Width, Height];
            for (int x1 = 0; x1 < Width; x1++)
                for (int y1 = 0; y1 < Height; y1++)
                    for (int x2 = 0; x2 < Width; x2++)
                        for (int y2 = 0; y2 < Height; y2++)
                            Dist[x1, y1, x2, y2] = int.MaxValue;

            int[] dx = { -1, -1, -1, 0, 0, 1, 1, 1 };
            int[] dy = { -1, 0, 1, -1, 1, -1, 0, 1 };
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (map[x][y] == Tile.Wall) continue;
                    List<(int x1, int y1)>[] bfs = new List<(int x1, int y1)>[Width * Height * DistX];
                    bfs[0] = new List<(int x1, int y1)> { (x, y) };
                    for (int depth = 0; depth < bfs.Length; depth++)
                    {
                        if (bfs[depth] == null) continue;
                        foreach ((int x1, int y1) in bfs[depth])
                        {
                            if (Dist[x, y, x1, y1] != int.MaxValue) continue;
                            Dist[x, y, x1, y1] = depth;
                            for (int dir = 0; dir < dx.Length; dir++)
                            {
                                int x2 = x1 + dx[dir];
                                int y2 = y1 + dy[dir];
                                if (x2 < 0 || x2 >= Width || y2 < 0 || y2 >= Height) continue;
                                if (Dist[x, y, x2, y2] != int.MaxValue) continue;
                                int dist = Dist[x, y, x1, y1] + (dx[dir] == 0 ? DistY : DistX);
                                if (map[x2][y2] == Tile.Wall)
                                {
                                    Dist[x2, y2, x, y] = Dist[x, y, x2, y2];
                                    Dist[x, y, x2, y2] = dist;
                                }
                                else
                                {
                                    if (map[x1][y2] == Tile.Wall|| map[x2][y1] == Tile.Wall) continue;
                                    if (bfs[dist] == null) bfs[dist] = new List<(int x1, int y1)>();
                                    bfs[dist].Add((x2, y2));
                                }
                            }
                        }
                    }
                }
            }
        }

        private List<MyRectangle> CreateMap(Tile[][] map, Tile tile)
        {
            List<MyRectangle> result = new List<MyRectangle>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (map[x][y] == tile) result.Add(new MyRectangle(x, y, 1, 1));
                }
            }

            foreach (MyRectangle r1 in result.ToList())
            {
                if (!result.Contains(r1)) continue;
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    foreach (MyRectangle r2 in result)
                    {
                        if (r1.Y == r2.Y && r1.Height == r2.Height && r1.XRight == r2.X)
                        {
                            result.Remove(r2);
                            r1.Width += r2.Width;
                            changed = true;
                            break;
                        }
                        if (r1.X == r2.X && r1.Width == r2.Width && r1.YTop == r2.Y)
                        {
                            result.Remove(r2);
                            r1.Height += r2.Height;
                            changed = true;
                            break;
                        }
                    }
                }
            }

            return result;
        }

        private List<MyRectangle>[,] CacheMap(List<MyRectangle> rects)
        {
            List<MyRectangle>[,] result = new List<MyRectangle>[Width, Height];
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    result[x, y] = rects.Where(r => r.DistToPoint(x + 0.5, y + 0.5) < 3).ToList();
                }
            }

            return result;
        }

        public void Step(int depth, int scoreFactor)
        {
            foreach (MyUnit unit in Units) unit.InitMove(depth);
            double t = 0;
            int[] initialHealth = Units.Where(u => u.Friendly).Select(u => u.Health).ToArray();
            while (t < 1 - 1e-9)
            {
                MyCollision collision = null;
                MyUnit partner = Units.First(u => u.Friendly);
                foreach (MyUnit unit in Units)
                {
                    MyCollision c = unit.NextCollision(depth, unit.Friendly && unit != partner ? partner : null);
                    if (collision == null || c != null && c.CollisionTime < collision.CollisionTime) collision = c;
                }
                foreach (MyBullet bullet in Bullets)
                {
                    MyCollision c = bullet.NextCollision(Units);
                    if (collision == null || c != null && c.CollisionTime < collision.CollisionTime) collision = c;
                }
                double moveTime = collision == null ? 1 - t : Math.Min(1 - t, collision.CollisionTime);
                foreach (MyUnit unit in Units) unit.Rectangle.Move(moveTime);
                foreach (MyBullet bullet in Bullets) bullet.Rectangle.Move(moveTime);

                if (collision != null && collision.CollisionTime == moveTime)
                {
                    if (collision.Bullet != null)
                    {
                        collision.Bullet.Collide(collision, Units);
                        Bullets.Remove(collision.Bullet);
                    }
                    else collision.Unit.Collide(collision);
                }

                // todo: explode mines, handle ladders, collect items

                t += moveTime;
            }
            int[] finalHealth = Units.Where(u => u.Friendly).Select(u => u.Health).ToArray();
            for (int i = 0; i < initialHealth.Length; i++)
            {
                cumulativeScore[i] -= 1000 * scoreFactor * (initialHealth[i] - finalHealth[i]);
            }
            // if (Units.Count == 2)
            // {
            //     double dist = Units[0].Rectangle.PythDist(Units[1].Rectangle);
            //     if (dist < 4) cumulativeScore[1] -= scoreFactor * (4 - dist);
            // }
            for (int unitIndex = 0; unitIndex < Units.Count; unitIndex++)
            {
                MyUnit unit = Units[unitIndex];
                MyUnit opp = opponents.OrderBy(o => unit.Dist(o.Rectangle)).First();
                double dist = unit.Dist(fightX[unitIndex], fightY[unitIndex]);
                if (closestHeal[unitIndex] == null && canShoot[unitIndex]) cumulativeScore[unitIndex] -= scoreFactor * dist * 0.01;
                if (closestWeapon[unitIndex] != null && unit.Rectangle.Collide(closestWeapon[unitIndex].Rectangle))
                {
                    cumulativeScore[unitIndex] += 100 * scoreFactor;
                    unit.GunType = closestWeapon[unitIndex].Weapon;
                }
                MyBox heal = closestHeal[unitIndex];
                if (!unit.GotHeal && heal != null && unit.Rectangle.Collide(heal.Rectangle))
                {
                    cumulativeScore[unitIndex] += 100 * scoreFactor;
                    unit.GotHeal = true;
                }
                if (unit.GunType != MyWeaponType.NONE && !unit.GotMine && closestMine[unitIndex] != null && unit.Rectangle.Collide(closestMine[unitIndex].Rectangle))
                {
                    cumulativeScore[unitIndex] += 20 * scoreFactor;
                    unit.GotMine = true;
                }
                foreach (MyRectangle danger in DangerZones)
                {
                    if (unit.Rectangle.Collide(danger)) cumulativeScore[unitIndex] -= 1000 * scoreFactor;
                }
                if (unit.Actions[depth].Vertical == VerticalMovement.UP) cumulativeScore[unitIndex] -= 1e-3;
            }

            if (depth >= 4) // score final position or intermediate position (hard to get high depth right)
            {
                ScoreFinal();
            }
        }

        public void ScoreFinal()
        {
            for (int unitIndex = 0; unitIndex < Units.Count; unitIndex++)
            {
                MyUnit unit = Units[unitIndex];
                MyUnit opp = opponents.OrderBy(o => unit.Dist(o.Rectangle)).First();
                double targetX = fightX[unitIndex];
                double targetY = fightY[unitIndex];
                if (unit.GunType == MyWeaponType.NONE)
                {
                    MyBox box = closestWeapon[unitIndex];
                    targetX = box.Rectangle.XCenter;
                    targetY = box.Rectangle.YCenter;
                }
                else if (closestHeal[unitIndex] != null && !unit.GotHeal)
                {
                    targetX = closestHeal[unitIndex].Rectangle.XCenter;
                    targetY = closestHeal[unitIndex].Rectangle.YCenter;
                }
                else if (unit.GunType == MyWeaponType.ROCKET)
                {
                    MyBox box = closestWeapon[unitIndex];
                    targetX = box.Rectangle.XCenter;
                    targetY = box.Rectangle.YCenter;
                }

                else if (closestMine[unitIndex] != null && !unit.GotMine)
                {
                    targetX = closestMine[unitIndex].Rectangle.XCenter;
                    targetY = closestMine[unitIndex].Rectangle.YCenter;
                }
                else if (!canShoot[unitIndex])
                {
                    targetX = opp.Rectangle.XCenter;
                    targetY = opp.Rectangle.YCenter;
                }
                positionScore[unitIndex] = Math.Max(positionScore[unitIndex], -unit.Dist(targetX, targetY));
            }
        }

        public bool CanShoot(MyUnit from, MyUnit to, bool blockTeammate)
        {
            List<MyPoint> rayStart = new List<MyPoint>();
            MyPoint target = new MyPoint(to.Rectangle.XCenter, to.Rectangle.YCenter);
            if (from.GunType == MyWeaponType.ROCKET)
            {
                rayStart.Add(new MyPoint(from.Rectangle.XCenter - 0.4, from.Rectangle.YCenter - 0.8));
                rayStart.Add(new MyPoint(from.Rectangle.XCenter + 0.4, from.Rectangle.YCenter - 0.8));
                rayStart.Add(new MyPoint(from.Rectangle.XCenter - 0.4, from.Rectangle.YCenter + 0.8));
                rayStart.Add(new MyPoint(from.Rectangle.XCenter + 0.4, from.Rectangle.YCenter + 0.8));
            }
            else
            {
                rayStart.Add(new MyPoint(from.Rectangle.XCenter - 0.2, from.Rectangle.YCenter));
                rayStart.Add(new MyPoint(from.Rectangle.XCenter + 0.2, from.Rectangle.YCenter));
            }

            MyUnit teammate = Units.FirstOrDefault(u => u.ID != from.ID && u.Friendly == from.Friendly);
            foreach (MyPoint p1 in rayStart)
            {
                if (blockTeammate && teammate != null && teammate.Rectangle.IsBlocking(p1, target)) return false;
                foreach (MyRectangle wall in Walls)
                {
                    if (wall.IsBlocking(p1, target)) return false;
                }
            }

            return true;
        }

        public double Score()
        {
            return cumulativeScore.Sum() + positionScore.Sum();
        }

        private static int[] dx = { 0, 1, 0, -1 };
        private static int[] dy = { 1, 0, -1, 0 };
        private void FindClosestReachable(MyUnit unit, ref double targetX, ref double targetY)
        {
            for (int spread = 0; ; spread++)
            {
                for (int dir = 0; dir < 4; dir++)
                {
                    int x = (int)targetX + spread * dx[dir];
                    int y = (int)targetY + spread * dy[dir];
                    if (x < 0 || x >= Width || y < 0 || y >= Height) continue;
                    if (Dist[(int)unit.Rectangle.XCenter, (int)unit.Rectangle.YCenter, x, y] < int.MaxValue)
                    {
                        targetX = x;
                        targetY = y;
                        return;
                    }
                }
            }
        }
    }
}
