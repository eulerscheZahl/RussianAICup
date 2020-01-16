using System.Collections.Generic;
using System.Linq;
using System.Text;

public class Board
{
	public static int SIZE = 31;
	public Cell[,] Grid = new Cell[SIZE, SIZE];
	public List<Cell> Items = new List<Cell> ();
	public List<Player> Players = new List<Player> ();

	public int RemainingTurns;

	public Board ()
	{
		for (int x = 0; x < SIZE; x++) {
			for (int y = 0; y < SIZE; y++) {
				Grid [x, y] = new Cell (x, y);
			}
		}

		for (int x = 0; x < SIZE; x++) {
			for (int y = 0; y < SIZE; y++) {
				for (int dir = 0; dir < 4; dir++) {
					int x_ = x + Direction.DX [dir];
					int y_ = y + Direction.DY [dir];
					if (x_ < 0 || x_ >= SIZE || y_ < 0 || y_ >= SIZE)
						continue;
					Grid [x, y].Neighbors [dir] = Grid [x_, y_];
				}
			}
		}
	}

	public Player Me () => Players.First (p => p.ID == 0);

	public override string ToString ()
	{
		StringBuilder sb = new StringBuilder ();
		for (int y = 0; y < SIZE; y++) {
			for (int x = 0; x < SIZE; x++) {
				sb.Append (Grid [x, y].PrintCell ());
			}
			sb.AppendLine ();
		}
		return sb.ToString ();
	}
}
