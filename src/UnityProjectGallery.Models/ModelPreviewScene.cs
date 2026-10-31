namespace UnityProjectGallery.Models;

/// <summary>静态模型场景，供 WPF 视口转换。</summary>
public sealed class ModelPreviewScene
{
    public required ModelPreviewMesh[] Meshes { get; init; }

    public int MeshCount => Meshes.Length;

    public int VertexCount => Meshes.Sum(mesh => mesh.Positions.Length / 3);

    public int TriangleCount => Meshes.Sum(mesh => mesh.Indices.Length / 3);
}

/// <summary>一个三角网格及基础颜色，不包含动画和 Unity 材质信息。</summary>
public sealed class ModelPreviewMesh
{
    public string Name { get; init; } = string.Empty;

    /// <summary>XYZ 三元组顶点坐标。</summary>
    public required float[] Positions { get; init; }

    /// <summary>三角面索引。</summary>
    public required int[] Indices { get; init; }

    /// <summary>与顶点数量一致的 XYZ 法线；为空时由视口按面计算。</summary>
    public float[]? Normals { get; init; }

    public byte R { get; init; } = 190;

    public byte G { get; init; } = 198;

    public byte B { get; init; } = 212;

    public byte A { get; init; } = 255;
}

/// <summary>模型加载结果。</summary>
public sealed record ModelPreviewResult(ModelPreviewScene? Scene, string? ErrorMessage)
{
    public bool Success => Scene is not null;
}
