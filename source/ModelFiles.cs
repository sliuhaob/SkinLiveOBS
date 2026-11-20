using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace SkinLive
{
    internal static class ModelFiles
    {
        public const string Manifest="Avatar.model3.json", Texture="Avatar.1024/texture_00.png";
        const string ItemSetting="SkinLiveOBS.item.txt", CustomSetting="SkinLiveOBS.item.json";
        const string LegacyRig="86ED31C45489742F436CB6BF00749771C59834D03B81BF6050DD1F28039C0BD6";
        const string HeldRig110="B291210DF1906F326E0D50F790F2418A79987768D3D985B8BCD822BF63332C81";
        const string EmptyRig120="67468C868360B0BC9BEFB7E3B3FAEA1CA72D24A488374F8FFFAD8A7EBE98D8A9";
        const string HeldRig120="DC45495D3F8E1443C01385A547FFF5D114CE70CCD01CF35E7EC5530EF8B1885C";
        static readonly string[] Assets={"Avatar.moc3",Manifest,"Avatar.cdi3.json","Motions/Jump.motion3.json","Motions/Swing.motion3.json",
            "Motions/SwingLeft.motion3.json","Motions/Crouch.motion3.json","ASSET-LICENSE.txt"};
        static Stream Asset(string name)
        {
            Stream s=typeof(ModelFiles).Assembly.GetManifestResourceStream("SkinLive."+name.Replace('/','.'));
            if(s==null)throw new InvalidDataException("内置模型数据缺失："+name);return s;
        }
        static void MakeDir(string path){Directory.CreateDirectory(Path.GetDirectoryName(path));}
        static readonly Lazy<string> EmptyHash=new Lazy<string>(()=>RigHash("Avatar.moc3"));
        static readonly Lazy<string> HeldHash=new Lazy<string>(()=>RigHash("RightHand.moc3"));
        static readonly Lazy<string> ExtendedHash=new Lazy<string>(()=>RigHash("CustomHand.moc3"));
        static string RigHash(string resource)
        {
            using(SHA256 hash=SHA256.Create()) using(Stream input=Asset(resource))
                return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","");
        }
        public static void Export(Bitmap atlas,string destination,HandItem item)
        {
            string path=Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            string parent=Path.GetDirectoryName(path);
            if(string.IsNullOrEmpty(parent)||Directory.Exists(path)||File.Exists(path))
                throw new IOException("请选择尚不存在的模型文件夹，以免覆盖已有模型。");
            Directory.CreateDirectory(parent);
            string stage=Path.Combine(parent,".skinlive-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            try {
                foreach(string name in Assets) {
                    string target=Path.Combine(stage,name);MakeDir(target);
                    string resource=name;
                    if(name=="Avatar.moc3" && item!=VanillaItems.Empty)
                        resource=item.Design!=null && !item.Design.Standard?"CustomHand.moc3":"RightHand.moc3";
                    using(Stream input=Asset(resource)) using(FileStream output=File.Create(target)) input.CopyTo(output);
                }
                string tex=Path.Combine(stage,Texture);MakeDir(tex);atlas.Save(tex,ImageFormat.Png);
                if(item!=VanillaItems.Empty)File.WriteAllText(Path.Combine(stage,ItemSetting),item.Id+Environment.NewLine);
                if(item.Design!=null)item.Design.Write(Path.Combine(stage,CustomSetting));
                Directory.Move(stage,path);
            } finally {if(Directory.Exists(stage))Directory.Delete(stage,true);}
        }
        public static string NewFolder(string parent,string name)
        {
            string folder=Path.Combine(parent,name);
            for(int i=2;Directory.Exists(folder)||File.Exists(folder);i++)folder=Path.Combine(parent,name+"-"+i);
            return folder;
        }
        static string Child(string parent,string relative)
        {
            string root=Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string p=Path.GetFullPath(Path.Combine(root,relative));
            if(Path.IsPathRooted(relative)||!p.StartsWith(root,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("模型引用文件必须在同一模型文件夹中。");
            return p;
        }
        public static Bitmap OpenExport(string manifest,out HandItem item)
        {
            string parent=Path.GetDirectoryName(Path.GetFullPath(manifest));
            var json=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(manifest));
            var refs=(Dictionary<string,object>)json["FileReferences"];
            string moc=Child(parent,(string)refs["Moc"]);
            item=VanillaItems.Empty;
            using(SHA256 hash=SHA256.Create()) using(Stream actual=File.OpenRead(moc)) {
                string digest=BitConverter.ToString(hash.ComputeHash(actual)).Replace("-","");
                if(digest!=LegacyRig && digest!=EmptyRig120 && digest!=EmptyHash.Value) {
                    bool standard=digest==HeldRig110 || digest==HeldRig120 || digest==HeldHash.Value;
                    if(!standard && digest!=ExtendedHash.Value)
                        throw new InvalidDataException("请选择由本工具导出的方块人模型。");
                    string setting=Path.Combine(parent,ItemSetting);
                    string id=File.Exists(setting)?File.ReadAllText(setting).Trim():null;
                    string custom=Path.Combine(parent,CustomSetting);
                    if(File.Exists(custom)) {
                        item=new HandItem(CustomItem.Read(custom));
                        if(standard!=item.Design.Standard || (id!=null && id!=item.Id))
                            throw new InvalidDataException("物品信息与模型不匹配，请使用同一份导出的完整文件夹。");
                    } else if(!standard || (id!=null && id.StartsWith("custom:",StringComparison.Ordinal))) {
                        throw new InvalidDataException("模型缺少自定义握点文件 SkinLiveOBS.item.json，请复制完整的模型文件夹。");
                    } else if(id!=null) {
                        item=VanillaItems.Find(id);
                        if(item==null)throw new InvalidDataException("模型使用了当前版本不支持的右手物品。");
                    }
                }
            }
            System.Collections.IList files=(System.Collections.IList)refs["Textures"];
            if(files.Count!=1)throw new InvalidDataException("此模型的贴图数量不正确。");
            using(var stream=File.OpenRead(Child(parent,(string)files[0])))
            using(var image=new Bitmap(stream)) {
                if(image.Width!=SkinMap.Size||image.Height!=SkinMap.Size)throw new InvalidDataException("模型贴图尺寸不正确。");
                return image.Clone(new Rectangle(0,0,image.Width,image.Height),PixelFormat.Format32bppArgb);
            }
        }
    }
}
