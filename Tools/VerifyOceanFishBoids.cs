using System;
using MagicIsland.Ocean;
using UnityEngine;

class VerifyOceanFishBoids
{
    static int checks;
    static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }

    static int Main()
    {
        var positions = new[] { Vector3.zero, new Vector3(.2f, 0, 0) };
        var velocities = new[] { Vector3.up, Vector3.right };
        var radii = new[] { .1f, .1f };
        var forces = OceanFishBoids.Evaluate(positions, velocities, radii, 0, 2, .7f);
        Check(forces.Neighbours == 1 && forces.Separation.x < -.5f, "Close fish steer apart");
        Check(forces.Alignment.x > .9f && Mathf.Abs(forces.Alignment.y) < .00001f, "Fish turn towards a moving neighbour's heading");
        Check(forces.Cohesion.x > 0, "Fish steer towards their neighbours' center");

        positions[1] = new Vector3(1.5f, 0, 0);
        forces = OceanFishBoids.Evaluate(positions, velocities, radii, 0, 2, .7f);
        Check(forces.Separation == Vector3.zero && forces.Cohesion.x > .5f, "Loose fish regroup without a close-range repulsion");
        positions[1] = new Vector3(4, 0, 0);
        forces = OceanFishBoids.Evaluate(positions, velocities, radii, 0, 2, .7f);
        Check(forces.Neighbours == 0 && forces.Alignment == Vector3.zero && forces.Cohesion == Vector3.zero, "Distant fish do not steer each other");

        positions[1] = Vector3.zero;
        var a = OceanFishBoids.Evaluate(positions, velocities, radii, 0, 2, .7f);
        var b = OceanFishBoids.Evaluate(positions, velocities, radii, 1, 2, .7f);
        Check(a.Separation.sqrMagnitude > .9f && (a.Separation + b.Separation).sqrMagnitude < .000001f, "Overlapping members separate in opposite deterministic directions");
        velocities[1] = Vector3.zero;
        forces = OceanFishBoids.Evaluate(positions, velocities, radii, 0, 2, .7f);
        Check(forces.Alignment == Vector3.zero, "Stationary neighbours do not invent a heading");

        radii[0] = radii[1] = 1;
        positions[1] = new Vector3(1, 0, 0);
        forces = OceanFishBoids.Evaluate(positions, velocities, radii, 0, 2, .1f);
        Check(forces.Separation.x < 0, "Body clearance protects large fish even with a small configured spacing");
        positions[1] = Vector3.zero;
        for (int repeat = 0; repeat < 100; repeat++)
        {
            forces = OceanFishBoids.Evaluate(positions, velocities, radii, 0, 2, .7f);
            Check(forces.Separation.magnitude <= 2.00001f && forces.Cohesion.magnitude <= 1.00001f, "Forces remain bounded");
        }
        forces = OceanFishBoids.Evaluate(new[] { Vector3.zero }, new[] { Vector3.zero }, new[] { .2f }, 0, 2, .7f);
        Check(forces.Neighbours == 0 && forces.Separation == Vector3.zero && forces.Alignment == Vector3.zero && forces.Cohesion == Vector3.zero, "A single ray swims without phantom flock forces");
        positions=new[]{Vector3.zero,new Vector3(0,.3f,0)};
        velocities=new[]{Vector3.zero,Vector3.up};radii=new[]{.1f,.1f};
        forces=OceanFishBoids.Evaluate(positions,velocities,radii,0,2,.7f);
        Check(forces.Separation.y<0&&forces.Cohesion.y>0&&forces.Alignment.y>0,"Depth participates in all three Boids rules");
        positions[1]=new Vector3(1.3f,0,0);velocities[0]=Vector3.right;velocities[1]=Vector3.left;
        forces=OceanFishBoids.Evaluate(positions,velocities,radii,0,2,.7f);
        Check(forces.Separation.x<0,"Approaching swimmers separate before reaching body clearance");
        Console.WriteLine("PASS: " + checks + " Boids neighbourhood assertions");
        return 0;
    }
}
