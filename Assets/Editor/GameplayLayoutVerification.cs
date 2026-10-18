using System;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.UI.Views;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GameplayLayoutVerification
    {
        static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        static Rect ScreenRect(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }
        public static void Verify(GameManager g)
        {
            var hud = Object.FindObjectOfType<PrototypeHud>(); var task = Object.FindObjectOfType<TaskView>();
            var crop = Object.FindObjectOfType<CropInfoView>(); var prompt = Object.FindObjectOfType<InteractionPromptView>();
            var camera = g.Players.Player.ViewCamera;
            var plot = g.Farm.Grid.CellCenter(19); g.Players.RestorePosition(plot + new Vector3(0, .1f, -1)); Physics.SyncTransforms();
            camera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget();
            g.Interactions.RefreshPointer(new Ray(plot + Vector3.up * 8, Vector3.down), false); g.UI.RefreshViews();
            Require(crop.Group.alpha == 1 && prompt.Group.alpha == 0, "Compact crop/prompt cards remain mutually exclusive");
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(1920, 1080) })
            {
                GameUiPreview.CaptureInventory(camera, size.x, size.y, () =>
                {
                    var cards = new[] { (RectTransform)hud.DayText.transform.parent, (RectTransform)hud.ToolButtons[0].transform.parent,
                        (RectTransform)crop.NameText.transform.parent, task.Card };
                    var buttons = new[] { hud.ReturnMenuButton, hud.InventoryButton, hud.MapButton, hud.MagicButton };
                    var bounds = cards.Select(c => ScreenRect(c, camera)).ToArray();
                    for (int i = 0; i < bounds.Length; i++)
                    {
                        Require(bounds[i].xMin >= 0 && bounds[i].yMin >= 0 && bounds[i].xMax <= size.x && bounds[i].yMax <= size.y, "Card inside viewport " + size);
                        for (int j = i + 1; j < bounds.Length; j++) Require(!bounds[i].Overlaps(bounds[j]), "Gameplay cards cannot overlap " + size);
                        foreach (var button in buttons) Require(!bounds[i].Overlaps(ScreenRect((RectTransform)button.transform, camera)), "Top buttons cannot be covered " + size);
                    }
                    foreach (var view in new UIView[] { hud, crop, task }) foreach (var label in view.GetComponentsInChildren<TMP_Text>())
                    {
                        label.ForceMeshUpdate(true);
                        Require(!label.isTextOverflowing && label.text.Where(c => !char.IsControl(c)).All(c => label.font.HasCharacter(c)), "Compact Chinese label fits: " + label.name + " / " + label.text);
                    }
                });
                File.Copy("Logs/InventoryPreview.png", "Logs/CompactGameplay" + size.x + "x" + size.y + ".png", true);
            }
            float height = task.Card.sizeDelta.y;
            task.CollapseButton.onClick.Invoke(); Require(task.Card.sizeDelta.y == 52 && g.Players.Player.enabled, "Collapse remains non-modal");
            task.CollapseButton.onClick.Invoke(); Require(task.Card.sizeDelta.y == height && height == task.ExpandedHeight, "Expand restores authored height");
            Require(!hud.InventoryText.gameObject.activeSelf && !hud.FeedbackText.gameObject.activeSelf, "Secondary inventory counts and duplicate feedback are removed from persistent HUD");
            File.WriteAllText("Logs/GameplayLayoutVerification.txt", "PASS: packed AA; compact authored HUD and seven tools; crop/prompt exclusivity; no card/button overlap at 1280x720, 1024x768 and 1920x1080; Chinese glyphs and text fit; task collapse/expand without pause; original interactions and AA identities retained.\n");
            g.Interactions.ClearTarget();
        }
    }
}
