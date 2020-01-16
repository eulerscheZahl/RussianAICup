//#undef DEBUG

using System;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.Diagnostics;

class Strategy
{
	public static Random random = new Random ();

	public static void Main (string[] args)
	{
		string input = Console.ReadLine (); // start_game
		while (true) {
			input = Console.ReadLine ();
			if (input.Contains ("end_game"))
				return;
			if (!input.Contains ("\"i\""))
				input = input.Replace ("\"1\"", "\"i\"");
			Board board = ReadInput (input);

			Player me = board.Me ();
			// dirty hack
			if (me.Position.Owner != me && me.Position.Line != me) {
				me.Line.Add (me.Position);
				me.Position.Line = me;
			}
			foreach (Cell c in board.Grid)
				c.FinalizeDistances (board);
			foreach (Cell c in (me.Line.Count == 0 ? me.Position : me.Line.First()).Neighbors) {
				if (c != null)
					c.MarkValidEnd ();
			}

			//while (true) { // performance testing
			#if DEBUG
			Stopwatch sw = Stopwatch.StartNew ();
			#endif
			PredictOpponent (board);
			#if DEBUG
			for (int y = 0; y < Board.SIZE; y++) {
				for (int x = 0; x < Board.SIZE; x++) {
					int conquer = board.Grid [x, y].OpponentConquerTime;
					if (conquer < 10)
						Console.Error.Write (conquer);
					else
						Console.Error.Write (".");
					//if (board.Grid [x, y].ValidEnd)
					//	Console.Error.Write ("#");
					//else
					//	Console.Error.Write (".");
				}
				Console.Error.WriteLine ();
			}
			#endif
			bool tmp = KillOpponent (board) || PlayTurn (board) || SurviveMove (board);
			#if DEBUG
			Console.Error.WriteLine (sw.ElapsedMilliseconds);
			#endif
			//}
		}
	}

	private static void PredictOpponent (Board board)
	{
		foreach (Player p in board.Players) {
			if (p.ID == 0 || p.Position.Owner == p)
				continue;
			List<Region> finalize = EndRegion (board, p, 0).ToList ();
			foreach (Region r in finalize) {
				r.FindPath (p);
			}
			finalize = finalize.Where (c => c.CompleteTime <= board.RemainingTurns && c.IsValid (board, p)).ToList ();
			if (finalize.Count == 0)
				continue;

			List<Region> orderedCandidates = finalize.OrderByDescending (c => c.Score (p)).ToList ();
			Region toFill = orderedCandidates [0];
			foreach (Cell c in toFill.ConquerRegion) {
				c.OpponentConquerTime = Math.Min (c.OpponentConquerTime, toFill.CompleteTime);
				if (c.OpponentConquerTime == toFill.CompleteTime)
					c.Conquerer = p;
			}
		}
	}

	private static bool KillOpponent (Board board)
	{
		Player me = board.Me ();
		List<Cell> line = board.Players.Where (p => p != me).SelectMany (p => p.Line).ToList ();
		int[] playerToBase = new int[7];
		foreach (Player p in board.Players) {
			playerToBase [p.ID] = p.ToBaseTime ();
			if (p != me && me.Line.Count < p.Line.Count && me.ValidMoves ().Contains (p.Position)) {
				string action = me.MoveAction (p.Position);
				#if DEBUG
				Console.Error.WriteLine ("KillOpponent: {{\"command\": \"{0}\"}}", action);
				#endif
				Console.WriteLine ("{{\"command\": \"{0}\"}}", action);
				return true;
			}
		}
		foreach (Cell path in line.OrderBy(l => l.PlayerDist[me.ID])) {
			if (!path.Neighbors.Any (n => n != null && n.Owner == me))
				continue; // coward, TODO
			int toBase = playerToBase [path.Line.ID];
			if (path.PlayerDist [me.ID] >= toBase)
				break;
			if (path.PlayerDist [me.ID] == -1 || !CanReturn (me, path))
				continue;
			bool enemyCanHitMe = me.Line.Any (l => l.PlayerDist [path.Line.ID] <= path.PlayerDist [me.ID]); 
			if (enemyCanHitMe)
				continue;
			// TODO: check for line length on direct collision?
			string action = me.MoveAction (path);
			#if DEBUG
			Console.Error.WriteLine ("KillOpponent: {{\"command\": \"{0}\"}}", action);
			#endif
			Console.WriteLine ("{{\"command\": \"{0}\"}}", action);
			return true;
		}
		return false;
	}

	private static Region previousPlan = null;

	private static bool PlayTurn (Board board)
	{
		Player me = board.Me ();
		HashSet<Region> candidates = new HashSet<Region> ();
		if (previousPlan != null) {
			previousPlan.UpdateTurn (board, me);
			candidates.Add (previousPlan);
		}
		if (me.Position.Owner != me) {
			foreach (Region region in EndRegion(board, me, 3))
				candidates.Add (region);
		}
		foreach (Region region in CreateRegions (board, me, 8))
			candidates.Add (region);
		foreach (Region r in candidates) {
			r.FindPath (me);
		}

		candidates = new HashSet<Region> (candidates.Where (c => c.CompleteTime <= board.RemainingTurns && c.IsValid (board, me)));
		if (candidates.Count == 0)
			return false;

		List<Region> orderedCandidates = candidates.OrderByDescending (c => c.Score (me)).ToList ();
		Region toFill = orderedCandidates [0];
		toFill.IsValid (board, me);
		Cell toCapture = toFill.Path [0];
		if (!CanReturn (me, toCapture))// || toFill.Score (me) < 1)
			return false;
		toFill.ExtendPlan (board, me);
		toFill.IsValid (board, me);
		double score = toFill.Score (me);
		previousPlan = toFill;
		toCapture = toFill.Path [0];
		string action = me.MoveAction (toCapture);
		#if DEBUG
		Console.Error.WriteLine ("PlayTurn: {{\"command\": \"{0}\"}}   " + toFill.Name + ": " + score, action);
		#endif
		Console.WriteLine ("{{\"command\": \"{0}\"}}", action);
		return true;
	}

	private static IEnumerable<Region> EndRegion (Board board, Player player, int delta)
	{
		int maxDist = player.ToBaseTime () + delta;
		foreach (Cell own in player.Territory) {
			if (own.Neighbors.Any (n => n != null && n.Owner == player && n.PlayerDist [player.ID] == own.PlayerDist [player.ID] - 1))
				continue;
			List<Cell> path = player.ToCellPath (own);
			if (path == null || path.Count > maxDist)
				continue;
			Region region = new Region (board, player, player.Position.X, player.Position.X, player.Position.Y, player.Position.Y);
			region.Territory = new HashSet<Cell> (player.Territory.Union (player.Line).Union (path));
			region.Name = "close_region_" + own.X + "_" + own.Y;
			region.Path = path;
			bool[,] visited = new bool[Board.SIZE, Board.SIZE];
			foreach (Cell c in region.Territory)
				visited [c.X, c.Y] = true;
			for (int x = 0; x < Board.SIZE; x++) {
				for (int y = 0; y < Board.SIZE; y++) {
					if (visited [x, y])
						continue;
					HashSet<Cell> component = new HashSet<Cell> { board.Grid [x, y] };
					Queue<Cell> queue = new Queue<Cell> ();
					queue.Enqueue (board.Grid [x, y]);
					visited [x, y] = true;
					while (queue.Count > 0) {
						Cell c = queue.Dequeue ();
						foreach (Cell n in c.Neighbors) {
							if (n == null || visited [n.X, n.Y])
								continue;
							visited [n.X, n.Y] = true;
							component.Add (n);
							queue.Enqueue (n);
						}
					}
					if (component.All (comp => comp.Neighbors.All (n => n != null && (component.Contains (n) || region.Territory.Contains (n))))) {
						region.Territory.UnionWith (component);
					}
				}
			}
			region.RegenerateBorder (player);
			yield return region;
		}
	}

	private static bool CanReturn (Player player, Cell c)
	{
		HashSet<Cell> line = new HashSet<Cell> (player.Line);
		line.Add (c);
		Queue<Cell> queue = new Queue<Cell> ();
		queue.Enqueue (c);
		HashSet<Cell> visited = new HashSet<Cell> { c };
		while (queue.Count > 0) {
			Cell current = queue.Dequeue ();
			if (current.Owner == player)
				return true;
			foreach (Cell n in current.Neighbors) {
				if (n == null || line.Contains (n) || visited.Contains (n))
					continue;
				queue.Enqueue (n);
				visited.Add (n);
			}
		}
		return false;
	}

	private static IEnumerable<Region> CreateRegions (Board board, Player player, int size)
	{
		for (int width = 2; width <= size; width++) {
			for (int height = 2; height <= size; height++) {
				for (int xMin = player.Position.X - width; xMin <= player.Position.X; xMin++) {
					for (int yMin = player.Position.Y - height; yMin <= player.Position.Y; yMin++) {
						int xMax = xMin + width, yMax = yMin + height;
						if (xMin < 0 || yMin < 0 || xMax >= Board.SIZE || yMax >= Board.SIZE)
							continue;
						yield return new Region (board, player, xMin, xMax, yMin, yMax);
					}
				}
			}
		}
	}

	private static Board ReadInput (string json)
	{
		#if DEBUG
		Console.Error.WriteLine (json);
		#endif
		Board board = new Board ();
		dynamic input = JsonConvert.DeserializeObject (json);
		int tick = (int)input ["params"] ["tick_num"].Value;
		board.RemainingTurns = (2499 - tick) / 6;
		foreach (dynamic playerInput in input["params"]["players"]) {
			string name = playerInput.Name;
			int id = 0;
			int.TryParse (name, out id);
			Player player = new Player (id);
			board.Players.Add (player);
			dynamic pos = playerInput.Value ["position"];
			int posX = pos [0], posY = pos [1];
			int oldX = posX, oldY = posY;
			dynamic dir = playerInput.Value ["direction"].Value;
			if (dir == "up" && posY % 30 > 15)
				posY += 30;
			if (dir == "down" && posY % 30 < 15)
				posY -= 30;
			if (dir == "right" && posX % 30 > 15)
				posX += 30;
			if (dir == "left" && posX % 30 < 15)
				posX -= 30;			
			player.Position = board.Grid [posX / 30, Board.SIZE - 1 - posY / 30];
			if (dir != null)
				player.SetDirection (dir.ToString ());

			foreach (dynamic line in playerInput.Value["lines"]) {
				Cell cell = board.Grid [line [0] / 30, Board.SIZE - 1 - (int)line [1] / 30];
				cell.Line = player;
				player.Line.Add (cell);
			}
			foreach (dynamic line in playerInput.Value["territory"]) {
				Cell cell = board.Grid [line [0] / 30, Board.SIZE - 1 - (int)line [1] / 30];
				cell.Owner = player;
				player.Territory.Add (cell);
			}
			foreach (dynamic b in playerInput.Value["bonuses"]) {
				string type = b ["type"];
				int ticks = b ["ticks"];
				if (type == "s")
					player.Slow = ticks;
				if (type == "n")
					player.Nitro = ticks;
			}

			player.ComputeDistances (board, tick > 1);
			board.Grid [oldX / 30, Board.SIZE - 1 - oldY / 30].PlayerDist [player.ID] = 0; // opponent blocks 2 cells while moving
		}
		foreach (dynamic b in input["params"]["bonuses"]) {
			dynamic pos = b ["position"];
			Cell position = board.Grid [pos [0] / 30, Board.SIZE - 1 - (int)pos [1] / 30];
			board.Items.Add (position);
			position.Item = b ["type"];
		}
		#if DEBUG
		Console.Error.WriteLine (board);
		#endif
		return board;
	}

	private static bool SurviveMove (Board board)
	{
		Player me = board.Players.Find (p => p.ID == 0);

		List<Cell> path = BuildPath (me);
		Cell target = path [0];

		if (me.Position.Owner == me) {
			if (me.ValidMoves ().Any (v => v.OpponentDist != -1 && v.OpponentDist <= 2)) {
				target = me.ValidMoves ().OrderByDescending (v => v.OpponentDist).ThenBy (v => v.MeDist).First ();
			} else {
				foreach (Cell c in board.Grid) {
					if (c.Owner != me && c.PlayerDist [me.ID] > 0 && (target.Owner == me || target.PlayerDist [me.ID] > c.PlayerDist [me.ID]))
						target = c;
				}
			}
		} else {
			List<Region> closing = EndRegion (board, me, 0).ToList ();
			Region plan = closing.OrderBy (p => p.Path.Last ().OpponentDist).Last ();
			target = plan.Path.First ();
		}

		string action = me.MoveAction (target);
		if (action == "NONE")
			action = "up";
		#if DEBUG
		Console.Error.WriteLine ("SurviveMove: {{\"command\": \"{0}\"}}", action);
		#endif
		Console.WriteLine ("{{\"command\": \"{0}\"}}", action);
		return true;
	}

	private static List<Cell> BuildPath (Player player)
	{
		HashSet<Cell> initialLine = new HashSet<Cell> (player.Line);
		Cell pos = player.Position;
		Cell prev = player.Previous;
		int tries = 0;
		while (true) {
			tries++;
			List<Cell> plan = new List<Cell> ();
			bool success = true;
			while (true) {
				List<Cell> candidates = player.ValidMoves ().ToList ();
				if (candidates.Count == 0) {
					success = false;
					break;
				}
				Cell c = candidates [random.Next (candidates.Count)];
				plan.Add (c);
				player.MoveTo (c);
				if (c.Owner == player)
					break;
			}

			player.Position = pos;
			player.Previous = prev;
			player.Line = new HashSet<Cell> (initialLine);

			if (success)
				return plan;
		}
	}
}