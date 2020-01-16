using System;
using System.Collections.Generic;
using System.Linq;

public class Region : IEquatable<Region>
{
	public HashSet<Cell> Territory = new HashSet<Cell> ();
	public List<Cell> Border = new List<Cell> ();
	public List<Cell> Path = new List<Cell> ();
	public HashSet<Cell> ConquerRegion = new HashSet<Cell> ();
	public int CompleteTime = 0;
	public string Name;
	public static int[,] Zobrist = new int[Board.SIZE, Board.SIZE];

	static Region ()
	{
		for (int x = 0; x < Board.SIZE; x++) {
			for (int y = 0; y < Board.SIZE; y++) {
				Zobrist [x, y] = Strategy.random.Next ();
			}
		}
	}

	public Region (Board board, Player player, int xMin, int xMax, int yMin, int yMax)
	{
		for (int x = xMin; x <= xMax; x++) {
			for (int y = yMin; y <= yMax; y++) {
				Territory.Add (board.Grid [x, y]);
				if (board.Grid [x, y].Owner != player)
					ConquerRegion.Add (board.Grid [x, y]);
				if (x == xMin || x == xMax || y == yMin || y == yMax)
					Border.Add (board.Grid [x, y]);
			}
		}
		foreach (Cell c in player.Line) {
			Territory.Add (c);
			ConquerRegion.Add (c);
		}
		Name = xMin + "_" + yMin + "_" + (xMax + 1 - xMin) + "_" + (yMax + 1 - yMin);
	}

	public Region (Region region)
	{
		this.Territory = new HashSet<Cell> (region.Territory);
		this.Border = region.Border.ToList ();
		this.Path = region.Path.ToList ();
		this.Name = region.Name;
		this.CompleteTime = region.CompleteTime;
	}

	public void UpdateTurn (Board board, Player player)
	{
		HashSet<Cell> oldTerritory = Territory;
		Territory = new HashSet<Cell> ();
		foreach (Cell t in oldTerritory)
			Territory.Add (board.Grid [t.X, t.Y]);
		for (int i = 0; i < Border.Count; i++)
			Border [i] = board.Grid [Border [i].X, Border [i].Y];
		ConquerRegion = new HashSet<Cell> (Territory.Where (t => t.Owner != player));
	}

	public override string ToString ()
	{
		return Name;
	}

	private List<Cell> FindPath (Cell from, HashSet<Cell> targets, HashSet<Cell> blocked, bool paranoid)
	{
		foreach (Cell c in from.Neighbors) {
			if (targets.Contains (c) && (!paranoid || c.OpponentConquerTime > 1))
				return new List<Cell>{ c };
		}

		int[,] dist = new int[Board.SIZE, Board.SIZE];
		Queue<Cell> queue = new Queue<Cell> ();
		queue.Enqueue (from);
		for (int x = 0; x < Board.SIZE; x++) {
			for (int y = 0; y < Board.SIZE; y++) {
				dist [x, y] = -1;
			}
		}
		dist [from.X, from.Y] = 0;
		while (queue.Count > 0) {
			Cell c = queue.Dequeue ();
			if (targets.Contains (c) && (!paranoid || c.ValidEnd && c.OpponentConquerTime > dist [c.X, c.Y] + 1)) {
				List<Cell> result = new List<Cell> ();
				while (dist [c.X, c.Y] > 0) {
					result.Add (c);
					c = c.Neighbors.First (n => n != null && dist [n.X, n.Y] == dist [c.X, c.Y] - 1);
				}
				result.Reverse ();
				return result;
			}
			foreach (Cell c2 in c.Neighbors) {
				if (c2 == null || blocked.Contains (c2) || dist [c2.X, c2.Y] != -1)
					continue;
				queue.Enqueue (c2);
				dist [c2.X, c2.Y] = dist [c.X, c.Y] + 1;
			}
		}
		return null;
	}

	public void FindPath (Player player)
	{
		Path.Clear ();
		CompleteTime = 0;
		HashSet<Cell> toVisit = new HashSet<Cell> (Border.Where (c => c.Owner != player && c.Line != player));
		Cell current = player.Position;
		HashSet<Cell> blocked = new HashSet<Cell> (player.Line);
		while (toVisit.Count > 0) {
			List<Cell> subPath = FindPath (current, toVisit, blocked, false);
			if (subPath == null) {
				Path.Clear ();
				return;
			}
			CompleteTime += subPath.Count;
			current = subPath.Last ();
			Path.AddRange (subPath);
			toVisit.Remove (current);
			foreach (Cell s in subPath)
				blocked.Add (s);
		}
		if (player.Territory.Count > 0) {
			List<Cell> subPath = FindPath (current, player.Territory, blocked, false);
			if (Path.Count == 0)
				subPath = player.ToBasePath ();
			if (subPath == null || subPath.Count == 0) {
				Path.Clear ();
				return;
			}
			Path.Add (subPath.Last ());
			CompleteTime += subPath.Count;
		}
	}

	public void RegenerateBorder (Player player)
	{
		Border = Territory.Where (t => t.Neighbors.Count (n => Territory.Contains (n)) < 4).ToList ();
		ConquerRegion = new HashSet<Cell> (Territory.Where (t => t.Owner != player));
	}

	public void ExtendPlan (Board board, Player player)
	{
		int failed = 0;
		double bestScore = Score (player);
		if (Path.Count < 2)
			return;
		while (failed < 15 && ConquerRegion.Count < 150) {
			failed++;
			Region region = new Region (this);
			Cell c = Path [Strategy.random.Next (Path.Count - 1)];
			c = c.Neighbors.FirstOrDefault (n => !region.Territory.Contains (n));
			if (c == null)
				continue;
			region.Border.Add (c);
			region.Territory.Add (c);
			region.Path.Add (c);
			bool repeat = true;
			while (repeat) {
				region.Path.Add (player.Position);
				HashSet<Cell> extend = new HashSet<Cell> (region.Path.SelectMany (p => p.Neighbors));
				extend.ExceptWith (region.Territory);
				extend = new HashSet<Cell> (extend.Where (x => x != null && x.Neighbors.Count (n => region.Path.Contains (n)) > 1));
				if (extend.Count == 0)
					repeat = false;
				region.Territory.UnionWith (extend);
				region.RegenerateBorder (player);
				region.FindPath (player);
			}
			region.Path.Remove (player.Position);
			if (!region.IsValid (board, player))
				continue;
			double score = region.Score (player);
			bool canStillBuildOld = region.CompleteTime + region.Path.Last ().ManhattanDist (Path.Last ()) <= CompleteTime && region.ConquerRegion.Count >= this.ConquerRegion.Count;
			if (region.CompleteTime <= board.RemainingTurns && score > bestScore || canStillBuildOld) {
				failed = 0;
				Territory = region.Territory;
				Border = region.Border;
				Path = region.Path;
				CompleteTime = region.CompleteTime;
			}
		}
	}

	public bool IsValid (Board board, Player player)
	{
		if (Path.Count == 0)
			return false;
		if (player.ID == 0 && Path.Last ().OpponentConquerTime <= Path.Count)
			return false;
		if (!Territory.Any (b => b.Owner != player))
			return false;
		Cell next = FindPath (player.Position, new HashSet<Cell>{ Path [0] }, player.Line, false) [0];
		if (player.ID == 0 && player.Position.Owner == player && next.OpponentDist <= 2)
			return false;
		if (player.ID == 0) {
			int[] opponentCutTime = { 1000, 1000, 1000, 1000, 1000, 1000, 1000 };
			for (int t = Path.Count - 1; t >= 0; t--) {
				if (Path [t].Line != null)
					opponentCutTime [Path [t].Line.ID] = t + 1;
			}
			for (int t = 0; t < Path.Count; t++) {
				Player conquerer = Path [t].Conquerer;
				if (conquerer == null)
					continue;
				if (Path [t].OpponentConquerTime == t + 1 && opponentCutTime [conquerer.ID] > t + 1)
					return false; // will get surrounded and eaten
			}
		}
		HashSet<Cell> blocked = new HashSet<Cell> (player.Line);
		blocked.Add (next);
		List<Cell> escapePath = FindPath (next, player.Territory, blocked, player.ID == 0);
		int escapeDist = escapePath == null ? CompleteTime : escapePath.Count + Path [0].PlayerDist [player.ID];
		if (next.Owner == player) {
			escapeDist = 1;
			escapePath = new List<Cell> { next };
		}
		if (player.ID == 0 && escapePath != null && escapePath.Count > 0 && escapeDist >= escapePath.Last ().OpponentDist)
			return false; // deadly collision
		int escapeTime = player.GetTime (escapeDist);
		foreach (Player p in board.Players) {
			if (p == player)
				continue;
			if (next.PlayerDist [p.ID] == 1 || next.Owner != player && next.PlayerDist [p.ID] == 2)
				return false;
			foreach (Cell c in Path.Union(player.Line)) {
				int playerDist = c.PlayerDist [p.ID];
				if (playerDist == -1)
					continue;
				if (p.GetTime (playerDist) < escapeTime)
					return false;
				if (p.GetTime (playerDist) < player.GetTime (CompleteTime) - 3)
					return false;
			}
		}
		if (escapePath != null) {
			foreach (Cell c in escapePath) {
				foreach (Player p in board.Players) {
					if (p == player)
						continue;
					int playerDist = c.PlayerDist [p.ID];
					if (playerDist != -1 && p.GetTime (playerDist) < escapeTime)
						return false;
				}
			}

			if (escapePath.Count > 1) {
				Cell c = escapePath [escapePath.Count - 2];
				foreach (Player p in board.Players) {
					if (p == player)
						continue;
					int playerDist = c.PlayerDist [p.ID];
					if (playerDist == -1)
						continue;
					if (p.GetTime (playerDist) == escapeTime)
						return false;
				}
			}
		}
		return true;
	}

	public double FillPoints (Player player)
	{
		double result = 0;
		foreach (Cell c in Territory) {
			if (c.Owner == null) {
				if (player.ID == 0 && c.OpponentConquerTime != int.MaxValue)
					result += 5; // will be an opponent tile by the time i reach it
				else
					result++;
			} else if (c.Owner != player)
				result += 5 + 0.1 * c.OpponentDist; // include juicy-factor
			
			if (c.Item == null)
				continue;
			if (c.Item == "s")
				result -= 50;
			if (c.Item == "n")
				result += 20;
			if (c.Item == "saw")
				result += 30;			
		}
		return result;
	}

	public double Score (Player player)
	{
		double risky = 20;
		//if (player.ID == 0)
		//	risky = 3 * Math.Max (0, CompleteTime - Path.Min (p => p.OpponentDist)) * Math.Sqrt (CompleteTime);
		double result = (double)FillPoints (player) / Math.Sqrt (CompleteTime + 5 + risky);
		if (player.ID == 0) {
			double regionDist = Path.Last ().OpponentRegionDist;
			double opDist = Path.Last ().OpponentDist;
			if (regionDist < opDist - 2)
				result += player.Position.OpponentRegionDist - regionDist + 5;
			//result += Strategy.random.NextDouble () / 10;
		}
		return result;
	}

	public bool Equals (Region r)
	{
		if (Name.StartsWith ("close_region"))
			return false;
		return this.ConquerRegion.SetEquals (r.ConquerRegion);
	}

	public override int GetHashCode ()
	{
		int result = 0;
		foreach (Cell q in ConquerRegion) {
			result ^= Zobrist [q.X, q.Y];
		}
		return result;
	}
}
