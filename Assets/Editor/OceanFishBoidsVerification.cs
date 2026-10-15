using System;
using System.IO;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class OceanFishBoidsVerification
    {
        static void Require(bool condition, string message)
        { if (!condition) throw new Exception("Boids: " + message); }

        [MenuItem("Magic Island/Verify Fish Boids")]
        public static void Run()
        {
            var shader=Shader.Find("MagicIsland/Ocean Ecology");
            Require(shader!=null&&!ShaderUtil.ShaderHasError(shader),"fish shader imports without compiler errors");
            OceanFishBehaviourVerification.Verify(AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath));
            OceanEcologyPolishVerification.Verify(AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath));
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/OceanFishBoidsVerification.txt", "PASS: fish shader import, separation/alignment/cohesion, simultaneous neighbour snapshots, zero-weight controls, bounded acceleration, invalid time/pause, baked water columns, separated bodies, individual depth changes, no persistent stalls, swept reef avoidance, feeding/exploration, startle/recovery, 30/60 fps and combined-renderer poses. Packed-AA rendering is a separate OceanFlowVerification check.\n");
            Debug.Log("Ocean fish Boids verification PASSED.");
        }

        static OceanFishSteering Create(OceanSchool school, float separation, float alignment, float cohesion)
        {
            school.NeighbourRadius = 4; school.SeparationRadius = 1;
            school.SeparationWeight = separation; school.AlignmentWeight = alignment;
            school.CohesionWeight = cohesion; school.CruiseWeight = 0;
            var simulation = new OceanFishSteering(school);
            for (int i = 0; i < simulation.States.Length; i++)
            {
                var state = simulation.States[i]; state.Until = 100;
                state.Yaw = 0; state.Velocity = Vector3.forward * .5f;
                simulation.States[i] = state;
            }
            return simulation;
        }

        public static void VerifyFlocking()
        {
            var root = new GameObject("Boids rules fixture");
            try
            {
                var school = root.AddComponent<OceanSchool>();
                school.Members = 2; school.HomeRadius = 10;
                school.Swimmers = new[] {
                    new OceanFishMember { Spawn = new Vector3(-.2f, 0, 0), Radius = .1f, Pivot = new Vector4(0,0,0,1) },
                    new OceanFishMember { Spawn = new Vector3(.2f, 0, 0), Radius = .1f, Pivot = new Vector4(0,0,0,3.399963f) }
                };
                var separation = Create(school, 2, 0, 0);
                separation.Advance(1);
                Require(separation.States[0].Position.x < -.3f && separation.States[1].Position.x > .3f, "close fish spread out through actual steering");

                school.Swimmers[0].Spawn = new Vector3(-1, 0, 0);
                school.Swimmers[1].Spawn = new Vector3(1, 0, 0);
                var cohesion = Create(school, 0, 0, 1);
                cohesion.Advance(1);
                Require(cohesion.States[0].Position.x > -.9f && cohesion.States[1].Position.x < .9f, "loose fish approach their common center");

                var aligned = Create(school, 0, 1, 0);
                float neighbourYaw = 3 * Mathf.Deg2Rad;
                var neighbourVelocity = new Vector3(Mathf.Sin(neighbourYaw), 0, Mathf.Cos(neighbourYaw)) * .5f;
                var state = aligned.States[1]; state.Yaw = neighbourYaw;
                state.Velocity = neighbourVelocity; aligned.States[1] = state;
                aligned.Advance(OceanFishSteering.StepSeconds);
                Require(Mathf.Abs(aligned.States[0].Yaw - neighbourYaw) < .00001f && Mathf.Abs(aligned.States[1].Yaw) < .00001f, "members turn towards each other's previous headings");

                // With an immutable neighbourhood snapshot, reversing member order keeps the same first-step result.
                Vector3 first = aligned.States[0].Velocity, second = aligned.States[1].Velocity;
                var reverse = Create(school, 0, 1, 0);
                state = reverse.States[0]; state.Yaw = neighbourYaw; state.Velocity = neighbourVelocity;
                reverse.States[0] = state;
                reverse.Advance(OceanFishSteering.StepSeconds);
                // Compare heading, since each baked member deliberately has a different cruise speed.
                Require(Vector3.Distance(first.normalized, reverse.States[1].Velocity.normalized) < .00001f
                    && Vector3.Distance(second.normalized, reverse.States[0].Velocity.normalized) < .00001f, "index order does not leak partially updated neighbour velocities");

                var disabled = Create(school, 0, 0, 0);
                disabled.Advance(OceanFishSteering.StepSeconds);
                Require(Mathf.Abs(disabled.States[0].Velocity.x) < .00001f, "zero weights remove social steering");
                Vector3 paused = disabled.States[0].Position;
                foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) disabled.Advance(invalid);
                Require(disabled.States[0].Position == paused, "pause and invalid elapsed time cannot advance the flock");
                float before = disabled.States[0].Velocity.magnitude;
                disabled.Advance(OceanFishSteering.StepSeconds);
                Require(Mathf.Abs(disabled.States[0].Velocity.magnitude - before) <= school.Acceleration * OceanFishSteering.StepSeconds + .00001f, "acceleration limits speed changes");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
