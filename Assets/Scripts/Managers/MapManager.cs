using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.UI.Models;
using UnityEngine;
namespace MagicIsland.Managers
{
    public sealed class MapManager : MonoBehaviour
    {
        public MapModel Model { get; } = new MapModel();
        public bool Ready => game != null && game.Players.Player != null && Model.Locations.Count > 0;
        private GameManager game;
        private string selectedId = "farm";
        public static Vector2 Project(Vector3 point) => new Vector2((float)MapProjection.Normalize(point.x), (float)MapProjection.Normalize(point.z));
        public void Initialize(GameManager manager) { Clear(); game = manager; Capture(); }
        public void Capture()
        {
            if (game == null) return;
            var g = game; Model.Locations.Clear(); Model.FarmSize = g.Farm.State.Size;
            Add("farm", "农田", g.Farm.Grid.transform.position, "翻土、播种、浇水与收获。\n作物浇水后在睡觉跨日时成长。");
            Add("home", "我的小屋", g.Housing.Entrance.Position, "从门口进入小屋，摆放家具。\n每天在床边睡觉结算并保存进度。");
            Add("shop", "岛屿商店", g.Shop.Counter.position, "购买种子、出售作物和鱼获。\n也可购买养殖用品和升级背包。");
            Add("orders", "办事处公告栏", g.Orders.Board.transform.position, "查看并交付今日订单。\n办事处还可办理小屋扩建与鸡舍建造。");
            Add("workbench", "工具工作台", g.ToolUpgrades.Workbench.transform.position, "制作装饰、家具、肥料和饲料。\n切换工具升级，强化斧头与镐子。");
            Add("kitchen", "料理台", g.Cooking.Station.transform.position, "用作物、鱼或鸡蛋制作料理。\n食物可恢复体力，也可用作礼物。");
            Add("storage", "储物箱", g.Storage.Chest.transform.position, "存放种子、材料和料理。\n可整组存取、拖拽和快捷转移。");
            Add("shipping", "出货箱", g.Shipping.Box.transform.position, "投入准备出售的物品。\n睡觉时结算收入并保存进度。");
            AddGroup("forest", "橡树林", true, "选择斧头，走近树木逐次点击。\n采完获得木材，等待原定日期恢复。");
            AddGroup("rocks", "采石区", false, "选择镐子，走近岩石逐次点击。\n采完获得石料，等待原定日期恢复。");
            Add("fishing", "海岸钓鱼点", g.Fishing.Spot.transform.position, "选择鱼竿，在码头附近抛竿。\n等待咬钩，及时收竿获取鱼获。");
            Add("coop", g.Animals.Coop == null ? "鸡舍预留地" : "鸡舍", g.Animals.Coop == null ? g.Configuration.Animals.Position : g.Animals.Coop.transform.position,
                g.Animals.Coop == null ? "前往办事处建造鸡舍。\n建成后可购买小鸡、喂食和收取鸡蛋。" : "照顾小鸡、喂食、摸摸和收取鸡蛋。\n养殖用品可在商店购买。");
            var paths = g.Terrain.Terrain.Paths;
            Model.Roads = paths == null ? new Vector4[0] : paths.Segments.Where(t => t.gameObject.activeInHierarchy).Select(t =>
            {
                var half = t.forward * t.lossyScale.z * .5f; var a = Project(t.position - half); var b = Project(t.position + half);
                return new Vector4(a.x, a.y, b.x, b.y);
            }).ToArray();
            Model.Selected = Mathf.Max(0, Model.Locations.FindIndex(p => p.Id == selectedId)); RefreshPlayer();
        }
        private void Add(string id, string name, Vector3 point, string description)
        { Model.Locations.Add(new MapLocationModel { Id = id, Name = name, Description = description, WorldPosition = point, Position = Project(point) }); }
        private void AddGroup(string id, string name, bool trees, string description)
        {
            var indices = Enumerable.Range(0, game.Gathering.Nodes.Count).Where(i => (game.Gathering.State.Definitions[i].Tool == Farming.FarmTool.Axe) == trees).ToArray();
            if (indices.Length == 0) return;
            Vector3 position = Vector3.zero; foreach (int index in indices) position += game.Gathering.Nodes[index].transform.position;
            Add(id, name, position / indices.Length, description);
        }
        public bool Select(int index)
        {
            if (!Ready || index < 0 || index >= Model.Locations.Count) return false;
            Model.Selected = index; selectedId = Model.Locations[index].Id; RefreshPlayer(); return true;
        }
        public void RefreshPlayer()
        {
            if (!Ready) return;
            bool inside = game.Housing.Inside; Vector3 point = inside ? game.Housing.Entrance.Position : game.Players.Player.transform.position;
            Model.PlayerPosition = Project(point); Model.Region = inside ? "当前位置：我的小屋（室内）" : "当前位置：岛屿";
            var selected = Model.Locations[Model.Selected]; float distance = Vector3.ProjectOnPlane(selected.WorldPosition - point, Vector3.up).magnitude;
            Model.DetailTitle = selected.Name; Model.DetailText = selected.Description + "\n\n" + (inside ? "距小屋门口约 " : "距你约 ") + Mathf.RoundToInt(distance) + " 米";
        }
        public void Clear() { game = null; selectedId = "farm"; Model.Locations.Clear(); Model.Roads = new Vector4[0]; Model.Region = Model.DetailTitle = Model.DetailText = null; }
    }
}
