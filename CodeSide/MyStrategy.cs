using AiCup2019.Model;
using System;
using System.Linq;
using System.Collections.Generic;

namespace AiCup2019
{
    public class MyStrategy
    {
        private static Random random = new Random(0);
        private MyGamestate RandomSearch(MyGamestate state, MyGamestate previousPlan, int firstTurnSteps)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            int runs = 0;
            double bestScore = -1e9;
            MyGamestate result = null;
            // reuse previous plan
            if (previousPlan != null)
            {
                runs++;
                MyGamestate prev = new MyGamestate(state);
                for (int unitIndex = 0; unitIndex < prev.Units.Count; unitIndex++)
                {
                    MyUnit partnerUnit = previousPlan.Units.First(u => u.ID == prev.Units[unitIndex].ID);
                    prev.Units[unitIndex].Actions = partnerUnit.Actions.ToArray();
                    if (firstTurnSteps == MyGamestate.FrameSkip)
                    {
                        for (int i = 1; i < prev.Units[unitIndex].Actions.Length; i++)
                        {
                            prev.Units[unitIndex].Actions[i - 1] = prev.Units[unitIndex].Actions[i];
                        }
                    }
                }
                prev.SimPlan(firstTurnSteps);
                bestScore = prev.Score();
                result = prev;
            }

            // find heuristic plan
            MyGamestate depthState = new MyGamestate(state);
            for (int depth = 0; depth < state.Units[0].Actions.Length; depth++)
            {
                int ticks = depth == 0 ? firstTurnSteps : MyGamestate.FrameSkip;
                for (int unitIndex = 0; unitIndex < state.Units.Count; unitIndex++)
                {
                    double tmpBest = int.MinValue;
                    for (int x = 0; x < 3; x++)
                    {
                        for (int y = 0; y < 3; y++)
                        {
                            MyGamestate tmpState = new MyGamestate(depthState);
                            MyAction action = new MyAction((HorizontalMovement)x, (VerticalMovement)y);
                            tmpState.Units[unitIndex].Actions[0] = action;
                            for (int i = 0; i < ticks; i++) tmpState.Step(0, 100);
                            tmpState.ScoreFinal();
                            if (tmpState.Score() > tmpBest)
                            {
                                tmpBest = tmpState.Score();
                                depthState.Units[unitIndex].Actions[depth] = action;
                            }
                        }
                    }
                }
                for (int i = 0; i < ticks; i++) depthState.Step(depth, 100);
            }
            MyGamestate working = new MyGamestate(state);
            for (int i = 0; i < working.Units.Count; i++)
            {
                working.Units[i].Actions = depthState.Units[i].Actions.ToArray();
            }
            working.SimPlan(firstTurnSteps);
            if (working.Score() > bestScore)
            {
                bestScore = depthState.Score();
                result = depthState;
            }

            // find random plan
            List<int> myIndex = Enumerable.Range(0, state.Units.Count).Where(i => state.Units[i].Friendly).ToList();
            int timeLimit = firstTurnSteps == MyGamestate.FrameSkip ? 30 : 10;
            while (sw.ElapsedMilliseconds < timeLimit)
            {
                runs++;
                working = new MyGamestate(state);
                int modify = myIndex[random.Next(myIndex.Count)];
                if (result != null)
                {
                    for (int idx = 0; idx < working.Units.Count; idx++)
                    {
                        if (idx != modify) working.Units[idx].Actions = result.Units[idx].Actions.ToArray();
                    }
                }
                MyUnit unit = working.Units[modify];
                for (int i = 0; i < unit.Actions.Length; i++)
                {
                    if (i > 0 && random.Next(2) == 0) unit.Actions[i] = unit.Actions[i - 1];
                    unit.Actions[i] = new MyAction((HorizontalMovement)random.Next(3), (VerticalMovement)random.Next(3));
                }
                working.SimPlan(firstTurnSteps);
                double score = working.Score();
                if (score > bestScore)
                {
                    bestScore = score;
                    result = working;
                }
            }

            // remove unnecessary jumps
            //for (int depth = 0; depth < result.Units[0].Actions.Length; depth++)
            //{
            //    for (int unitIndex = 0; unitIndex < result.Units.Count; unitIndex++)
            //    {
            //        if (working.Units[unitIndex].Actions[depth].Vertical != VerticalMovement.UP) continue;
            //        working = new MyGamestate(state);
            //        for (int idx = 0; idx < working.Units.Count; idx++)
            //        {
            //            working.Units[idx].Actions = result.Units[idx].Actions.ToArray();
            //        }
            //        working.Units[unitIndex].Actions[depth] = new MyAction(working.Units[unitIndex].Actions[depth].Horizontal, VerticalMovement.STAY);
            //        working.SimPlan(firstTurnSteps);
            //        double score = working.Score();
            //        if (score > bestScore)
            //        {
            //            bestScore = score;
            //            result = working;
            //        }
            //    }
            //}
#if DEBUG
            Debug.Draw(new CustomData.Log(runs + ": " + bestScore));
#endif

            return result;
        }

        private UnitAction Suicide(Game game, Unit unit)
        {
            UnitAction result = new UnitAction();
            if (unit.Mines == 0) return result;
            int x = (int)unit.Position.X;
            int y = (int)unit.Position.Y;
            if (unit.Position.Y - (int)unit.Position.Y > 1e-6) return result;
            if (game.Level.Tiles[x][y - 1] != Tile.Wall && game.Level.Tiles[x][y - 1] != Tile.Platform) return result;
            if (!unit.Weapon.HasValue) return result;
            if (unit.Weapon.Value.FireTimer.HasValue && 60 * unit.Weapon.Value.FireTimer >= 1) return result;
            if (game.LootBoxes.Any(b => Math.Abs(b.Position.X - unit.Position.X) <= 0.25 && Math.Abs(b.Position.Y - unit.Position.Y) <= 1e-6)) return result;
            MyGamestate state = new MyGamestate(game, unit);
            MyUnit myUnit = state.Units.First(u => u.ID == unit.Id);

            result.Aim = new Vec2Double(0, -1);
            int placedMines = game.Mines.Count(m => Math.Abs(unit.Position.X - m.Position.X) < 1e-6 && Math.Abs(unit.Position.Y - m.Position.Y) < 1e-6);

            double explosionSize = 3;
            MyRectangle conservativeExplosion = new MyRectangle(myUnit.Rectangle.XCenter - explosionSize - 1.0 / 6, myUnit.Rectangle.Y + 0.25 - 1.0 / 6 - explosionSize, 2 * explosionSize - 2.0 / 6, 2 * explosionSize - 3.0 / 6);
            MyRectangle realExplosion = new MyRectangle(myUnit.Rectangle.XCenter - explosionSize, myUnit.Rectangle.Y + 0.25 - explosionSize, 2 * explosionSize, 2 * explosionSize);
            List<MyUnit> opponentLaterHit = MyGamestate.opponents.Where(o => o.Rectangle.Collide(conservativeExplosion)).ToList();
            List<MyUnit> opponentNowHit = MyGamestate.opponents.Where(o => o.Rectangle.Collide(conservativeExplosion)).ToList();
            List<MyUnit> meHit = state.Units.Where(o => o.Rectangle.Collide(realExplosion)).ToList();
            List<MyUnit> opponentHit = opponentLaterHit;
            if (!unit.Weapon.Value.FireTimer.HasValue && (placedMines > 0 || opponentNowHit.All(o => o.Health <= 50))) opponentHit = opponentNowHit;
            if (opponentNowHit.Count == 0) return result;

            bool winningMove = true;
            int myScore = game.Players.First(p => p.Id == unit.PlayerId).Score % 1000;
            int oppScore = game.Players.First(p => p.Id != unit.PlayerId).Score % 1000;
            myScore += opponentHit.Sum(u => u.Health);
            if (myScore <= oppScore) winningMove = false;
            int neededMines = opponentHit.Max(p => p.Health) > 50 ? 2 : 1;
            if (unit.Mines + placedMines < neededMines) winningMove = false;
            if (opponentHit.Count < MyGamestate.opponents.Count) winningMove = false;

            if (winningMove)
            {
                placedMines++;
                result.PlantMine = true;
                result.Shoot = placedMines >= neededMines;
                return result;
            }

            if (meHit.Count == 1 && meHit[0].Health > 50 && meHit[0].GunType != MyWeaponType.ROCKET && opponentHit.Any(o => o.Health <= 50))
            { // kill opponent with explosion while surviving
                result.PlantMine = true;
                result.Shoot = true;
                return result;
            }

            if (meHit.Count < state.Units.Count && unit.Mines + placedMines >= neededMines)
            { // 1:1 trade
                placedMines++;
                result.PlantMine = true;
                result.Shoot = placedMines >= neededMines;
                return result;
            }

            return result;
        }

        public static Debug Debug;
        private static MyGamestate previousPlan;
        private static int lastSimTick = -1;
        public UnitAction GetAction(Unit unit, Game game, Debug debug)
        {
            MyStrategy.Debug = debug;
#if DEBUG
            if (game.CurrentTick < 130) return new UnitAction();
#endif
            UnitAction suicide = Suicide(game, unit);
            if (suicide.PlantMine)
                return suicide;

            if (game.CurrentTick == lastSimTick)
            {
                return GetAction(game, previousPlan, unit);
            }
            lastSimTick = game.CurrentTick;

            MyGamestate state = new MyGamestate(game, unit);

            int firstTurnSteps = MyGamestate.FrameSkip - game.CurrentTick % MyGamestate.FrameSkip;
            if (firstTurnSteps == MyGamestate.FrameSkip) state.UpdateFightLocations();
            MyGamestate best = RandomSearch(state, previousPlan, firstTurnSteps);
            previousPlan = best;

            // just for debug drawing
#if DEBUG
            foreach (MyUnit u in state.Units.Where(u => u.Friendly))
            {
                u.Actions = best.Units.First(un => u.ID == un.ID).Actions;
            }
            for (int depth = 0; depth < state.Units.First(u => u.Friendly).Actions.Length; depth++)
            {
                int steps = depth == 0 ? firstTurnSteps : MyGamestate.FrameSkip;
                for (int ticks = 0; ticks < steps; ticks++)
                {
                    foreach (MyUnit u in state.Units.Where(u => u.Friendly))
                    {
                        state.Step(depth, 0);
                        u.Rectangle.Draw(new ColorFloat(unit.Id == u.ID ? 0 : 1, unit.Id == u.ID ? 1 : 0, 1, 1 - 0.15f * depth));
                        float x0 = (float)u.Rectangle.XCenter;
                        float y0 = (float)u.Rectangle.YCenter;
                        float x1 = x0;
                        float y1 = y0;
                        if (u.Actions[depth].Horizontal == HorizontalMovement.RIGHT) x1 += 0.4f;
                        if (u.Actions[depth].Horizontal == HorizontalMovement.LEFT) x1 -= 0.4f;
                        if (u.Actions[depth].Vertical == VerticalMovement.UP) y1 += 0.4f;
                        if (u.Actions[depth].Vertical == VerticalMovement.DOWN) y1 -= 0.4f;
                        debug.Draw(new CustomData.Line(new Vec2Float(x0, y0), new Vec2Float(x1, y1), 0.05f, new ColorFloat(1, 0, 0, 1)));
                    }
                }
            }
#endif

            return GetAction(game, best, unit);
        }

        private static UnitAction GetAction(Game game, MyGamestate actionState, Unit gameUnit)
        {
            MyUnit unit = actionState.Units.First(u => u.ID == gameUnit.Id);
            UnitAction result = unit.GetGameAction();

            MyGamestate initialState = new MyGamestate(game, game.Units.First(u => u.Id == gameUnit.Id));
            unit = initialState.Units.First(u => u.ID == gameUnit.Id);
            List<MyUnit> opponents = game.Units.Select(u => new MyUnit(u, u.PlayerId == gameUnit.PlayerId)).Where(u => !u.Friendly).ToList();
            opponents = opponents.OrderByDescending(o => 0.1 * unit.Health + unit.Dist(o.Rectangle.XCenter, o.Rectangle.YCenter)).ToList();
            List<MyUnit>[] predict = new List<MyUnit>[50];
            predict[0] = opponents.Select(o => new MyUnit(o)).ToList();
            for (int depth = 1; depth < predict.Length; depth++)
            {
                predict[depth] = predict[depth - 1].Select(p => new MyUnit(p)).ToList();
                foreach (MyUnit pred in predict[depth])
                {
                    pred.InitMove(0);
                    MyCollision coll = pred.NextCollision(0, null);
                    double time = 1;
                    if (coll != null) time = Math.Min(time, coll.CollisionTime);
                    pred.Rectangle.Move(time);
                }
            }


            // aiming
            result.Aim = new Vec2Double(opponents.Last().Rectangle.XCenter - unit.Rectangle.XCenter, opponents.Last().Rectangle.YCenter - unit.Rectangle.YCenter);
            if (gameUnit.Weapon.HasValue && gameUnit.Weapon.Value.LastAngle.HasValue)
            {
                double bulletSpeed = gameUnit.Weapon.Value.Parameters.Bullet.Speed / 60;
                for (int oppIndex = 0; oppIndex < opponents.Count; oppIndex++)
                {
                    for (int depth = 0; depth < 50; depth++)
                    {
                        MyUnit opp = predict[depth][oppIndex];
                        double dx = unit.Rectangle.XCenter - opp.Rectangle.XCenter;
                        double dy = unit.Rectangle.YCenter - opp.Rectangle.YCenter;
                        double dist = Math.Sqrt(dx * dx + dy * dy);
                        if (initialState.CanShoot(unit, opp, true))
                        {
                            result.Aim = new Vec2Double(opp.Rectangle.XCenter - unit.Rectangle.XCenter, opp.Rectangle.YCenter - unit.Rectangle.YCenter);
                            result.Shoot = true;
                        }
                        else break;
                        if (dist <= bulletSpeed * (depth + 3)) break;
                    }
                }
                double angle = Math.Atan2(result.Aim.Y, result.Aim.X);
                double oldAngle = gameUnit.Weapon.Value.LastAngle.Value;
                double correctAngle = 3 * Math.PI / 180;
                if (!unit.Ammo || !result.Shoot) correctAngle *= 180;
                if (Math.Abs(angle - oldAngle) < correctAngle)
                {
                    angle = oldAngle;
#if DEBUG
                    Debug.Draw(new CustomData.Log("keep angle"));
#endif
                }
                result.Aim = new Vec2Double(1000 * Math.Cos(angle), 1000 * Math.Sin(angle));
#if DEBUG
                Debug.Draw(new CustomData.Line(
                new Vec2Float((float)unit.Rectangle.XCenter, (float)unit.Rectangle.YCenter),
                    new Vec2Float((float)(unit.Rectangle.XCenter + result.Aim.X), (float)(unit.Rectangle.YCenter + result.Aim.Y)),
                0.05f, new ColorFloat(1, 0, 0, 1)));
#endif
            }

            // weapon swap
            MyWeaponType closestWeapon = MyGamestate.Boxes.Where(b => b.Weapon != MyWeaponType.NONE).OrderBy(b => unit.Dist(b.Rectangle.XCenter, b.Rectangle.YCenter)).First().Weapon;
            List<MyWeaponType> weaponPriority = new List<MyWeaponType> { MyWeaponType.PISTOL, MyWeaponType.RIFLE, MyWeaponType.ROCKET, MyWeaponType.NONE };
            result.SwapWeapon = weaponPriority.IndexOf(unit.GunType) > weaponPriority.IndexOf(closestWeapon);

            return result;
        }
    }
}