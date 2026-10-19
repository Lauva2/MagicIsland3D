#if UNITY_EDITOR
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed partial class GameManager
    {
        private bool testBiomeTravel;
        public bool CanTestBiomeTravel => !testBiomeTravel && State==GameFlowState.Playing && !ModalOpen && !ConstructionBusy
            && !AnyGameplayCommit && !Tools.Busy && !Housing.Inside && !Housing.State.Busy
            && Terrain.Terrain?.World?.ArtVersion==3;

        // Editor only. Synchronous, no disk writes, scene changes or gameplay rewards.
        // A blocked landing leaves the player/camera untouched. Managers retain all AA ownership.
        public bool TryTestBiomeTravel(IslandBiome biome)
        {
            if(!CanTestBiomeTravel || !System.Array.Exists(BiomeIslandLayout.Biomes,b=>b==biome))return false;
            if(!Terrain.TryBiomeLanding(biome,Players.Player.GetComponent<CharacterController>(),out var point))
            { Debug.LogWarning("群系传送失败：岛上没有安全落点。"); return false; }
            testBiomeTravel=true;
            try
            {
                SetInputEnabled(false);
                Magic.ResetPresentation(); Cameras.RestoreOccluders();
                Players.RestorePosition(point); Physics.SyncTransforms();
                Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget();
                Weather.Tick(Time.Clock.Minute,true); Map.Capture();
            }
            finally { testBiomeTravel=false; SetInputEnabled(State==GameFlowState.Playing&&!ModalOpen); }
            return true;
        }
    }
}
#endif
