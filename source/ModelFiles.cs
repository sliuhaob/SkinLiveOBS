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
        static readonly string[] Assets={"Avatar.moc3",Manifest,"Avatar.cdi3.json","Motions/Jump.motion3.json","Motions/Swing.motion3.json","ASSET-LICENSE.txt"};
        static Stream Asset(string name)
        {
            Stream s=typeof(ModelFiles).Assembly.GetManifestResourceStream("SkinLive."+name.Replace('/','.'));
            if(s==null)throw new InvalidDataException("内置模型数据缺失："+name);return s;
        }
        static void MakeDir(string path){Directory.CreateDirectory(Path.GetDirectoryName(path));}
        public static void Export(Bitmap atlas,string destination)
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
                    using(Stream input=Asset(name)) using(FileStream output=File.Create(target)) input.CopyTo(output);
                }
                string tex=Path.Combine(stage,Texture);MakeDir(tex);atlas.Save(tex,ImageFormat.Png);
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
        public static Bitmap OpenExport(string manifest)
        {
            string parent=Path.GetDirectoryName(Path.GetFullPath(manifest));
            var json=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(manifest));
            var refs=(Dictionary<string,object>)json["FileReferences"];
            string moc=Child(parent,(string)refs["Moc"]);
            using(SHA256 hash=SHA256.Create()) using(Stream expected=Asset("Avatar.moc3")) using(Stream actual=File.OpenRead(moc)) {
                if(!String.Equals(Convert.ToBase64String(hash.ComputeHash(expected)),Convert.ToBase64String(hash.ComputeHash(actual)),StringComparison.Ordinal))
                    throw new InvalidDataException("此直播窗口只读取本工具导出的空手方块人模型。");
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
