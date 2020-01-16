public class Direction
{
	public enum Dir
	{
		UP,
		RIGHT,
		DOWN,
		LEFT,
		NONE
	}

	public static int[] DX = { 0, 1, 0, -1 };
	public static int[] DY = { -1, 0, 1, 0 };

	public static string GetDirection (Cell from, Cell to)
	{
		for (int i = 0; i < from.Neighbors.Length; i++) {
			if (from.Neighbors [i] == to)
				return ((Dir)i).ToString ().ToLower ();
		}
		return Dir.NONE.ToString ();
	}
}