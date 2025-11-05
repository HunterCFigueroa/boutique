using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf.SharpDX;
using HelixToolkit.Wpf.SharpDX.Model;
using RequiemGlamPatcher.Models;
using Serilog;
using Color = System.Windows.Media.Color;
using Vector3 = System.Numerics.Vector3;
using MeshGeometry3D = HelixToolkit.Wpf.SharpDX.MeshGeometry3D;
using Material = HelixToolkit.Wpf.SharpDX.Material;
using PerspectiveCamera = HelixToolkit.Wpf.SharpDX.PerspectiveCamera;
using Color4 = SharpDX.Color4;
using SharpDXVector2 = SharpDX.Vector2;
using SharpDXVector3 = SharpDX.Vector3;

namespace RequiemGlamPatcher.Views;

public partial class OutfitPreviewWindow : Window
{
    private readonly ArmorPreviewScene _scene;
    private readonly GroupModel3D _meshGroup = new();
    private readonly DefaultEffectsManager _effectsManager = new();
    private PerspectiveCamera? _initialCamera;

    public OutfitPreviewWindow(ArmorPreviewScene scene)
    {
        InitializeComponent();
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));

        InitializeViewport();
        BuildScene();
    }

    private void InitializeViewport()
    {
        PreviewViewport.EffectsManager = _effectsManager;
        PreviewViewport.Items.Clear();

        PreviewViewport.Items.Add(new AmbientLight3D
        {
            Color = ToMediaColor(new Color4(0.35f, 0.35f, 0.35f, 1f))
        });
        PreviewViewport.Items.Add(new DirectionalLight3D
        {
            Color = ToMediaColor(new Color4(0.85f, 0.85f, 0.85f, 1f)),
            Direction = new Vector3D(-0.3f, -0.5f, -0.7f)
        });
        PreviewViewport.Items.Add(new DirectionalLight3D
        {
            Color = ToMediaColor(new Color4(0.65f, 0.65f, 0.65f, 1f)),
            Direction = new Vector3D(0.45f, 0.1f, -0.35f)
        });
        PreviewViewport.Items.Add(new DirectionalLight3D
        {
            Color = ToMediaColor(new Color4(0.45f, 0.45f, 0.45f, 1f)),
            Direction = new Vector3D(-0.2f, 0.4f, -0.5f)
        });

        PreviewViewport.Items.Add(_meshGroup);
    }

    private void BuildScene()
    {
        GenderLabel.Text = $"Gender: {_scene.Gender}";
        if (_scene.MissingAssets.Any())
        {
            MissingAssetsPanel.Visibility = Visibility.Visible;
            MissingAssetsList.ItemsSource = _scene.MissingAssets;
        }
        else
        {
            MissingAssetsPanel.Visibility = Visibility.Collapsed;
        }

        var evaluatedMeshes = EvaluateMeshes(out var center, out var radius);
        _meshGroup.Children.Clear();

        if (evaluatedMeshes.Count == 0)
        {
            MissingAssetsPanel.Visibility = Visibility.Visible;
            MissingAssetsList.ItemsSource = new[] { "No geometry available to render." };
            return;
        }

        foreach (var evaluated in evaluatedMeshes)
        {
            var geometry = CreateGeometry(evaluated, center);
            if (geometry == null)
                continue;

            var material = CreateMaterialForMesh(evaluated.Shape);
            var model = new MeshGeometryModel3D
            {
                Geometry = geometry,
                Material = material,
                CullMode = SharpDX.Direct3D11.CullMode.None,
                IsHitTestVisible = false
            };

            _meshGroup.Children.Add(model);
        }

        ConfigureCamera(radius);
        PreviewViewport.InvalidateRender();
    }

    private List<EvaluatedMesh> EvaluateMeshes(out Vector3 center, out float radius)
    {
        var evaluatedMeshes = new List<EvaluatedMesh>();
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var mesh in _scene.Meshes)
        {
            var transformedVertices = new List<Vector3>(mesh.Vertices.Count);
            var transformedNormals = new List<Vector3>(mesh.Normals.Count);

            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                var world = Vector3.Transform(mesh.Vertices[i], mesh.Transform);
                transformedVertices.Add(world);

                min = Vector3.Min(min, world);
                max = Vector3.Max(max, world);
            }

            for (int i = 0; i < mesh.Normals.Count; i++)
            {
                var normal = Vector3.TransformNormal(mesh.Normals[i], mesh.Transform);
                if (normal != Vector3.Zero)
                    normal = Vector3.Normalize(normal);
                transformedNormals.Add(normal);
            }

            evaluatedMeshes.Add(new EvaluatedMesh(mesh, transformedVertices, transformedNormals));
        }

        if (evaluatedMeshes.Count == 0)
        {
            center = Vector3.Zero;
            radius = 1f;
            return evaluatedMeshes;
        }

        center = (min + max) * 0.5f;
        var extents = max - min;
        radius = Math.Max(Math.Max(extents.X, extents.Y), extents.Z);
        if (radius <= 0)
            radius = 1f;
        radius *= 0.5f;

        return evaluatedMeshes;
    }

    private static MeshGeometry3D? CreateGeometry(EvaluatedMesh evaluated, Vector3 center)
    {
        if (evaluated.Vertices.Count == 0 || evaluated.Shape.Indices.Count == 0)
            return null;

        var positions = new Vector3Collection(
            evaluated.Vertices.Select(v => new SharpDXVector3(v.X - center.X, v.Y - center.Y, v.Z - center.Z)));

        var geometry = new MeshGeometry3D
        {
            Positions = positions,
            Indices = new IntCollection(evaluated.Shape.Indices)
        };

        if (evaluated.Normals.Count == evaluated.Vertices.Count)
        {
            geometry.Normals = new Vector3Collection(
                evaluated.Normals.Select(n => new SharpDXVector3(n.X, n.Y, n.Z)));
        }

        var uvs = evaluated.Shape.TextureCoordinates;
        if (uvs != null && uvs.Count == evaluated.Vertices.Count)
        {
            geometry.TextureCoordinates = new Vector2Collection(
                uvs.Select(tc => new SharpDXVector2(tc.X, tc.Y)));
        }
        else if (uvs != null)
        {
            Log.Warning("Texture coordinate count {UvCount} does not match vertex count {VertexCount} for mesh {MeshName}",
                uvs.Count, evaluated.Vertices.Count, evaluated.Shape.Name);
        }

        geometry.UpdateBounds();
        return geometry;
    }

    private void ConfigureCamera(float radius)
    {
        var baseDistance = Math.Max(radius * 3.0f, 150.0f);
        var height = baseDistance * 0.6;

        var camera = new PerspectiveCamera
        {
            FieldOfView = 45,
            Position = new Point3D(0, -baseDistance, height),
            LookDirection = new Vector3D(0, baseDistance, -height),
            UpDirection = new Vector3D(0, 0, 1)
        };

        PreviewViewport.Camera = camera;
        _initialCamera = (PerspectiveCamera)camera.Clone();
    }

    private static Material CreateMaterialForMesh(PreviewMeshShape mesh)
    {
        var material = TryCreateTextureMaterial(mesh);
        if (material != null)
            return material;

        var fallbackColor = GetFallbackColor(mesh);
        var diffuse = ToColor4(fallbackColor);
        return new PhongMaterial
        {
            DiffuseColor = diffuse,
            AmbientColor = new Color4(diffuse.Red * 0.4f, diffuse.Green * 0.4f, diffuse.Blue * 0.4f, 1f),
            SpecularColor = new Color4(0.15f, 0.15f, 0.15f, 1f),
            SpecularShininess = 16f
        };
    }

    private static Material? TryCreateTextureMaterial(PreviewMeshShape mesh)
    {
        var texturePath = mesh.DiffuseTexturePath;
        if (string.IsNullOrWhiteSpace(texturePath))
        {
            Log.Debug("No diffuse texture provided for mesh {MeshName}", mesh.Name);
            return null;
        }

        if (!File.Exists(texturePath))
        {
            Log.Warning("Diffuse texture path {TexturePath} does not exist on disk.", texturePath);
            return null;
        }

        try
        {
            var material = new PhongMaterial
            {
                DiffuseMap = new TextureModel(texturePath),
                DiffuseColor = new Color4(1f, 1f, 1f, 1f),
                AmbientColor = new Color4(0.35f, 0.35f, 0.35f, 1f),
                SpecularColor = new Color4(0.18f, 0.18f, 0.18f, 1f),
                SpecularShininess = 32f
            };

            Log.Debug("Successfully created textured material for {TexturePath}", texturePath);
            return material;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to create textured material for {TexturePath}", texturePath);
            return null;
        }
    }

    private static Color GetFallbackColor(PreviewMeshShape mesh)
    {
        if (mesh.Name.Contains("Base Body", StringComparison.OrdinalIgnoreCase))
            return Color.FromRgb(200, 200, 200);

        var hash = mesh.SourcePath.GetHashCode(StringComparison.OrdinalIgnoreCase);
        byte r = (byte)((hash >> 16) & 0xFF);
        byte g = (byte)((hash >> 8) & 0xFF);
        byte b = (byte)(hash & 0xFF);

        const double scale = 0.6;
        const byte min = 70;
        r = (byte)(min + r * scale);
        g = (byte)(min + g * scale);
        b = (byte)(min + b * scale);

        return Color.FromRgb(r, g, b);
    }

    private static System.Windows.Media.Color ToMediaColor(Color4 color)
    {
        return System.Windows.Media.Color.FromScRgb(color.Alpha, color.Red, color.Green, color.Blue);
    }

    private static Color4 ToColor4(Color color)
    {
        return new Color4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
    }

    private void OnResetView(object sender, RoutedEventArgs e)
    {
        if (_initialCamera == null)
            return;

        if (PreviewViewport.Camera is PerspectiveCamera activeCamera)
        {
            activeCamera.Position = _initialCamera.Position;
            activeCamera.LookDirection = _initialCamera.LookDirection;
            activeCamera.UpDirection = _initialCamera.UpDirection;
            activeCamera.FieldOfView = _initialCamera.FieldOfView;
        }
        else
        {
            PreviewViewport.Camera = (PerspectiveCamera)_initialCamera.Clone();
        }
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _meshGroup.Children.Clear();
        PreviewViewport.Items.Clear();
        _effectsManager.Dispose();
    }

    private record EvaluatedMesh(PreviewMeshShape Shape, IReadOnlyList<Vector3> Vertices, IReadOnlyList<Vector3> Normals);
}
