using System.Collections.Generic;
using UnityEngine;

public static class PolygonOverlap2D
{
    public static float GetOverlapArea(Vector2[] subject, Vector2[] clip)
    {
        List<Vector2> intersection = GetIntersectionPolygon(subject, clip);
        return Mathf.Abs(ShoelaceArea(intersection));
    }

    public static List<Vector2> GetIntersectionPolygon(Vector2[] subject, Vector2[] clip)
    {
        if (subject == null || subject.Length < 3)
            return new List<Vector2>();

        if (clip == null || clip.Length < 3)
            return new List<Vector2>();

        return SutherlandHodgman(
            new List<Vector2>(subject),
            new List<Vector2>(clip)
        );
    }

    private static List<Vector2> SutherlandHodgman(List<Vector2> subject, List<Vector2> clip)
    {
        List<Vector2> output = new List<Vector2>(subject);

        for (int i = 0; i < clip.Count; i++)
        {
            Vector2 a = clip[i];
            Vector2 b = clip[(i + 1) % clip.Count];

            List<Vector2> input = new List<Vector2>(output);
            output.Clear();

            if (input.Count == 0)
                break;

            Vector2 previous = input[input.Count - 1];

            foreach (Vector2 current in input)
            {
                bool currentInside = IsInside(current, a, b);
                bool previousInside = IsInside(previous, a, b);

                if (currentInside)
                {
                    if (!previousInside)
                        output.Add(GetIntersection(previous, current, a, b));

                    output.Add(current);
                }
                else if (previousInside)
                {
                    output.Add(GetIntersection(previous, current, a, b));
                }

                previous = current;
            }
        }

        return output;
    }

    private static bool IsInside(Vector2 p, Vector2 a, Vector2 b)
    {
        return Cross(b - a, p - a) >= 0f;
    }

    private static Vector2 GetIntersection(Vector2 p1, Vector2 p2, Vector2 a, Vector2 b)
    {
        Vector2 r = p2 - p1;
        Vector2 s = b - a;

        float denominator = Cross(r, s);

        if (Mathf.Abs(denominator) < 0.00001f)
            return p1;

        float t = Cross(a - p1, s) / denominator;
        return p1 + t * r;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private static float ShoelaceArea(List<Vector2> points)
    {
        if (points == null || points.Count < 3)
            return 0f;

        float area = 0f;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Count];

            area += a.x * b.y - b.x * a.y;
        }

        return area * 0.5f;
    }
}