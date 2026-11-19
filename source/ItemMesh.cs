using System;
using System.Drawing;

namespace SkinLive
{
    // A generated Minecraft-style item is one texel thick. Both broad faces and
    // the exposed edges of the opaque pixels follow the same right-arm joint.
    internal static class ItemMesh
    {
        internal const int First = SkinMap.Mouth + 1, Count = 8 + 2 * 17;
        const int Pixels = 16, Scale = 6;
        static readonly Rectangle surface = new Rectangle(119, 903, Pixels * Scale, Pixels * Scale);

        static Rectangle Patch(int index)
        {
            // Separate the part inside the fist so the Live2D draw order can
            // place the grip behind the sleeve independently of the tool head.
            int x = (index & 1) == 0 ? 0 : 6, y = (index & 2) == 0 ? 0 : 10;
            return Rectangle.FromLTRB(x, y, x == 0 ? 6 : Pixels, y == 0 ? 10 : Pixels);
        }

        internal static Rectangle Tile(int index)
        {
            if (index < 8) {
                Rectangle p = Patch(index % 4);
                return new Rectangle(surface.X + p.X * Scale, surface.Y + p.Y * Scale,
                    p.Width * Scale, p.Height * Scale);
            }
            int strip = index - 8;
            return new Rectangle(strip < 17 ? 231 : 343, 903 + strip % 17 * 6, Pixels * Scale, 4);
        }

        internal static string Name(int index)
        {
            if (index < 8) return "RightItem" + (index < 4 ? "Front" : "Back") + index % 4;
            return "RightItemEdge" + (index < 25 ? "Row" : "Column") + ((index - 8) % 17);
        }

        internal static int[] ReadSprite(Bitmap atlas)
        {
            var pixels = new int[Pixels * Pixels];
            for (int y = 0; y < Pixels; y++)
                for (int x = 0; x < Pixels; x++)
                    pixels[y * Pixels + x] = atlas.GetPixel(surface.X + x * Scale + Scale / 2,
                        surface.Y + y * Scale + Scale / 2).ToArgb();
            return pixels;
        }

        static int EdgeColor(int before, int after, int light, int dark)
        {
            bool first = ((uint)before >> 24) != 0, second = ((uint)after >> 24) != 0;
            if (first == second) return 0;
            return SkinMap.Shade(first ? before : after, first ? dark : light);
        }

        internal static void Paint(int[] atlas, int[] sprite)
        {
            if (sprite != null && sprite.Length != Pixels * Pixels)
                throw new ArgumentException("手持物品需要 16×16 的贴图。");
            // These three slots are reserved for items, including texture gutters.
            for (int y = 896; y < 1008; y++)
                Array.Clear(atlas, y * SkinMap.Size + 112, 336);
            if (sprite == null) return;
            for (int y = 0; y < surface.Height; y++)
                for (int x = 0; x < surface.Width; x++)
                    atlas[(surface.Y + y) * SkinMap.Size + surface.X + x] = sprite[y / Scale * Pixels + x / Scale];
            for (int k = 0; k <= Pixels; k++) {
                Rectangle row = Tile(8 + k), column = Tile(25 + k);
                for (int i = 0; i < Pixels; i++) {
                    int r = EdgeColor(k == 0 ? 0 : sprite[(k - 1) * Pixels + i],
                        k == Pixels ? 0 : sprite[k * Pixels + i], 92, 66);
                    int c = EdgeColor(k == 0 ? 0 : sprite[i * Pixels + k - 1],
                        k == Pixels ? 0 : sprite[i * Pixels + k], 80, 72);
                    for (int dy = 0; dy < row.Height; dy++)
                        for (int dx = 0; dx < Scale; dx++) {
                            atlas[(row.Y + dy) * SkinMap.Size + row.X + i * Scale + dx] = r;
                            atlas[(column.Y + dy) * SkinMap.Size + column.X + i * Scale + dx] = c;
                        }
                }
            }
        }

        struct Grip
        {
            readonly Mesh.Transform transform;
            readonly Limb arm;
            readonly double lift;
            public Grip(Pose pose)
            {
                arm = pose.Joints[2];
                transform = new Mesh.Transform(arm);
                lift = pose.Lift;
            }
            public Vec Point(double u, double v, double thickness)
            {
                // Sprite handle runs from lower left to upper right. The grip is
                // inside the fist. U and V span the item and N spans its thickness.
                const double scale = .71;
                u = (u - 4.5) * scale; v = (v - 12.5) * scale; thickness *= scale;
                return transform.Project(arm,
                    -1.45 - .934 * u + .212 * v + .287 * thickness,
                     9.00 + .341 * u + .767 * v + .545 * thickness,
                    -1.85 - .105 * u + .606 * v - .788 * thickness, lift);
            }
        }

        internal static void Evaluate(Face[] faces, Pose pose)
        {
            var grip = new Grip(pose);
            for (int i = 0; i < Count; i++) {
                Face face = faces[First + i];
                if (i < 8) {
                    Rectangle p = Patch(i % 4);
                    double n = i < 4 ? .5 : -.5;
                    face.V[0] = grip.Point(p.Left, p.Top, n);
                    face.V[1] = grip.Point(p.Right, p.Top, n);
                    face.V[2] = grip.Point(p.Right, p.Bottom, n);
                    face.V[3] = grip.Point(p.Left, p.Bottom, n);
                } else {
                    int k = (i - 8) % 17;
                    bool row = i < 25;
                    face.V[0] = grip.Point(row ? 0 : k, row ? k : 0, -.5);
                    face.V[1] = grip.Point(row ? Pixels : k, row ? k : Pixels, -.5);
                    face.V[2] = grip.Point(row ? Pixels : k, row ? k : Pixels, .5);
                    face.V[3] = grip.Point(row ? 0 : k, row ? k : 0, .5);
                }
                face.Depth = (face.V[0].Z + face.V[1].Z + face.V[2].Z + face.V[3].Z) * .25;
                // External Live2D renderers can view either side of these quads.
                face.Opacity = 1;
            }
        }
    }
}
