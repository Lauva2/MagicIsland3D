using System.Collections;
using System.Collections.Generic;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace MagicIsland.Managers
{
    public sealed class NPCManager : MonoBehaviour
    {
        public NPCState State { get; private set; }
        public NPCCatalog Catalog { get; private set; }
        public string LastError { get; private set; }
        public IReadOnlyDictionary<string, NPCActor> Actors => actors;
        private readonly Dictionary<string, NPCActor> actors = new Dictionary<string, NPCActor>();
        private readonly List<AsyncOperationHandle<GameObject>> handles = new List<AsyncOperationHandle<GameObject>>();
        private int generation;
        private NPCActor focused;
        private Quaternion rotation;
        public void Initialize(NPCCatalog catalog, SavedNPC[] saved = null)
        { Clear(); Catalog = catalog; State = new NPCState(catalog.Ids()); if (saved != null) State.Restore(saved); }
        public IEnumerator LoadActors(Transform world, Vector3 hallPosition)
        {
            LastError = null; int current = generation;
            foreach (var config in Catalog.NPCs)
            {
                var handle = Addressables.InstantiateAsync(config.PrefabAddress, world); handles.Add(handle); yield return handle;
                if (current != generation) yield break;
                var actor = handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null ? handle.Result.GetComponent<NPCActor>() : null;
                if (actor == null || actor.GetComponentsInChildren<Collider>().Length == 0) { LastError = "NPC prefab needs actor and colliders: " + config.PrefabAddress; yield break; }
                actor.Initialize(config.Id); if (actor.NameText != null) actor.NameText.text = config.DisplayName;
                actor.transform.localPosition = hallPosition + config.HallOffset; actors.Add(config.Id, actor);
            }
        }
        public bool Owns(NPCActor actor) => actor != null && actor.Id != null && actors.TryGetValue(actor.Id, out var own) && actor == own;
        public bool InRange(string id, Vector3 position, float reach) => id != null && actors.TryGetValue(id, out var actor)
            && Vector3.ProjectOnPlane(actor.transform.position - position, Vector3.up).sqrMagnitude <= reach * reach;
        public bool IsFocused(string id) => focused != null && focused.Id == id;
        public void Focus(string id, Vector3 position)
        {
            Unfocus(); if (!actors.TryGetValue(id, out focused)) return; rotation = focused.transform.rotation;
            var direction = Vector3.ProjectOnPlane(position - focused.transform.position, Vector3.up);
            if (direction.sqrMagnitude > .001f) focused.transform.rotation = Quaternion.LookRotation(direction);
        }
        public void Unfocus() { if (focused != null) focused.transform.rotation = rotation; focused = null; }
        public void Clear()
        {
            generation++; Unfocus();
            foreach (var handle in handles) if (handle.IsValid()) { if (handle.IsDone) Release(handle); else handle.Completed += Release; }
            handles.Clear(); actors.Clear(); State = null; Catalog = null; LastError = null;
        }
        private static void Release(AsyncOperationHandle<GameObject> handle)
        { if (!handle.IsValid()) return; if (handle.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(handle); else Addressables.Release(handle); }
        private void OnDestroy() => Clear();
    }
}
