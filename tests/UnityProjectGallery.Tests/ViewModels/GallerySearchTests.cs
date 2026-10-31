using UnityProjectGallery.App.ViewModels;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Tests.ViewModels;

public sealed class GallerySearchTests
{
    [Fact]
    public void Search_MatchesFeatureName()
    {
        var project = new ProjectItemViewModel(
            new UnityProject
            {
                Id = "feature-search",
                Name = "FlockingDemo",
                ProjectPath = @"C:\UnityProjects\FlockingDemo",
                UnityVersion = "2022.3.62f1",
                Description = "群体行为演示"
            },
            _ => { },
            _ => { });

        project.CreateFeature("鸟群群体寻路", "Boids 群体移动演示");

        var gallery = new GalleryViewModel(new[] { project });
        gallery.SearchText = "群体寻路";

        Assert.Single(gallery.Projects);
        Assert.Same(project, gallery.Projects[0]);
    }

    [Fact]
    public void Search_DoesNotMatchUnrelatedFeatureName()
    {
        var project = new ProjectItemViewModel(
            new UnityProject
            {
                Id = "feature-no-search",
                Name = "TerrainDemo",
                ProjectPath = @"C:\UnityProjects\TerrainDemo",
                UnityVersion = "6000.0.34f1",
                Description = "地形生成"
            },
            _ => { },
            _ => { });

        project.CreateFeature("地形分块加载", string.Empty);
        var gallery = new GalleryViewModel(new[] { project });

        gallery.SearchText = "鸟群寻路";

        Assert.Empty(gallery.Projects);
    }
}
