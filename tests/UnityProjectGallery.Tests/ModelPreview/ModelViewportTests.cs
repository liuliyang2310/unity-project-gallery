using System.Threading;
using UnityProjectGallery.App.Controls;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Tests.ModelPreview;

public sealed class ModelViewportTests
{
    [Fact]
    public void SettingScene_BuildsWpfViewportWithoutErrors()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var viewport = new ModelViewport();
                viewport.Scene = new ModelPreviewScene
                {
                    Meshes = new[]
                    {
                        new ModelPreviewMesh
                        {
                            Name = "Triangle",
                            Positions = new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f },
                            Indices = new[] { 0, 1, 2 },
                            Normals = new[] { 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f }
                        }
                    }
                };
                viewport.ResetCamera();
                viewport.Scene = null;
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }
}
