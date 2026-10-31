using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.App.Controls;

/// <summary>
/// 轻量 WPF 3D 视口：把静态模型场景转换为 MeshGeometry3D，
/// 提供轨道旋转、平移、缩放与视角重置。
/// </summary>
public partial class ModelViewport : UserControl
{
    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(
        nameof(Scene),
        typeof(ModelPreviewScene),
        typeof(ModelViewport),
        new PropertyMetadata(null, OnSceneChanged));

    private const double OrbitDegreesPerPixel = 0.35;
    private const double MinPitch = -85;
    private const double MaxPitch = 85;

    private readonly PerspectiveCamera _camera = new()
    {
        FieldOfView = 45,
        UpDirection = new Vector3D(0, 1, 0)
    };

    private Vector3D _target;
    private double _yaw = 35;
    private double _pitch = 20;
    private double _distance = 4;
    private double _radius = 1;
    private Point _lastMousePosition;
    private bool _isOrbiting;
    private bool _isPanning;

    public ModelViewport()
    {
        InitializeComponent();
        Viewport.Camera = _camera;
        Loaded += (_, _) => UpdateCamera();
        SizeChanged += (_, _) => UpdateCamera();
    }

    public ModelPreviewScene? Scene
    {
        get => (ModelPreviewScene?)GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>恢复模型加载完成时的默认观察角度。</summary>
    public void ResetCamera()
    {
        _yaw = 35;
        _pitch = 20;
        _target = new Vector3D(0, 0, 0);
        _distance = CalculateFitDistance();
        UpdateCamera();
    }

    private static void OnSceneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModelViewport viewport)
        {
            viewport.BuildScene(e.NewValue as ModelPreviewScene);
            viewport.ResetCamera();
        }
    }

    private void BuildScene(ModelPreviewScene? scene)
    {
        Viewport.Children.Clear();
        _radius = 1;

        if (scene is null || scene.Meshes.Length == 0)
        {
            return;
        }

        var (center, radius) = CalculateBounds(scene);
        _radius = Math.Max(radius, 0.001);

        var group = new Model3DGroup();
        group.Children.Add(new AmbientLight(Color.FromRgb(42, 46, 56)));
        group.Children.Add(new DirectionalLight(
            Color.FromRgb(225, 220, 210),
            new Vector3D(-0.55, -0.9, -0.65)));
        group.Children.Add(new DirectionalLight(
            Color.FromRgb(95, 115, 160),
            new Vector3D(0.7, 0.25, 0.75)));

        var centerTransform = new TranslateTransform3D(-center.X, -center.Y, -center.Z);

        foreach (var mesh in scene.Meshes)
        {
            var geometry = new MeshGeometry3D
            {
                Positions = CreatePositions(mesh.Positions),
                TriangleIndices = CreateIndices(mesh.Indices)
            };

            if (mesh.Normals is not null && mesh.Normals.Length == mesh.Positions.Length)
            {
                geometry.Normals = CreateNormals(mesh.Normals);
            }

            geometry.Freeze();

            var color = Color.FromRgb(mesh.R, mesh.G, mesh.B);
            var diffuse = new DiffuseMaterial(new SolidColorBrush(color));
            diffuse.Freeze();

            var highlights = new MaterialGroup();
            highlights.Children.Add(diffuse);
            var specular = new SpecularMaterial(
                new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                24);
            highlights.Children.Add(specular);
            highlights.Freeze();

            group.Children.Add(new GeometryModel3D
            {
                Geometry = geometry,
                Material = highlights,
                BackMaterial = diffuse,
                Transform = centerTransform
            });
        }

        Viewport.Children.Add(new ModelVisual3D { Content = group });
    }

    private static (Point3D Center, double Radius) CalculateBounds(ModelPreviewScene scene)
    {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var minZ = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        var maxZ = double.MinValue;

        foreach (var mesh in scene.Meshes)
        {
            var positions = mesh.Positions;
            for (var i = 0; i + 2 < positions.Length; i += 3)
            {
                var x = positions[i];
                var y = positions[i + 1];
                var z = positions[i + 2];
                if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                minZ = Math.Min(minZ, z);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                maxZ = Math.Max(maxZ, z);
            }
        }

        if (minX == double.MaxValue)
        {
            return (new Point3D(0, 0, 0), 1);
        }

        var center = new Point3D(
            (minX + maxX) / 2,
            (minY + maxY) / 2,
            (minZ + maxZ) / 2);

        var dx = maxX - minX;
        var dy = maxY - minY;
        var dz = maxZ - minZ;
        var radius = Math.Sqrt(dx * dx + dy * dy + dz * dz) / 2;
        return (center, radius);
    }

    private static Point3DCollection CreatePositions(float[] positions)
    {
        var collection = new Point3DCollection(positions.Length / 3);
        for (var i = 0; i + 2 < positions.Length; i += 3)
        {
            collection.Add(new Point3D(positions[i], positions[i + 1], positions[i + 2]));
        }

        return collection;
    }

    private static Int32Collection CreateIndices(int[] indices)
    {
        var collection = new Int32Collection(indices.Length);
        foreach (var index in indices)
        {
            collection.Add(index);
        }

        return collection;
    }

    private static Vector3DCollection CreateNormals(float[] normals)
    {
        var collection = new Vector3DCollection(normals.Length / 3);
        for (var i = 0; i + 2 < normals.Length; i += 3)
        {
            collection.Add(new Vector3D(normals[i], normals[i + 1], normals[i + 2]));
        }

        return collection;
    }

    private double CalculateFitDistance()
    {
        var halfFov = _camera.FieldOfView * Math.PI / 360;
        return _radius / Math.Tan(halfFov) * 1.6;
    }

    private void UpdateCamera()
    {
        var minDistance = Math.Max(_radius * 0.02, 0.0001);
        var maxDistance = Math.Max(_radius * 100, minDistance * 2);
        _distance = Math.Clamp(_distance, minDistance, maxDistance);

        var yawRadians = _yaw * Math.PI / 180;
        var pitchRadians = _pitch * Math.PI / 180;
        var offset = new Vector3D(
            Math.Sin(yawRadians) * Math.Cos(pitchRadians),
            Math.Sin(pitchRadians),
            Math.Cos(yawRadians) * Math.Cos(pitchRadians));

        var cameraOffset = _target + offset * _distance;
        _camera.Position = new Point3D(cameraOffset.X, cameraOffset.Y, cameraOffset.Z);
        _camera.LookDirection = -offset;
        _camera.UpDirection = new Vector3D(0, 1, 0);
        _camera.NearPlaneDistance = Math.Max(_radius * 0.0005, 0.0001);
        _camera.FarPlaneDistance = Math.Max(
            _camera.NearPlaneDistance * 1000,
            _distance + _radius * 12 + 1);
    }

    private void OnViewportMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isOrbiting = true;
        _lastMousePosition = e.GetPosition(this);
        CaptureMouse();
        Focus();
        e.Handled = true;
    }

    private void OnViewportMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            ResetCamera();
            e.Handled = true;
        }
    }

    private void OnViewportMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isPanning = true;
        _lastMousePosition = e.GetPosition(this);
        CaptureMouse();
        Focus();
        e.Handled = true;
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isOrbiting && !_isPanning)
        {
            return;
        }

        var position = e.GetPosition(this);
        var deltaX = position.X - _lastMousePosition.X;
        var deltaY = position.Y - _lastMousePosition.Y;
        _lastMousePosition = position;

        if (_isOrbiting)
        {
            _yaw += deltaX * OrbitDegreesPerPixel;
            _pitch = Math.Clamp(_pitch - deltaY * OrbitDegreesPerPixel, MinPitch, MaxPitch);
        }
        else
        {
            var lookDirection = _camera.LookDirection;
            lookDirection.Normalize();

            var right = Vector3D.CrossProduct(lookDirection, _camera.UpDirection);
            right.Normalize();

            var up = Vector3D.CrossProduct(right, lookDirection);
            up.Normalize();

            var worldPerPixel = 2 * _distance * Math.Tan(_camera.FieldOfView * Math.PI / 360) /
                                Math.Max(ActualHeight, 1);
            _target += (-right * deltaX + up * deltaY) * worldPerPixel;
        }

        UpdateCamera();
        e.Handled = true;
    }

    private void OnViewportMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isOrbiting)
        {
            return;
        }

        _isOrbiting = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private void OnViewportMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private void OnViewportLostMouseCapture(object sender, MouseEventArgs e)
    {
        _isOrbiting = false;
        _isPanning = false;
    }

    private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _distance *= e.Delta > 0 ? 0.88 : 1.14;
        UpdateCamera();
        e.Handled = true;
    }
}
