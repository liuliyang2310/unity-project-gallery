using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Assimp;
using UnityProjectGallery.Infrastructure.Models;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Tests.ModelPreview;

public sealed class AssimpModelLoaderTests : IDisposable
{
    private readonly string _tempRoot;

    public AssimpModelLoaderTests()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "UnityProjectGalleryModelLoaderTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Theory]
    [InlineData(".fbx")]
    [InlineData(".obj")]
    [InlineData(".gltf")]
    [InlineData(".glb")]
    public void Assimp_SupportsRequestedImportFormats(string extension)
    {
        using var context = new AssimpContext();
        Assert.True(context.IsImportFormatSupported(extension));
    }

    [Fact]
    public async Task LoadAsync_LoadsGltfTriangle()
    {
        var filePath = Path.Combine(_tempRoot, "triangle.gltf");
        var buffer = BuildTriangleBuffer();
        var gltf = BuildGltfDocument(buffer, includeUri: true);
        await File.WriteAllTextAsync(filePath, gltf);

        var loader = new AssimpModelLoader();
        var result = await loader.LoadAsync(filePath);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Scene);
        Assert.Equal(1, result.Scene!.TriangleCount);
    }

    [Fact]
    public async Task LoadAsync_LoadsGlbTriangle()
    {
        var filePath = Path.Combine(_tempRoot, "triangle.glb");
        var buffer = BuildTriangleBuffer();
        var json = BuildGltfDocument(buffer, includeUri: false);
        var glb = BuildGlb(json, buffer);
        await File.WriteAllBytesAsync(filePath, glb);

        var loader = new AssimpModelLoader();
        var result = await loader.LoadAsync(filePath);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Scene);
        Assert.Equal(1, result.Scene!.TriangleCount);
    }

    [Fact]
    public async Task LoadAsync_LoadsObjWithCommentLines()
    {
        var filePath = Path.Combine(_tempRoot, "commented.obj");
        await File.WriteAllTextAsync(
            filePath,
            "# exported by Blender\no Triangle\nv 0 0 0\nv 1 0 0\nv 0 1 0\n# face\nf 1 2 3\n");

        var loader = new AssimpModelLoader();
        var result = await loader.LoadAsync(filePath);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Scene);
        Assert.Equal(1, result.Scene!.TriangleCount);
    }

    [Fact]
    public async Task LoadAsync_LoadsObjWithWindowsLineEndings()
    {
        var filePath = Path.Combine(_tempRoot, "crlf.obj");
        await File.WriteAllTextAsync(
            filePath,
            "# exported by Blender\r\no Triangle\r\nv 0 0 0\r\nv 1 0 0\r\nv 0 1 0\r\nf 1 2 3\r\n");

        var loader = new AssimpModelLoader();
        var result = await loader.LoadAsync(filePath);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Scene);
        Assert.Equal(1, result.Scene!.TriangleCount);
    }

    [Fact]
    public async Task LoadAsync_LoadsObjTriangleUsingNativeAssimp()
    {
        var filePath = Path.Combine(_tempRoot, "triangle.obj");
        await File.WriteAllTextAsync(
            filePath,
            "o Triangle\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");

        var loader = new AssimpModelLoader();
        var result = await loader.LoadAsync(filePath);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Scene);
        var scene = result.Scene!;
        Assert.Equal(1, scene.MeshCount);
        Assert.Equal(3, scene.VertexCount);
        Assert.Equal(1, scene.TriangleCount);
        var mesh = Assert.Single(scene.Meshes);
        Assert.Equal(9, mesh.Positions.Length);
        Assert.Equal(3, mesh.Indices.Length);
    }

    [Fact]
    public async Task LoadAsync_LoadsAsciiFbxTriangle()
    {
        var filePath = Path.Combine(_tempRoot, "triangle.fbx");
        await File.WriteAllTextAsync(
            filePath,
            """
            ; FBX 7.4.0 project file
            FBXHeaderExtension:  {
                FBXHeaderVersion: 1003
                FBXVersion: 7400
            }
            GlobalSettings:  {
                Version: 1000
                Properties70:  {
                    P: "UpAxis", "int", "Integer", "",1
                    P: "UpAxisSign", "int", "Integer", "",1
                    P: "FrontAxis", "int", "Integer", "",2
                    P: "FrontAxisSign", "int", "Integer", "",1
                    P: "CoordAxis", "int", "Integer", "",0
                    P: "CoordAxisSign", "int", "Integer", "",1
                }
            }
            Objects:  {
                Geometry: 1, "Geometry::Triangle", "Mesh" {
                    Vertices: *9 {
                        a: 0,0,0,1,0,0,0,1,0
                    }
                    PolygonVertexIndex: *3 {
                        a: 0,1,-3
                    }
                    GeometryVersion: 124
                }
                Model: 2, "Model::Triangle", "Mesh" {
                    Version: 232
                    Shading: T
                    Culling: "CullingOff"
                }
            }
            Connections:  {
                C: "OO",2,0
                C: "OO",1,2
            }
            """);
        var loader = new AssimpModelLoader();

        var result = await loader.LoadAsync(filePath);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Scene);
        Assert.Equal(1, result.Scene!.TriangleCount);
    }

    [Fact]
    public async Task LoadAsync_ReportsMissingFile()
    {
        var filePath = Path.Combine(_tempRoot, "missing.obj");
        var loader = new AssimpModelLoader();

        var result = await loader.LoadAsync(filePath);

        Assert.False(result.Success);
        Assert.Contains("不存在", result.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_RejectsUnsupportedExtension()
    {
        var filePath = Path.Combine(_tempRoot, "notes.txt");
        await File.WriteAllTextAsync(filePath, "not a model");
        var loader = new AssimpModelLoader();

        var result = await loader.LoadAsync(filePath);

        Assert.False(result.Success);
        Assert.Contains("不支持", result.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_ReportsCorruptedModelWithoutThrowing()
    {
        var filePath = Path.Combine(_tempRoot, "broken.obj");
        await File.WriteAllTextAsync(filePath, "this is not a valid obj file");
        var loader = new AssimpModelLoader();

        var result = await loader.LoadAsync(filePath);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    private static byte[] BuildTriangleBuffer()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        foreach (var value in new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f })
        {
            writer.Write(value);
        }

        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)2);
        writer.Flush();
        return stream.ToArray();
    }

    private static string BuildGltfDocument(byte[] buffer, bool includeUri)
    {
        var uri = "data:application/octet-stream;base64," + Convert.ToBase64String(buffer);
        var document = new
        {
            asset = new { version = "2.0", generator = "UnityProjectGallery.Tests" },
            scene = 0,
            scenes = new[] { new { nodes = new[] { 0 } } },
            nodes = new[] { new { mesh = 0 } },
            meshes = new[]
            {
                new
                {
                    primitives = new[]
                    {
                        new
                        {
                            attributes = new Dictionary<string, int> { ["POSITION"] = 0 },
                            indices = 1,
                            mode = 4
                        }
                    }
                }
            },
            buffers = new[] { new { uri, byteLength = buffer.Length } },
            bufferViews = new[]
            {
                new { buffer = 0, byteOffset = 0, byteLength = 36, target = 34962 },
                new { buffer = 0, byteOffset = 36, byteLength = 6, target = 34963 }
            },
            accessors = new object[]
            {
                new
                {
                    bufferView = 0,
                    componentType = 5126,
                    count = 3,
                    type = "VEC3",
                    min = new[] { 0f, 0f, 0f },
                    max = new[] { 1f, 1f, 0f }
                },
                new { bufferView = 1, componentType = 5123, count = 3, type = "SCALAR" }
            }
        };

        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        if (includeUri)
        {
            return json;
        }

        var node = JsonNode.Parse(json)!;
        node["buffers"]![0]!.AsObject().Remove("uri");
        return node.ToJsonString();
    }

    private static byte[] BuildGlb(string json, byte[] buffer)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var jsonLength = PadToFourBytes(jsonBytes.Length);
        var bufferLength = PadToFourBytes(buffer.Length);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(0x46546C67);
        writer.Write(2);
        writer.Write(12 + 8 + jsonLength + 8 + bufferLength);
        writer.Write(jsonLength);
        writer.Write(0x4E4F534A);
        writer.Write(jsonBytes);
        for (var i = jsonBytes.Length; i < jsonLength; i++)
        {
            writer.Write((byte)0x20);
        }

        writer.Write(bufferLength);
        writer.Write(0x004E4942);
        writer.Write(buffer);
        for (var i = buffer.Length; i < bufferLength; i++)
        {
            writer.Write((byte)0);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static int PadToFourBytes(int length)
    {
        return (length + 3) / 4 * 4;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
