using System.Linq;
using MagicIsland.Tools;
using UnityEngine;

namespace MagicIsland.Player
{
    // Visual state only. ToolManager supplies progress after committing gameplay once.
    public sealed class PlayerAnimation : MonoBehaviour
    {
        public Animator Animator;
        public Transform ToolSocket;
        [Min(.1f)] public float ReferenceWalkSpeed = 4f;
        private static readonly int moveSpeed = UnityEngine.Animator.StringToHash("MoveSpeed");
        private static readonly int strideRate = UnityEngine.Animator.StringToHash("StrideRate");
        private static readonly int idle = UnityEngine.Animator.StringToHash("Base Layer.Idle");
        private int actionState;
        public bool Paused { get; private set; }
        public ToolActionKind? Action { get; private set; }
        public float ActionProgress { get; private set; }
        public float MovementRatio { get; private set; }
        public bool IsValid => Animator != null && Animator.runtimeAnimatorController != null && ToolSocket != null
            && ToolSocket.IsChildOf(Animator.transform) && ReferenceWalkSpeed >= .1f && !float.IsInfinity(ReferenceWalkSpeed)
            && Animator.parameters.Any(p => p.nameHash == moveSpeed && p.type == AnimatorControllerParameterType.Float)
            && Animator.parameters.Any(p => p.nameHash == strideRate && p.type == AnimatorControllerParameterType.Float)
            && new[] { "Idle", "Walk", "Hoe", "Plant", "Water", "Axe", "Pickaxe", "Harvest", "Fertilize" }
                .All(s => Animator.HasState(0, UnityEngine.Animator.StringToHash("Base Layer." + s)));

        public void Initialize()
        {
            Animator.applyRootMotion = false; Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            ResetPose();
        }
        public void SetMovement(float speed, float maximumSpeed, float deltaTime)
        {
            MovementRatio = Paused || Action.HasValue ? 0 : Mathf.Clamp01(speed / Mathf.Max(.1f, maximumSpeed));
            Animator.SetFloat(moveSpeed, MovementRatio, .1f, Mathf.Max(0, deltaTime));
            Animator.SetFloat(strideRate, MovementRatio > .01f ? Mathf.Clamp(speed / ReferenceWalkSpeed, .1f, 3f) : 1f);
        }
        public void SetPaused(bool paused)
        {
            if (Animator == null) return;
            Paused = paused; Animator.speed = paused || Action.HasValue ? 0 : 1;
            if (paused && !Action.HasValue)
            {
                MovementRatio = 0; Animator.SetFloat(moveSpeed, 0); Animator.Play(idle, 0, 0); Animator.Update(0);
            }
        }
        public void BeginAction(ToolActionKind kind)
        {
            string state = kind == ToolActionKind.Seed ? "Plant" : kind == ToolActionKind.Fertilizer ? "Fertilize" : kind.ToString();
            actionState = UnityEngine.Animator.StringToHash("Base Layer." + state);
            Action = kind; MovementRatio = 0; Animator.SetFloat(moveSpeed, 0); Animator.speed = 0;
            SampleAction(0);
        }
        public void SampleAction(float progress)
        {
            if (Paused || !Action.HasValue) return;
            ActionProgress = Mathf.Clamp01(progress);
            Animator.Play(actionState, 0, ActionProgress); Animator.Update(0);
        }
        public void EndAction()
        {
            if (Animator == null) return;
            Action = null; ActionProgress = 0; MovementRatio = 0;
            Animator.SetFloat(moveSpeed, 0); Animator.speed = Paused ? 0 : 1;
            Animator.CrossFadeInFixedTime(idle, .12f, 0, 0);
        }
        public void ResetPose()
        {
            if (Animator == null) return;
            Action = null; ActionProgress = MovementRatio = 0;
            Animator.Rebind(); Animator.SetFloat(moveSpeed, 0); Animator.SetFloat(strideRate, 1);
            Animator.Play(idle, 0, 0); Animator.Update(0); Animator.speed = Paused ? 0 : 1;
        }
    }
}
