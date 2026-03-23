using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace WallhackandAimbotCombinedTest
{
    public class Entity
    {
        public string name { get; set; }
        public List<Vector3> bones { get; set; }
        public List<Vector2> bones2d { get; set; }
        public Vector3 origin { get; set; }
        public Vector3 view { get; set; }
        public Vector3 position { get; set; }
        public Vector3 viewOffset { get; set; }
        public Vector3 head { get; set; }
        public Vector2 position2D { get; set; }
        public Vector2 viewPosition2D { get; set; }
        public Vector2 head2d { get; set; }
        public int team { get; set; }
        public float distance { get; set; }
        public float pixelDistance { get; set; }
        public IntPtr pawnAdress { get; set; }
        public IntPtr controllerAdress { get; set; }
        public int health { get; set; }
        public uint lifeState { get; set; }
        public bool spotted { get; set; }
        public bool visible { get; set; }
    }

    public enum BoneIds
    {
        Waist = 0,
        Neck = 5,
        Head = 6,
        ShoulderLeft = 8,
        ForeLeft = 9,
        HandLeft = 11,
        ShoulderRight = 16,
        ForeRight = 14,
        HandRight = 17,
        KneeLeft = 23,
        FeetLeft = 24,
        KneeRight = 26,
        FeetRight = 27

    }
}
