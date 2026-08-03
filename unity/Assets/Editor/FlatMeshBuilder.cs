#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Accumulates prisms/boxes with per-face vertices (no sharing), so
/// RecalculateNormals yields flat shading — the greybox look. Shared by
/// the ship and board-feature mesh builders.
/// </summary>
internal sealed class FlatMeshBuilder
{
    private readonly List<Vector3> _vertices = new List<Vector3>();
    private readonly List<int> _triangles = new List<int>();

    public void AddBox(Vector3 center, Vector3 size)
    {
        var half = size / 2f;
        AddPrism(
            new[]
            {
                new Vector2(center.x - half.x, center.z - half.z),
                new Vector2(center.x - half.x, center.z + half.z),
                new Vector2(center.x + half.x, center.z + half.z),
                new Vector2(center.x + half.x, center.z - half.z)
            },
            center.y - half.y,
            center.y + half.y);
    }

    /// <summary>Extrudes a convex outline (x, z), clockwise in
    /// x-right/z-up, between two heights: caps plus side walls.</summary>
    public void AddPrism(Vector2[] outline, float yBottom, float yTop)
    {
        // Top cap: fan in outline order gives an upward-facing normal
        // for a clockwise outline in Unity's left-handed space.
        for (var i = 1; i < outline.Length - 1; i++)
        {
            AddTriangle(
                At(outline[0], yTop), At(outline[i], yTop), At(outline[i + 1], yTop));
        }

        // Bottom cap, opposite winding.
        for (var i = 1; i < outline.Length - 1; i++)
        {
            AddTriangle(
                At(outline[0], yBottom), At(outline[i + 1], yBottom), At(outline[i], yBottom));
        }

        // Side walls (outline order runs clockwise in x-right/z-up, so
        // outward faces need the reversed winding).
        for (var i = 0; i < outline.Length; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Length];
            AddTriangle(At(a, yBottom), At(b, yTop), At(a, yTop));
            AddTriangle(At(a, yBottom), At(b, yBottom), At(b, yTop));
        }
    }

    /// <summary>Like <see cref="AddPrism"/>, but caps fan from the outline
    /// centroid — correct for star-shaped (possibly concave) outlines like
    /// the irregular rock/debris polygons, where a vertex fan would spill
    /// outside the shape.</summary>
    public void AddRadialPrism(Vector2[] outline, float yBottom, float yTop)
    {
        var centroid = Vector2.zero;
        foreach (var point in outline)
        {
            centroid += point;
        }

        centroid /= outline.Length;

        for (var i = 0; i < outline.Length; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Length];
            AddTriangle(At(centroid, yTop), At(a, yTop), At(b, yTop));
            AddTriangle(At(centroid, yBottom), At(b, yBottom), At(a, yBottom));
            AddTriangle(At(a, yBottom), At(b, yTop), At(a, yTop));
            AddTriangle(At(a, yBottom), At(b, yBottom), At(b, yTop));
        }
    }

    private static Vector3 At(Vector2 point, float y) => new Vector3(point.x, y, point.y);

    private void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        var start = _vertices.Count;
        _vertices.Add(a);
        _vertices.Add(b);
        _vertices.Add(c);
        _triangles.Add(start);
        _triangles.Add(start + 1);
        _triangles.Add(start + 2);
    }

    public Mesh Build()
    {
        var mesh = new Mesh();
        mesh.SetVertices(_vertices);
        mesh.SetTriangles(_triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
#endif
