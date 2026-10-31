using System.IO;
using Assimp;
using UnityProjectGallery.Core.Models;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Infrastructure.Models;

/// <summary>
/// 使用 Assimp 加载静态模型。开启三角化与顶点预变换，只输出网格和基础漫反射颜色，
/// 不解析动画、骨骼或 Unity 材质。
/// </summary>
public sealed class AssimpModelLoader : IModelPreviewLoader
{
    private const int MaxVertexCount = 2_000_000;
    private const int MaxTriangleCount = 2_000_000;

    private static readonly PostProcessSteps Steps =
        PostProcessSteps.Triangulate |
        PostProcessSteps.PreTransformVertices |
        PostProcessSteps.GenerateSmoothNormals |
        PostProcessSteps.SortByPrimitiveType;

    public Task<ModelPreviewResult> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Load(filePath, cancellationToken), cancellationToken);
    }

    private static ModelPreviewResult Load(
        string filePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new ModelPreviewResult(null, $"模型文件不存在：{filePath}");
        }

        if (!ModelAssetConventions.IsSupported(filePath))
        {
            return new ModelPreviewResult(null, "暂不支持该模型格式。");
        }

        try
        {
            using var context = new AssimpContext();
            var scene = context.ImportFile(filePath, Steps);
            if (scene is null)
            {
                return new ModelPreviewResult(null, "模型解析失败，未返回场景数据。");
            }

            var meshes = new List<ModelPreviewMesh>();
            var totalVertices = 0;
            var totalTriangles = 0;

            foreach (var mesh in scene.Meshes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (mesh.VertexCount == 0 || !mesh.HasVertices || !mesh.HasFaces)
                {
                    continue;
                }

                if (mesh.VertexCount > MaxVertexCount ||
                    mesh.FaceCount > MaxTriangleCount ||
                    totalVertices + mesh.VertexCount > MaxVertexCount ||
                    totalTriangles + mesh.FaceCount > MaxTriangleCount)
                {
                    return new ModelPreviewResult(
                        null,
                        $"模型规模过大（超过 {MaxVertexCount:N0} 顶点），已停止预览。");
                }

                var positions = new float[mesh.VertexCount * 3];
                for (var i = 0; i < mesh.VertexCount; i++)
                {
                    var vertex = mesh.Vertices[i];
                    positions[i * 3] = vertex.X;
                    positions[i * 3 + 1] = vertex.Y;
                    positions[i * 3 + 2] = vertex.Z;
                }

                var indices = new List<int>(mesh.FaceCount * 3);
                foreach (var face in mesh.Faces)
                {
                    if (face.IndexCount != 3)
                    {
                        continue;
                    }

                    indices.Add(face.Indices[0]);
                    indices.Add(face.Indices[1]);
                    indices.Add(face.Indices[2]);
                }

                if (indices.Count == 0)
                {
                    continue;
                }

                var normals = ReadNormals(mesh);
                var (r, g, b, a) = ReadMaterialColor(scene, mesh.MaterialIndex);

                meshes.Add(new ModelPreviewMesh
                {
                    Name = mesh.Name ?? string.Empty,
                    Positions = positions,
                    Indices = indices.ToArray(),
                    Normals = normals,
                    R = r,
                    G = g,
                    B = b,
                    A = a
                });

                totalVertices += mesh.VertexCount;
                totalTriangles += indices.Count / 3;
            }

            if (meshes.Count == 0)
            {
                return new ModelPreviewResult(null, "模型中没有可显示的三角网格。");
            }

            return new ModelPreviewResult(
                new ModelPreviewScene { Meshes = meshes.ToArray() },
                null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ModelPreviewResult(
                null,
                $"模型解析失败：{exception.Message}");
        }
    }

    private static float[]? ReadNormals(Mesh mesh)
    {
        if (!mesh.HasNormals || mesh.Normals.Count != mesh.VertexCount)
        {
            return null;
        }

        var normals = new float[mesh.VertexCount * 3];
        for (var i = 0; i < mesh.VertexCount; i++)
        {
            var normal = mesh.Normals[i];
            normals[i * 3] = normal.X;
            normals[i * 3 + 1] = normal.Y;
            normals[i * 3 + 2] = normal.Z;
        }

        return normals;
    }

    private static (byte R, byte G, byte B, byte A) ReadMaterialColor(
        Scene scene,
        int materialIndex)
    {
        if (materialIndex >= 0 && materialIndex < scene.MaterialCount)
        {
            var color = scene.Materials[materialIndex].ColorDiffuse;
            if (color[0] > 0.02f || color[1] > 0.02f || color[2] > 0.02f)
            {
                return (
                    ToByte(color[0]),
                    ToByte(color[1]),
                    ToByte(color[2]),
                    ToByte(color[3] > 0.02f ? color[3] : 1f));
            }
        }

        return (190, 198, 212, 255);
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);
    }
}
