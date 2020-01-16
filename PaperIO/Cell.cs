using System;
using System.Collections.Generic;
using System.Linq;

public class Cell
{
	public int X, Y;
	public Player Owner;
	public Player Line;
	public Cell[] Neighbors = new Cell[4];
	public int[] PlayerDist;
	public int MeDist;
	public int OpponentDist;
	public int OpponentRegionDist;
	public int OpponentConquerTime = int.MaxValue;
	public Player Conquerer;
	public string Item;
	public bool ValidEnd = false;

	public Cell (int x, int y)
	{
		this.X = x;
		this.Y = y;
		PlayerDist = new int[] { -1, -1, -1, -1, -1, -1, -1 };
	}

	public void MarkValidEnd() {
		if (ValidEnd == true)
			return;
		
		if (Owner != null && Owner.ID == 0) {
			ValidEnd = true;
			foreach (Cell c in Neighbors) {
				if (c != null)
					c.MarkValidEnd ();
			}
		}
	}

	public void FinalizeDistances (Board board)
	{
		MeDist = PlayerDist [0];
		OpponentDist = int.MaxValue;
		OpponentRegionDist = int.MaxValue;
		for (int i = 1; i < PlayerDist.Length; i++) {
			if (PlayerDist [i] != -1) {
				OpponentDist = Math.Min (OpponentDist, PlayerDist [i]);
			}
		}
		foreach (Player p in board.Players) {
			if (p == board.Me () || p.Territory.Count == 0)
				continue;
			OpponentRegionDist = Math.Min (OpponentRegionDist, p.Territory.Min (t => ManhattanDist (t)));
		}
	}

	public int ManhattanDist (Cell c)
	{
		return Math.Abs (X - c.X) + Math.Abs (Y - c.Y);
	}

	public override string ToString ()
	{
		return X + "/" + Y + ": " + PrintCell ();
	}

	public string PrintCell ()
	{
		if (Line != null)
			return ((char)('A' + Line.ID)).ToString ();
		if (Owner != null)
			return ((char)('a' + Owner.ID)).ToString ();
		return ".";
	}
}