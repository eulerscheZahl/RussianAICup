using System;
using System.Collections.Generic;
using System.Linq;

public class Player
{
	public int ID;
	public Cell Position;
	public Cell Previous;
	public HashSet<Cell> Territory = new HashSet<Cell> ();
	public HashSet<Cell> Line = new HashSet<Cell> ();
	public int Nitro = 0;
	public int Slow = 0;

	public Player (int id)
	{
		this.ID = id;
	}

	public void SetDirection (string dir)
	{
		if (dir == "up")
			Previous = Position.Neighbors [(int)Direction.Dir.DOWN];
		if (dir == "down")
			Previous = Position.Neighbors [(int)Direction.Dir.UP];
		if (dir == "right")
			Previous = Position.Neighbors [(int)Direction.Dir.LEFT];
		if (dir == "left")
			Previous = Position.Neighbors [(int)Direction.Dir.RIGHT];
	}

	public IEnumerable<Cell> ValidMoves ()
	{
		foreach (Cell c in Position.Neighbors) {
			if (c == null || c == Previous || Line.Contains (c))
				continue;
			yield return c;
		}
	}

	public void MoveTo (Cell cell)
	{
		Previous = Position;
		Position = cell;
		Line.Add (cell);
	}

	public void ComputeDistances (Board board, bool detectCrashed)
	{
		if (Previous == null && detectCrashed) {
			board.Grid [Position.X, Position.Y].PlayerDist [ID] = 0;
			return;
		}
		int[,] dist = BFS (Position, true);
		for (int x = 0; x < Board.SIZE; x++) {
			for (int y = 0; y < Board.SIZE; y++) {
				board.Grid [x, y].PlayerDist [ID] = dist [x, y];
			}
		}
	}

	public Cell ClosestOwnCell ()
	{
		Cell result = null;
		foreach (Cell c in Territory) {
			if (c.PlayerDist [ID] != -1 && (result == null || c.PlayerDist [ID] < result.PlayerDist [ID]))
				result = c;
		}
		return result;
	}

	public int ToBaseTime ()
	{
		Cell cell = ClosestOwnCell ();
		if (cell == null)
			return 0;
		return cell.PlayerDist [ID];
	}

	public List<Cell> ToBasePath ()
	{
		return ToCellPath (ClosestOwnCell ());
	}

	public List<Cell> ToCellPath (Cell cell)
	{
		if (cell == null)
			return null;
		List<Cell> result = new List<Cell> ();
		while (cell.PlayerDist [ID] > 0) {
			result.Add (cell);
			cell = cell.Neighbors.First (n => n != null && n.PlayerDist [ID] == cell.PlayerDist [ID] - 1);
		}
		return result;
	}

	private int[,] BFS (Cell from, bool checkValid)
	{
		int[,] result = new int[Board.SIZE, Board.SIZE];
		for (int x = 0; x < Board.SIZE; x++) {
			for (int y = 0; y < Board.SIZE; y++) {
				result [x, y] = -1;
			}
		}
		Queue<Cell> queue = new Queue<Cell> ();
		queue.Enqueue (from);
		result [from.X, from.Y] = 0;
		while (queue.Count > 0) {
			Cell current = queue.Dequeue ();
			foreach (Cell next in current.Neighbors) {
				if (next == null || (ID == 0 && Line.Contains (next)) || result [next.X, next.Y] != -1)
					continue; // only block line for own player, opponent might complete a region
				if (checkValid && current == Position && next == Previous)
					continue;
				result [next.X, next.Y] = 1 + result [current.X, current.Y];
				queue.Enqueue (next);
			}
		}
		return result;
	}

	public int GetTime (int dist)
	{
		double result = dist;
		if (Slow > 0) {
			int slowTime = Math.Min (dist, Slow);
			int normalTime = dist - slowTime;
			result = 5.0 / 3.0 * slowTime + normalTime;
		} else if (Nitro > 0) {
			int fastTime = Math.Min (dist, Nitro);
			int normalTime = dist - fastTime;
			result = 5.0 / 6.0 * fastTime + normalTime;
		}

		if (ID == 0)
			return (int)Math.Ceiling (result);
		return (int)Math.Floor (result);
	}

	public string MoveAction (Cell to)
	{
		int[,] dist = BFS (to, false);
		List<Cell> candidates = ValidMoves ().Where (m => dist [m.X, m.Y] != -1).ToList ();
		candidates = candidates.Where (c => dist [c.X, c.Y] == candidates.Min (d => dist [d.X, d.Y])).ToList ();
		if (candidates [0] != to)
			candidates = candidates.Where (c => c.Neighbors.Any (n => n != null && n != Position && dist [n.X, n.Y] != -1 && dist [n.X, n.Y] < dist [c.X, c.Y])).ToList ();
		Cell next = candidates.OrderByDescending (c => c.OpponentDist).First ();
		return Direction.GetDirection (Position, next);
	}
}