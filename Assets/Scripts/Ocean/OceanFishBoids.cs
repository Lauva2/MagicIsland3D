using UnityEngine;

namespace MagicIsland.Ocean
{
    // Pure, allocation-free neighbourhood rules. All inputs belong to the same fixed-step snapshot.
    public static class OceanFishBoids
    {
        public struct Forces
        {
            public Vector3 Separation, Alignment, Cohesion;
            public int Neighbours;
        }

        public static Forces Evaluate(Vector3[] positions, Vector3[] velocities, float[] radii,
            int index, float neighbourRadius, float separationRadius)
        {
            var result = new Forces();
            Vector3 center = Vector3.zero, velocity = Vector3.zero;
            float neighbourhood = Mathf.Max(.01f, neighbourRadius);
            for (int other = 0; other < positions.Length; other++)
            {
                if (other == index) continue;
                Vector3 away = positions[index] - positions[other];
                float distance = away.magnitude;
                float body = radii[index] + radii[other] + .08f;
                float spacing = Mathf.Max(separationRadius, body + .8f);
                // Anticipate approaching bodies before their current positions overlap.
                var relativeVelocity = velocities[index] - velocities[other];
                if(Vector3.Dot(away,relativeVelocity)<0)spacing+=Mathf.Min(.8f,relativeVelocity.magnitude*.7f);
                float approach = relativeVelocity.sqrMagnitude > .00001f
                    ? Mathf.Clamp(-Vector3.Dot(away, relativeVelocity) / relativeVelocity.sqrMagnitude, 0, .8f) : 0;
                var predicted = away + relativeVelocity * approach;
                if (predicted.magnitude < distance && predicted.sqrMagnitude > .0001f)
                    away = predicted.normalized * distance;
                if (distance < spacing)
                {
                    if (distance > .0001f) away /= distance;
                    else
                    {
                        // Coincident members choose opposite directions without gameplay randomness.
                        int first = Mathf.Min(index, other), second = Mathf.Max(index, other);
                        float angle = (first * 17 + second * 31 + 1) * 2.399963f;
                        away = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (index < other ? 1 : -1);
                    }
                    result.Separation += away * Mathf.Clamp((spacing - distance) / Mathf.Max(.1f, spacing - body), 0, 2);
                }
                if (distance > neighbourhood) continue;
                center += positions[other];
                velocity += velocities[other];
                result.Neighbours++;
            }
            result.Separation = Vector3.ClampMagnitude(result.Separation, 2);
            if (result.Neighbours > 0)
            {
                if (velocity.sqrMagnitude > .000001f)
                    result.Alignment = velocity.normalized;
                result.Cohesion = Vector3.ClampMagnitude((center / result.Neighbours - positions[index]) / neighbourhood, 1);
            }
            return result;
        }
    }
}
