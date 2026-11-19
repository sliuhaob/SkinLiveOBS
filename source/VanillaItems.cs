using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace SkinLive
{
    internal sealed class HandItem
    {
        public readonly string Id, Label, FileName, Sha256;
        internal HandItem(string id, string label, string fileName, string sha256)
        { Id = id; Label = label; FileName = fileName; Sha256 = sha256; }
        public override string ToString() { return Label; }
    }

    internal static class VanillaItems
    {
        const string Revision = "15701fa9c13923f12662984c32c124327e40e3d3";
        const string ItemPath = "resource_pack/textures/items/";
        const int ByteLimit = 8192;
        public static readonly HandItem Empty = new HandItem("none", "空手", null, null);
        public static readonly HandItem[] Choices = {
            Empty,
            new HandItem("minecraft:wooden_sword", "木剑", "wood_sword.png", "af0f711c51bb28d6d2c677791cbbb5f6e0a797518e959392c3d0d9027eb17243"),
            new HandItem("minecraft:wooden_pickaxe", "木镐", "wood_pickaxe.png", "1fd1fb4ba18cc1eb3367a275aba3c2a73c8ebf21663d8aa6c852ad5710adcf84"),
            new HandItem("minecraft:wooden_axe", "木斧", "wood_axe.png", "79557b547db13606a41a03c77c58b1afc6f11f242c3e3019eed259d1c7550550"),
            new HandItem("minecraft:wooden_shovel", "木锹（木铲）", "wood_shovel.png", "05221a712d3879935bcbc1bd34a335005c1b21fbe383bbfa8149e886d15eae6d"),
            new HandItem("minecraft:wooden_hoe", "木锄", "wood_hoe.png", "03e7b24e0e076f0ad87ae90ef3409c45c8d0af01fa115d7487ffa27b651d2965")
        };
        static readonly string cache = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "SkinLiveOBS", "items", Revision);

        public static HandItem Find(string id)
        {
            foreach (HandItem item in Choices)
                if (String.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)) return item;
            return null;
        }

        static bool Matches(HandItem item, byte[] data)
        {
            using (var hash = SHA256.Create()) {
                string digest = BitConverter.ToString(hash.ComputeHash(data)).Replace("-", "");
                return String.Equals(digest, item.Sha256, StringComparison.OrdinalIgnoreCase);
            }
        }

        static byte[] ReadFile(string file, HandItem item)
        {
            var info = new FileInfo(file);
            if (!info.Exists || info.Length > ByteLimit) return null;
            byte[] data = File.ReadAllBytes(file);
            return Matches(item, data) ? data : null;
        }

        public static int[] Load(HandItem item)
        {
            if (item == Empty) return null;
            byte[] data = ReadFile(Path.Combine(cache, item.FileName), item);
            if (data == null) throw new FileNotFoundException("请先获取原版贴图，或从文件夹离线导入。");
            using (var stream = new MemoryStream(data, false))
            using (var image = new Bitmap(stream)) return SkinMap.Pixels(image);
        }

        static byte[] Download(string url, HandItem item, bool api)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "SkinLiveOBS/" + typeof(VanillaItems).Assembly.GetName().Version.ToString(2);
            request.Accept = api ? "application/vnd.github+json" : "image/png";
            request.Timeout = request.ReadWriteTimeout = 15000;
            using (var response = request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var output = new MemoryStream()) {
                var block = new byte[1024]; int read;
                while ((read = stream.Read(block, 0, block.Length)) > 0) {
                    if (output.Length + read > ByteLimit) throw new InvalidDataException("贴图文件超出预期大小。");
                    output.Write(block, 0, read);
                }
                byte[] data = output.ToArray();
                if (api) {
                    var json = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(data));
                    object content, encoding;
                    if (json == null || !json.TryGetValue("encoding", out encoding) || (string)encoding != "base64" ||
                        !json.TryGetValue("content", out content) || !(content is string))
                        throw new InvalidDataException("官方仓库返回的文件格式不正确。");
                    data = Convert.FromBase64String((string)content);
                }
                if (!Matches(item, data)) throw new InvalidDataException(item.FileName + " 的 SHA-256 校验失败。");
                return data;
            }
        }

        static byte[] Fetch(HandItem item)
        {
            string path = ItemPath + item.FileName;
            try { return Download("https://raw.githubusercontent.com/Mojang/bedrock-samples/" + Revision + "/" + path, item, false); }
            catch (WebException) { }
            catch (InvalidDataException) { }
            catch (IOException) { }
            return Download("https://api.github.com/repos/Mojang/bedrock-samples/contents/" + path + "?ref=" + Revision, item, true);
        }

        static void Store(HandItem item, byte[] data)
        {
            Directory.CreateDirectory(cache);
            string file = Path.Combine(cache, item.FileName);
            string temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllBytes(temp, data);
                if (File.Exists(file)) File.Replace(temp, file, null);
                else File.Move(temp, file);
            } catch (IOException) {
                if (ReadFile(file, item) == null) throw;
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        // A non-null folder is always one explicitly chosen by the user.
        public static void Install(string folder)
        {
            if (folder == null) ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            for (int i = 1; i < Choices.Length; i++) {
                HandItem item = Choices[i];
                if (ReadFile(Path.Combine(cache, item.FileName), item) != null) continue;
                byte[] data = folder == null ? Fetch(item) : ReadFile(Path.Combine(folder, item.FileName), item);
                if (data == null) throw new InvalidDataException("缺少原版贴图或文件与官方版本不符：" + item.FileName);
                Store(item, data);
            }
        }
    }
}
