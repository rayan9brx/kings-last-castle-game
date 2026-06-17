using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KingsLastCastle.Rendering;

public sealed class PrimitiveRenderer : IDisposable
{

    //  Diese Klasse zeichnet einfache 3D-Formen direkt aus Dreiecken, ohne externe Modelle.
    private readonly GraphicsDevice _graphicsDevice;
    private readonly BasicEffect _effect;

    public PrimitiveRenderer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _effect = new BasicEffect(graphicsDevice)
        {
            VertexColorEnabled = true,
            LightingEnabled = false
        };
    }

    public void Begin(Matrix view, Matrix projection)
    {
        // Vor dem Zeichnen bekommt der Renderer die aktuelle Kamera und Projektion aus dem Spiel.
        _effect.View = view;
        _effect.Projection = projection;
        _effect.World = Matrix.Identity;
    }

    public void DrawCylinder(Vector3 center, float radius, float height, int sides, Color color)
    {
        // Hier wird aus Kreisringen ein sauberer Zylinder für Boden, Baumstämme, Pads und Projektile gebaut.
        sides = Math.Max(8, sides);
        var topCenter = center + Vector3.Up * (height * 0.5f);
        var bottomCenter = center - Vector3.Up * (height * 0.5f);
        var vertices = new List<VertexPositionColor>(sides * 12);

        for (var i = 0; i < sides; i++)
        {
            // Jedes Segment erzeugt Deckel, Boden und Seitenfläche des Zylinders.
            var a0 = MathHelper.TwoPi * i / sides;
            var a1 = MathHelper.TwoPi * (i + 1) / sides;
            var p0 = new Vector3(MathF.Cos(a0) * radius, 0f, MathF.Sin(a0) * radius);
            var p1 = new Vector3(MathF.Cos(a1) * radius, 0f, MathF.Sin(a1) * radius);
            var top0 = topCenter + p0;
            var top1 = topCenter + p1;
            var bottom0 = bottomCenter + p0;
            var bottom1 = bottomCenter + p1;
            var sideShade = 0.78f + 0.16f * MathF.Max(0f, MathF.Sin((a0 + a1) * 0.5f));

            AddTriangle(vertices, topCenter, top0, top1, Shade(color, 1.1f));
            AddTriangle(vertices, bottomCenter, bottom1, bottom0, Shade(color, 0.62f));
            AddTriangle(vertices, top0, bottom0, bottom1, Shade(color, sideShade));
            AddTriangle(vertices, top0, bottom1, top1, Shade(color, sideShade));
        }

        DrawTriangles(vertices);
    }

    public void DrawCone(Vector3 baseCenter, float radius, Vector3 tip, int sides, Color color)
    {
        // Hier wird der Kegel für Berge, Bäume und Felsen als vereinfachte 3D-Form verwendet.
        sides = Math.Max(8, sides);
        var vertices = new List<VertexPositionColor>(sides * 6);

        for (var i = 0; i < sides; i++)
        {
            //  Pro Segment entsteht ein Bodendreieck und ein Seitendreieck bis zur Spitze.
            var a0 = MathHelper.TwoPi * i / sides;
            var a1 = MathHelper.TwoPi * (i + 1) / sides;
            var p0 = baseCenter + new Vector3(MathF.Cos(a0) * radius, 0f, MathF.Sin(a0) * radius);
            var p1 = baseCenter + new Vector3(MathF.Cos(a1) * radius, 0f, MathF.Sin(a1) * radius);
            var sideShade = 0.74f + 0.16f * MathF.Max(0f, MathF.Sin((a0 + a1) * 0.5f));

            AddTriangle(vertices, baseCenter, p1, p0, Shade(color, 0.62f));
            AddTriangle(vertices, p0, p1, tip, Shade(color, sideShade));
        }

        DrawTriangles(vertices);
    }

    public void DrawPath(IReadOnlyList<Vector3> waypoints, float width, Color color, float yOffset = 0.055f)
    {
        // Hier wird der Weg als breite Geometrie mit Endkappen aufgebaut, damit er aus der Kamera gut lesbar ist.
        for (var i = 0; i < waypoints.Count - 1; i++)
        {
            var start = waypoints[i] + new Vector3(0f, yOffset, 0f);
            var end = waypoints[i + 1] + new Vector3(0f, yOffset, 0f);
            var direction = end - start;
            direction.Y = 0f;
            if (direction.LengthSquared() < 0.0001f)
            {
                continue;
            }

            direction.Normalize();
            var perp = new Vector3(-direction.Z, 0f, direction.X) * (width * 0.5f);
            //  Das Pfadsegment wird als breites Quad zwischen zwei Waypoints gezeichnet.
            DrawQuad(start + perp, end + perp, end - perp, start - perp, color);
            DrawCylinder(start, width * 0.5f, 0.04f, 14, color);
        }

        if (waypoints.Count > 0)
        {
            DrawCylinder(waypoints[^1] + new Vector3(0f, yOffset, 0f), width * 0.5f, 0.04f, 14, color);
        }
    }

    public void DrawQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        //  Ein Quad wird in zwei Dreiecke zerlegt, weil die Grafikkarte Dreiecke zeichnet.
        DrawTriangles(new[]
        {
            new VertexPositionColor(a, color),
            new VertexPositionColor(b, color),
            new VertexPositionColor(c, color),
            new VertexPositionColor(a, color),
            new VertexPositionColor(c, color),
            new VertexPositionColor(d, color)
        });
    }

    public void DrawBeam(Vector3 start, Vector3 end, float width, Color color)
    {
        // Hier wird der Laserstrahl als flache Doppel-Quad-Lösung gerendert, damit er von allen Seiten sichtbar bleibt.
        var direction = end - start;
        if (direction.LengthSquared() < 0.0001f)
        {
            return;
        }

        direction.Normalize();
        var side = Vector3.Cross(direction, Vector3.Up);
        if (side.LengthSquared() < 0.0001f)
        {
            side = Vector3.Right;
        }

        side.Normalize();
        side *= width;
        var up = Vector3.Up * width;

        //  Zwei gekreuzte Quads machen den Strahl aus verschiedenen Kamerawinkeln sichtbar.
        DrawQuad(start - side, start + side, end + side, end - side, color);
        DrawQuad(start - up, start + up, end + up, end - up, color);
    }

    public void Dispose() => _effect.Dispose();

    private void DrawTriangles(IReadOnlyList<VertexPositionColor> vertices)
    {
        // Hier werden alle Hilfsformen am Ende in einem gemeinsamen Dreiecks-Drawcall gesammelt.
        DrawTriangleArray(vertices as VertexPositionColor[] ?? vertices.ToArray());
    }

    private void DrawTriangleArray(VertexPositionColor[] vertices)
    {
        //  Hier werden die vorbereiteten Dreiecke wirklich an die GPU geschickt.
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length / 3);
        }
    }

    private static void AddTriangle(List<VertexPositionColor> vertices, Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        vertices.Add(new VertexPositionColor(a, color));
        vertices.Add(new VertexPositionColor(b, color));
        vertices.Add(new VertexPositionColor(c, color));
    }

    private static Color Shade(Color color, float factor)
    {
        return new Color(
            (byte)Math.Clamp((int)(color.R * factor), 0, 255),
            (byte)Math.Clamp((int)(color.G * factor), 0, 255),
            (byte)Math.Clamp((int)(color.B * factor), 0, 255),
            color.A);
    }
}
