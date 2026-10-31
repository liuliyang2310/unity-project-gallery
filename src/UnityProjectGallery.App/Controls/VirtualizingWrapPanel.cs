using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace UnityProjectGallery.App.Controls;

/// <summary>
/// 固定尺寸卡片的虚拟化换行面板。仅测量并生成视口内的卡片，
/// 配合 ListBox 的滚动宿主使用，避免一次解码全部封面缩略图。
/// </summary>
public sealed class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(330d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(336d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemMarginProperty = DependencyProperty.Register(
        nameof(ItemMargin),
        typeof(Thickness),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(new Thickness(0, 0, 22, 22), FrameworkPropertyMetadataOptions.AffectsMeasure));

    private Size _extent;
    private Size _viewport;
    private Point _offset;
    private int _columns = 1;

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public Thickness ItemMargin
    {
        get => (Thickness)GetValue(ItemMarginProperty);
        set => SetValue(ItemMarginProperty, value);
    }

    public bool CanHorizontallyScroll { get; set; }

    public bool CanVerticallyScroll { get; set; } = true;

    public double ExtentWidth => _extent.Width;

    public double ExtentHeight => _extent.Height;

    public double ViewportWidth => _viewport.Width;

    public double ViewportHeight => _viewport.Height;

    public double HorizontalOffset => _offset.X;

    public double VerticalOffset => _offset.Y;

    public ScrollViewer? ScrollOwner { get; set; }

    private double SlotWidth => ItemWidth + ItemMargin.Left + ItemMargin.Right;

    private double SlotHeight => ItemHeight + ItemMargin.Top + ItemMargin.Bottom;

    protected override Size MeasureOverride(Size availableSize)
    {
        var itemsControl = ItemsControl.GetItemsOwner(this);
        var itemCount = itemsControl?.Items.Count ?? 0;

        var viewportWidth = GetViewportDimension(availableSize.Width, ScrollOwner?.ViewportWidth);
        var viewportHeight = GetViewportDimension(availableSize.Height, ScrollOwner?.ViewportHeight);
        viewportWidth = Math.Max(SlotWidth, viewportWidth);
        viewportHeight = Math.Max(SlotHeight, viewportHeight);

        UpdateViewport(new Size(viewportWidth, viewportHeight));

        _columns = Math.Max(1, (int)Math.Floor(viewportWidth / SlotWidth));
        var rows = itemCount == 0
            ? 0
            : (int)Math.Ceiling(itemCount / (double)_columns);

        UpdateExtent(new Size(
            Math.Max(viewportWidth, _columns * SlotWidth),
            rows * SlotHeight));

        CoerceOffsets();

        if (itemCount == 0)
        {
            RemoveAllChildren();
            return new Size(viewportWidth, viewportHeight);
        }

        var firstRow = (int)Math.Floor(_offset.Y / SlotHeight);
        var lastRow = (int)Math.Ceiling((_offset.Y + viewportHeight) / SlotHeight) - 1;
        var firstIndex = Math.Max(0, firstRow * _columns);
        var lastIndex = Math.Min(itemCount - 1, (lastRow + 1) * _columns - 1);

        RemoveChildrenOutsideRange(firstIndex, lastIndex);
        RealizeChildren(firstIndex, lastIndex);

        return new Size(viewportWidth, viewportHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var generator = ItemContainerGenerator;
        for (var childIndex = 0; childIndex < InternalChildren.Count; childIndex++)
        {
            var child = InternalChildren[childIndex];
            if (child is null)
            {
                continue;
            }

            var itemIndex = generator.IndexFromGeneratorPosition(
                new GeneratorPosition(childIndex, 0));
            if (itemIndex < 0)
            {
                continue;
            }

            var column = itemIndex % _columns;
            var row = itemIndex / _columns;
            child.Arrange(new Rect(
                column * SlotWidth,
                row * SlotHeight - _offset.Y,
                SlotWidth,
                SlotHeight));
        }

        return finalSize;
    }

    private void RealizeChildren(int firstIndex, int lastIndex)
    {
        var generator = ItemContainerGenerator;
        var startPosition = generator.GeneratorPositionFromIndex(firstIndex);
        var childIndex = startPosition.Offset == 0
            ? startPosition.Index
            : startPosition.Index + 1;

        using (generator.StartAt(startPosition, GeneratorDirection.Forward, true))
        {
            for (var itemIndex = firstIndex; itemIndex <= lastIndex; itemIndex++, childIndex++)
            {
                var child = (UIElement?)generator.GenerateNext(out var newlyRealized);
                if (child is null)
                {
                    continue;
                }

                if (newlyRealized)
                {
                    if (childIndex >= InternalChildren.Count)
                    {
                        AddInternalChild(child);
                    }
                    else
                    {
                        InsertInternalChild(childIndex, child);
                    }

                    generator.PrepareItemContainer(child);
                }

                child.Measure(new Size(SlotWidth, SlotHeight));
            }
        }
    }

    private void RemoveChildrenOutsideRange(int firstIndex, int lastIndex)
    {
        var generator = ItemContainerGenerator;
        for (var childIndex = InternalChildren.Count - 1; childIndex >= 0; childIndex--)
        {
            var itemIndex = generator.IndexFromGeneratorPosition(
                new GeneratorPosition(childIndex, 0));
            if (itemIndex >= firstIndex && itemIndex <= lastIndex)
            {
                continue;
            }

            generator.Remove(new GeneratorPosition(childIndex, 0), 1);
            RemoveInternalChildRange(childIndex, 1);
        }
    }

    private void RemoveAllChildren()
    {
        if (InternalChildren.Count == 0)
        {
            return;
        }

        var generator = ItemContainerGenerator;
        for (var childIndex = InternalChildren.Count - 1; childIndex >= 0; childIndex--)
        {
            generator.Remove(new GeneratorPosition(childIndex, 0), 1);
        }

        RemoveInternalChildRange(0, InternalChildren.Count);
    }

    private void UpdateViewport(Size viewport)
    {
        if (viewport == _viewport)
        {
            return;
        }

        _viewport = viewport;
        ScrollOwner?.InvalidateScrollInfo();
    }

    private void UpdateExtent(Size extent)
    {
        if (extent == _extent)
        {
            return;
        }

        _extent = extent;
        ScrollOwner?.InvalidateScrollInfo();
    }

    private void CoerceOffsets()
    {
        var maxVertical = Math.Max(0, _extent.Height - _viewport.Height);
        var vertical = Math.Min(maxVertical, Math.Max(0, _offset.Y));
        if (vertical != _offset.Y)
        {
            _offset.Y = vertical;
            ScrollOwner?.InvalidateScrollInfo();
        }

        if (_offset.X != 0)
        {
            _offset.X = 0;
            ScrollOwner?.InvalidateScrollInfo();
        }
    }

    private static double GetViewportDimension(double available, double? ownerViewport)
    {
        if (!double.IsInfinity(available) && available > 0)
        {
            return available;
        }

        return ownerViewport is > 0 ? ownerViewport.Value : 1;
    }

    private void SetVerticalOffsetCore(double offset)
    {
        var clamped = Math.Min(
            Math.Max(0, _extent.Height - _viewport.Height),
            Math.Max(0, offset));

        if (Math.Abs(clamped - _offset.Y) < 0.1)
        {
            return;
        }

        _offset.Y = clamped;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    public void LineUp() => SetVerticalOffsetCore(_offset.Y - 48);

    public void LineDown() => SetVerticalOffsetCore(_offset.Y + 48);

    public void LineLeft()
    {
    }

    public void LineRight()
    {
    }

    public void PageUp() => SetVerticalOffsetCore(_offset.Y - _viewport.Height);

    public void PageDown() => SetVerticalOffsetCore(_offset.Y + _viewport.Height);

    public void PageLeft()
    {
    }

    public void PageRight()
    {
    }

    public void MouseWheelUp() => SetVerticalOffsetCore(_offset.Y - 48 * 3);

    public void MouseWheelDown() => SetVerticalOffsetCore(_offset.Y + 48 * 3);

    public void MouseWheelLeft()
    {
    }

    public void MouseWheelRight()
    {
    }

    public void SetHorizontalOffset(double offset)
    {
    }

    public void SetVerticalOffset(double offset) => SetVerticalOffsetCore(offset);

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        if (visual is not UIElement element)
        {
            return rectangle;
        }

        var childIndex = InternalChildren.IndexOf(element);
        if (childIndex < 0)
        {
            return rectangle;
        }

        var itemIndex = ItemContainerGenerator.IndexFromGeneratorPosition(
            new GeneratorPosition(childIndex, 0));
        if (itemIndex < 0)
        {
            return rectangle;
        }

        var row = itemIndex / _columns;
        var itemTop = row * SlotHeight;
        var itemBottom = itemTop + SlotHeight;

        if (itemTop < _offset.Y)
        {
            SetVerticalOffsetCore(itemTop);
        }
        else if (itemBottom > _offset.Y + _viewport.Height)
        {
            SetVerticalOffsetCore(itemBottom - _viewport.Height);
        }

        return new Rect(
            rectangle.X,
            rectangle.Y + itemTop - _offset.Y,
            rectangle.Width,
            rectangle.Height);
    }
}
