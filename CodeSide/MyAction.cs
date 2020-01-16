using AiCup2019.Model;
using System;
namespace AiCup2019
{
    public enum HorizontalMovement
    {
        RIGHT,
        LEFT,
        STAY,
    }

    public enum VerticalMovement
    {
        UP,
        DOWN,
        STAY
    }

    public class MyAction : IEquatable<MyAction>
    {
        public HorizontalMovement Horizontal;
        public VerticalMovement Vertical;
        public static MyAction Default = new MyAction(HorizontalMovement.STAY, VerticalMovement.STAY);

        public MyAction(HorizontalMovement horizontal, VerticalMovement vertical)
        {
            this.Horizontal = horizontal;
            this.Vertical = vertical;
        }

        public bool Equals(MyAction other)
        {
            return this.Horizontal.Equals(other.Horizontal) && this.Vertical.Equals(other.Vertical);
        }
    }
}
