using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace SkinLive
{
    // The 16x16 source and the user's grip are portable. The atlas is built from them, never from a saved file path.
    internal sealed class CustomItem
    {
        public const int Size = 16, MaxName = 60;
        public readonly string Name, Id;
        public readonly int GripX, GripY, Angle;
        public readonly int[] Pixels;
        public bool Standard { get { return GripX == ItemMesh.DefaultGripX && GripY == ItemMesh.DefaultGripY && Angle == 0; } }

        public CustomItem(string name, int[] pixels, int gripX, int gripY, int angle)
        {
            Name = (name ?? "").Trim();
            if (Name.Length == 0 || Name.Length > MaxName || Array.Exists(Name.ToCharArray(), char.IsControl))
                throw new InvalidDataException("请输入 1 至 60 个字符的物品名称。");
            if (pixels == null || pixels.Length != Size * Size || gripX < 0 || gripX >= Size ||
                    gripY < 0 || gripY >= Size || angle < -180 || angle > 180)
                throw new InvalidDataException("物品尺寸、握点或旋转角度无效。");
            GripX = gripX; GripY = gripY; Angle = angle == -180 ? 180 : angle;
            Pixels = (int[])pixels.Clone();
            bool visible = false;
            for (int i = 0; i < Pixels.Length; i++) {
                if ((uint)Pixels[i] >> 24 == 0) Pixels[i] = 0;
                else visible = true;
            }
            if (!visible) throw new InvalidDataException("物品图像完全透明，请选择有可见像素的 PNG。");
            using (var bytes = new MemoryStream()) {
                using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true)) {
                    writer.Write(Name); writer.Write(GripX); writer.Write(GripY); writer.Write(Angle); writer.Write(Rgba());
                }
                using (var hash = SHA256.Create())
                    Id = "custom:" + BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }

        public static CustomItem FromPng(string path)
        {
            using (var stream = File.OpenRead(path)) {
                Size bounds = SkinMap.ReadPngSize(stream, "请选择物品的 PNG 图片。");
                int w = bounds.Width, h = bounds.Height;
                if (w < 1 || h < 1 || w > 2048 || h > 2048 || stream.Length > 32L * 1024 * 1024)
                    throw new InvalidDataException("支持边长 1 至 2048 像素、文件不超过 32 MB 的 PNG。");
                using (var image = new Bitmap(stream)) {
                    if (image.Width != w || image.Height != h) throw new InvalidDataException("PNG 的尺寸信息不一致。");
                    string name = Path.GetFileNameWithoutExtension(path).Trim();
                    if (name.Length == 0) name = "自定义物品";
                    if (name.Length > MaxName) name = name.Substring(0, MaxName);
                    return new CustomItem(name, Fit(SkinMap.Pixels(image), w, h), ItemMesh.DefaultGripX, ItemMesh.DefaultGripY, 0);
                }
            }
        }
        static int[] Fit(int[] src, int w, int h)
        {
            if (w == Size && h == Size) return src;
            int longest = Math.Max(w, h);
            int dw = Math.Max(1, (w * Size + longest / 2) / longest);
            int dh = Math.Max(1, (h * Size + longest / 2) / longest);
            var result = new int[Size * Size];
            int left = (Size - dw) / 2, top = (Size - dh) / 2;
            for (int y = 0; y < dh; y++) for (int x = 0; x < dw; x++) {
                int x0 = x * w / dw, x1 = Math.Max(x0 + 1, (x + 1) * w / dw);
                int y0 = y * h / dh, y1 = Math.Max(y0 + 1, (y + 1) * h / dh);
                long a = 0, red = 0, green = 0, blue = 0;
                for (int sy = y0; sy < y1; sy++) for (int sx = x0; sx < x1; sx++) {
                    int color = src[sy * w + sx], alpha = (int)((uint)color >> 24);
                    a += alpha;
                    red += alpha * ((color >> 16) & 255); green += alpha * ((color >> 8) & 255); blue += alpha * (color & 255);
                }
                if (a == 0) continue;
                int area = (y1 - y0) * (x1 - x0);
                int opacity = (int)Math.Max(1, (a + area / 2) / area);
                result[(top + y) * Size + left + x] = opacity << 24 | (int)((red + a / 2) / a) << 16 |
                    (int)((green + a / 2) / a) << 8 | (int)((blue + a / 2) / a);
            }
            return result;
        }

        public int[] Sprite()
        {
            if (Standard) return Pixels;
            int span = ItemMesh.Extended.Pixels, center = ItemMesh.CanvasGrip;
            var sprite = new int[span * span];
            var sampled = new bool[Pixels.Length];
            double radians = Angle * Math.PI / 180;
            double cos = Math.Round(Math.Cos(radians), 12), sin = Math.Round(Math.Sin(radians), 12);
            // Inverse sample around pixel centers so even an off-center grip stays in the fist.
            for (int y = 0; y < span; y++) for (int x = 0; x < span; x++) {
                int dx = x - center, dy = y - center;
                int sx = (int)Math.Floor(GripX + cos * dx + sin * dy + .5);
                int sy = (int)Math.Floor(GripY - sin * dx + cos * dy + .5);
                if (sx >= 0 && sx < Size && sy >= 0 && sy < Size) {
                    int index = sy * Size + sx;
                    sprite[y * span + x] = Pixels[index]; sampled[index] = true;
                }
            }
            // An oblique rotation can miss an isolated one-pixel detail between sample centers.
            for (int i = 0; i < Pixels.Length; i++) {
                if (sampled[i] || Pixels[i] == 0) continue;
                int dx = i % Size - GripX, dy = i / Size - GripY;
                int x = center + (int)Math.Floor(cos * dx - sin * dy + .5);
                int y = center + (int)Math.Floor(sin * dx + cos * dy + .5);
                if (sprite[y * span + x] == 0) sprite[y * span + x] = Pixels[i];
            }
            return sprite;
        }

        byte[] Rgba()
        {
            var bytes = new byte[Pixels.Length * 4];
            for (int i = 0; i < Pixels.Length; i++) {
                int c = Pixels[i], j = i * 4;
                bytes[j] = (byte)(c >> 16); bytes[j + 1] = (byte)(c >> 8); bytes[j + 2] = (byte)c; bytes[j + 3] = (byte)(c >> 24);
            }
            return bytes;
        }

        internal sealed class Record
        {
            public int Version { get; set; }
            public string Name { get; set; }
            public int GripX { get; set; }
            public int GripY { get; set; }
            public int Angle { get; set; }
            public string Rgba { get; set; }
        }
        public void Write(string path)
        {
            var data = new Record { Version = 1, Name = Name, GripX = GripX, GripY = GripY, Angle = Angle,
                Rgba = Convert.ToBase64String(Rgba()) };
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(data), new UTF8Encoding(false));
        }
        static T Field<T>(IDictionary<string, object> data, string key)
        {
            object value;
            if (data == null || !data.TryGetValue(key, out value) || !(value is T))
                throw new InvalidDataException("自定义物品字段无效：" + key);
            return (T)value;
        }
        public static CustomItem Read(string path)
        {
            IDictionary<string, object> fields;
            using (var stream = File.OpenRead(path)) {
                if (stream.Length > 16384) throw new InvalidDataException("物品数据文件过大。");
                using (var reader = new StreamReader(stream, Encoding.UTF8, true)) {
                    var json = new JavaScriptSerializer { MaxJsonLength = 16384, RecursionLimit = 8 };
                    try { fields = json.DeserializeObject(reader.ReadToEnd()) as IDictionary<string, object>; }
                    catch (ArgumentException ex) { throw new InvalidDataException("物品数据格式无效。", ex); }
                    catch (InvalidOperationException ex) { throw new InvalidDataException("物品数据格式无效。", ex); }
                    catch (FormatException ex) { throw new InvalidDataException("物品字段格式无效。", ex); }
                    catch (OverflowException ex) { throw new InvalidDataException("物品字段超出有效范围。", ex); }
                }
            }
            string rgba = Field<string>(fields, "Rgba");
            if (Field<int>(fields, "Version") != 1 || rgba.Length != 1368)
                throw new InvalidDataException("自定义物品数据不完整或版本不受支持。");
            byte[] raw;
            try { raw = Convert.FromBase64String(rgba); }
            catch (FormatException ex) { throw new InvalidDataException("物品像素数据无效。", ex); }
            if (raw.Length != Size * Size * 4) throw new InvalidDataException("物品像素数量不正确。");
            var pixels = new int[Size * Size];
            for (int i = 0, j = 0; i < pixels.Length; i++, j += 4)
                pixels[i] = raw[j + 3] << 24 | raw[j] << 16 | raw[j + 1] << 8 | raw[j + 2];
            return new CustomItem(Field<string>(fields, "Name"), pixels, Field<int>(fields, "GripX"),
                Field<int>(fields, "GripY"), Field<int>(fields, "Angle"));
        }
    }
}
