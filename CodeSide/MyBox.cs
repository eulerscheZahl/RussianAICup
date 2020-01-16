using System;
namespace AiCup2019
{
    public class MyBox
    {
        public MyRectangle Rectangle;
        public bool Heal;
        public bool Mine;
        public MyWeaponType Weapon = MyWeaponType.NONE;


        public MyBox(MyRectangle rectangle)
        {
            this.Rectangle = rectangle;
        }
    }
}
