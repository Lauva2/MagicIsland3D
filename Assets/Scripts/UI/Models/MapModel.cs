using System.Collections.Generic;
using UnityEngine;
namespace MagicIsland.UI.Models
{
    public sealed class MapLocationModel
    {
        public string Id, Name, Description;
        public Vector3 WorldPosition;
        public Vector2 Position;
    }
    public sealed class MapModel
    {
        public readonly List<MapLocationModel> Locations = new List<MapLocationModel>();
        public Vector4[] Roads = new Vector4[0];
        public int FarmSize, Selected;
        public Vector2 PlayerPosition;
        public string Region, DetailTitle, DetailText;
    }
}
