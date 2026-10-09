using System.Collections.Generic;
using MagicIsland.Farming;
using MagicIsland.Player;
using MagicIsland.Managers;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MagicIsland.Interaction
{
    public sealed class InteractionController : MonoBehaviour
    {
        public Camera ViewCamera;
        public PlayerMovement Player;
        public FarmGrid Farm;
        public FarmManager FarmManager;
        public FarmTool Tool { get; private set; }
        public string Hint { get; private set; } = "靠近农田，选择锄头开始耕地";
        public string Feedback { get; private set; } = "每次浇水后进入下一天，作物生长一阶段";
        public void SetFeedback(string message) => Feedback = message;
        public float Reach = 2.25f;
        private readonly HashSet<int> visited = new HashSet<int>();
        private float nextAction;
        private bool gestureBlocked;

        public void SelectTool(FarmTool tool)
        {
            Tool = tool;
            // A held click must not switch tool and act again on the same plot.
            if (Input.GetMouseButton(0)) gestureBlocked = true;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectTool(FarmTool.Hoe);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectTool(FarmTool.Seeds);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectTool(FarmTool.WateringCan);
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Input.GetMouseButtonDown(0))
            {
                visited.Clear();
                gestureBlocked = overUI;
            }
            if (!Input.GetMouseButton(0))
            {
                visited.Clear();
                gestureBlocked = false;
            }
            int index = overUI ? -1 : Farm.Pick(ViewCamera.ScreenPointToRay(Input.mousePosition));
            bool inRange = index >= 0 && Vector3.ProjectOnPlane(Farm.CellCenter(index) - Player.transform.position,
                Vector3.up).sqrMagnitude <= Reach * Reach;
            Farm.SetHover(index, inRange);
            Hint = index < 0 ? "WASD 移动 · 鼠标指向农田" : inRange ? FarmManager.Describe(index, Tool) : "距离太远，请靠近";
            if (index < 0 || !Input.GetMouseButton(0) || gestureBlocked || !inRange
                || Time.time < nextAction || visited.Contains(index)) return;
            if (FarmManager.TryAct(index, Tool, out string message))
            {
                visited.Add(index);
                Player.FaceAndAct(Farm.CellCenter(index));
                Feedback = message + "完成";
                nextAction = Time.time + 0.22f;
            }
            else Feedback = message;
        }
    }
}
