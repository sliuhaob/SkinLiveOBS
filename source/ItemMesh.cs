using System;
using System.Drawing;

namespace SkinLive
{
    // A generated Minecraft-style item is one texel thick. Both broad faces and
    // the exposed edges of the opaque pixels follow the same right-arm joint.
    internal sealed class ItemMesh
    {
        internal const int First = SkinMap.Mouth + 1;
        internal const int DefaultGripX = 3, DefaultGripY = 13, CanvasGrip = 23;
        internal static readonly ItemMesh Vanilla = new ItemMesh(16, 0, 0);
        internal static readonly ItemMesh Extended = new ItemMesh(48, DefaultGripX - CanvasGrip, DefaultGripY - CanvasGrip);
        static readonly Rectangle surface = new Rectangle(119, 903, 96, 96);
        internal readonly int Pixels;
        readonly int Scale, originX, originY;
        readonly int[] cutX, cutY;
        readonly int patches, firstEdge;
        internal int Count { get { return firstEdge + 2 * (Pixels + 1); } }
        internal int TopMargin { get { return Pixels == 16 ? 0 : 40; } }
        internal int BottomMargin { get { return Pixels == 16 ? 0 : 104; } }

        ItemMesh(int pixels, int originX, int originY)
        {
            Pixels = pixels; Scale = 96 / pixels;
            this.originX = originX; this.originY = originY;
            // Keep the four original palm-area patches even when more canvas surrounds the fist.
            cutX = pixels == 16 ? new[] { 0, 6, 16 } : new[] { 0, -originX, 6 - originX, 16 - originX, pixels };
            cutY = pixels == 16 ? new[] { 0, 10, 16 } : new[] { 0, -originY, 10 - originY, 16 - originY, pixels };
            patches = (cutX.Length - 1) * (cutY.Length - 1); firstEdge = 2 * patches;
        }
        internal static ItemMesh ForSprite(int[] sprite)
        {
            if (sprite == null || sprite.Length == 16 * 16) return Vanilla;
            if (sprite.Length == Extended.Pixels * Extended.Pixels) return Extended;
            throw new ArgumentException("手持物品的像素画布尺寸不正确。");
        }

        Rectangle Patch(int index)
        {
            // Separate the part inside the fist so the Live2D draw order can
            // place the grip behind the sleeve independently of the tool head.
            int x = index % (cutX.Length - 1), y = index / (cutX.Length - 1);
            return Rectangle.FromLTRB(cutX[x], cutY[y], cutX[x + 1], cutY[y + 1]);
        }

        internal Rectangle Tile(int index)
        {
            if (index < firstEdge) {
                Rectangle p = Patch(index % patches);
                return new Rectangle(surface.X + p.X * Scale, surface.Y + p.Y * Scale,
                    p.Width * Scale, p.Height * Scale);
            }
            int strip = index - firstEdge;
            return new Rectangle(strip <= Pixels ? 231 : 343, 903 + strip % (Pixels + 1) * Scale,
                surface.Width, Math.Max(1, Scale - 2));
        }

        internal string Name(int index)
        {
            if (index < firstEdge) return "RightItem" + (index < patches ? "Front" : "Back") + index % patches;
            int strip = index - firstEdge;
            return "RightItemEdge" + (strip <= Pixels ? "Row" : "Column") + (strip % (Pixels + 1));
        }

        internal int[] ReadSprite(Bitmap atlas)
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

        internal void Paint(int[] atlas, int[] sprite)
        {
            if (sprite != null && sprite.Length != Pixels * Pixels)
                throw new ArgumentException("手持物品的像素画布尺寸不正确。");
            // These three slots are reserved for items, including texture gutters.
            for (int y = 896; y < 1008; y++)
                Array.Clear(atlas, y * SkinMap.Size + 112, 336);
            if (sprite == null) return;
            for (int y = 0; y < surface.Height; y++)
                for (int x = 0; x < surface.Width; x++)
                    atlas[(surface.Y + y) * SkinMap.Size + surface.X + x] = sprite[y / Scale * Pixels + x / Scale];
            for (int k = 0; k <= Pixels; k++) {
                Rectangle row = Tile(firstEdge + k), column = Tile(firstEdge + Pixels + 1 + k);
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
            const double DisplayScale = .85;
            static readonly double roll = 55 * Math.PI / 180;
            static readonly double rollCos = Math.Cos(roll), rollSin = Math.Sin(roll);
            readonly Mesh.Transform transform;
            readonly Limb arm;
            readonly double lift;
            readonly int originX, originY;
            public Grip(Pose pose, int originX, int originY)
            {
                this.originX = originX; this.originY = originY;
                arm = pose.Joints[2];
                transform = new Mesh.Transform(arm);
                lift = pose.RenderLift;
            }
            public Vec Point(double u, double v, double thickness)
            {
                // Vanilla generated item: center the 16x16 sprite, with image V down.
                double x = (u + originX - 8) * DisplayScale;
                double y = (8 - (v + originY)) * DisplayScale;
                double z = thickness * DisplayScale;

                // handheld.json thirdperson_righthand in model pixels:
                // translate(0, 4, .5) * rotateY(-90) * rotateZ(55).
                double itemX = -z;
                double itemY = 4 + rollSin * x + rollCos * y;
                double itemZ = .5 + rollCos * x - rollSin * y;

                // Adult right hand after translateToHand:
                // rotateX(-90) * rotateY(180) * translate(1, 2, -10).
                return transform.Project(arm, -(1 + itemX), 10 - itemZ, -(2 + itemY), lift);
            }
        }

        internal Rectangle HandBounds(Pose pose, int[] sprite)
        {
            Limb arm = pose.Joints[2];
            var armTransform = new Mesh.Transform(arm);
            Vec shoulder = armTransform.Project(arm, -1, 0, 0, pose.RenderLift);
            Vec wrist = armTransform.Project(arm, -1, 8, 0, pose.RenderLift);
            double left = Math.Min(shoulder.X, wrist.X) - 24, right = Math.Max(shoulder.X, wrist.X) + 24;
            double top = Math.Min(shoulder.Y, wrist.Y) - 24, bottom = Math.Max(shoulder.Y, wrist.Y) + 24;
            var grip = new Grip(pose, originX, originY);
            for (int i = 0; i < sprite.Length; i++) {
                if ((uint)sprite[i] >> 24 == 0) continue;
                Vec point = grip.Point(i % Pixels + .5, i / Pixels + .5, 0);
                left = Math.Min(left, point.X); right = Math.Max(right, point.X);
                top = Math.Min(top, point.Y); bottom = Math.Max(bottom, point.Y);
            }
            return Rectangle.FromLTRB((int)Math.Floor(left)-17, (int)Math.Floor(top)-17,
                (int)Math.Ceiling(right)+17, (int)Math.Ceiling(bottom)+17);
        }

        internal void Evaluate(Face[] faces, Pose pose)
        {
            var grip = new Grip(pose, originX, originY);
            for (int i = 0; i < Count; i++) {
                Face face = faces[First + i];
                if (i < firstEdge) {
                    Rectangle p = Patch(i % patches);
                    double n = i < patches ? .5 : -.5;
                    face.V[0] = grip.Point(p.Left, p.Top, n);
                    face.V[1] = grip.Point(p.Right, p.Top, n);
                    face.V[2] = grip.Point(p.Right, p.Bottom, n);
                    face.V[3] = grip.Point(p.Left, p.Bottom, n);
                } else {
                    int k = (i - firstEdge) % (Pixels + 1);
                    bool row = i - firstEdge <= Pixels;
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
