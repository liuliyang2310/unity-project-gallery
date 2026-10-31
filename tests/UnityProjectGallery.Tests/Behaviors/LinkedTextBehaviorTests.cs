using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using UnityProjectGallery.App.Behaviors;

namespace UnityProjectGallery.Tests.Behaviors;

public sealed class LinkedTextBehaviorTests
{
    [Fact]
    public void SetText_CreatesHyperlinksAndKeepsLineBreaks()
    {
        RunOnStaThread(() =>
        {
            var textBlock = new TextBlock();

            LinkedTextBehavior.SetText(
                textBlock,
                "官网：https://unity.com。\n手册：https://docs.unity3d.com/Manual/。");

            var links = textBlock.Inlines.OfType<Hyperlink>().ToList();
            Assert.Equal(2, links.Count);
            Assert.Equal("unity.com", links[0].NavigateUri.Host);
            Assert.Equal("docs.unity3d.com", links[1].NavigateUri.Host);
            Assert.Single(textBlock.Inlines.OfType<LineBreak>());
        });
    }

    [Fact]
    public void SetText_PrefixesHttpsForWwwAddresses()
    {
        RunOnStaThread(() =>
        {
            var textBlock = new TextBlock();

            LinkedTextBehavior.SetText(textBlock, "访问 www.unity3d.com 了解更多");

            var link = Assert.Single(textBlock.Inlines.OfType<Hyperlink>());
            Assert.Equal(Uri.UriSchemeHttps, link.NavigateUri.Scheme);
            Assert.Equal("www.unity3d.com", link.NavigateUri.Host);
        });
    }

    [Fact]
    public void SetText_TrimsTrailingPunctuationFromUrl()
    {
        RunOnStaThread(() =>
        {
            var textBlock = new TextBlock();

            LinkedTextBehavior.SetText(textBlock, "详见 https://example.com/path，谢谢");

            var link = Assert.Single(textBlock.Inlines.OfType<Hyperlink>());
            Assert.Equal("/path", link.NavigateUri.AbsolutePath);

            var lastRun = textBlock.Inlines.OfType<Run>().Last();
            Assert.Contains("谢谢", lastRun.Text, StringComparison.Ordinal);
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
