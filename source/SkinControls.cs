using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SkinLive
{
    internal sealed partial class Studio
    {
        Bitmap selectedAtlas;
        string selectedPath;
        Label skinLabel;
        ComboBox layoutBox;
        Button exportModel;
        void SkinControls(TableLayoutPanel table)
        {
            Row(table,LabelFor("角色皮肤",sectionFont,Theme.Ink),30);
            skinLabel=LabelFor("请选择皮肤 PNG",uiFont,Theme.Muted);Row(table,skinLabel,25);
            var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
            var pick=ActionButton("打开皮肤 / 模型",false);
            exportModel=ActionButton("导出 Live2D",false);exportModel.Enabled=false;
            pick.Click+=delegate {
                using(var dlg=new OpenFileDialog {Title="打开 MC 皮肤或本工具导出的模型",Filter="皮肤或模型|*.png;*.model3.json|PNG 皮肤|*.png|Live2D 模型|*.model3.json",CheckFileExists=true})
                    if(dlg.ShowDialog(this)==DialogResult.OK)LoadAvatar(dlg.FileName);
            };
            exportModel.Click+=delegate {ExportAvatar();};buttons.Controls.Add(pick);buttons.Controls.Add(exportModel);
            Row(table,buttons,38);
            layoutBox=Picker("PNG 手臂贴图布局");
            layoutBox.Items.AddRange(new object[]{"自动识别","宽臂贴图","细臂贴图"});
            layoutBox.SelectedIndex=prefs.ArmLayout;
            layoutBox.SelectedIndexChanged+=delegate {
                prefs.ArmLayout=layoutBox.SelectedIndex;
                if(!string.IsNullOrEmpty(selectedPath) && selectedPath.EndsWith(".png",StringComparison.OrdinalIgnoreCase)) LoadAvatar(selectedPath);
            };
            var pair=Pair(64);pair.Controls.Add(LabelFor("贴图布局",uiFont,Theme.Muted),0,0);pair.Controls.Add(layoutBox,1,0);
            Row(table,pair,33);Divider(table);
        }
        void PrepareSkin(string arg)
        {
            AllowDrop=true;
            DragEnter+=delegate(object o,DragEventArgs e){if(e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};
            DragDrop+=delegate(object o,DragEventArgs e){
                string[] files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files!=null&&files.Length>0)LoadAvatar(files[0]);
            };
            if(!string.IsNullOrEmpty(arg)){LoadAvatar(arg);return;}
            if(File.Exists(prefs.LastSkin) && LoadAvatar(prefs.LastSkin))return;
            string bundled=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Models","Avatar",ModelFiles.Manifest);
            if(File.Exists(bundled))LoadAvatar(bundled);
        }
        bool LoadAvatar(string file)
        {
            Bitmap next=null;
            try {
                file=Path.GetFullPath(file);string label;
                if(file.EndsWith(".model3.json",StringComparison.OrdinalIgnoreCase)) {
                    next=ModelFiles.OpenExport(file);label=Path.GetFileName(Path.GetDirectoryName(file))+" · 已转换模型";
                }
                else if(file.EndsWith(".png",StringComparison.OrdinalIgnoreCase)) {
                    bool slim;next=SkinMap.ConvertPng(file,prefs.ArmLayout,out slim);
                    label=Path.GetFileNameWithoutExtension(file)+(slim?" · 细臂贴图":" · 宽臂贴图");
                } else throw new InvalidDataException("请选择 PNG 皮肤，或本工具导出的 .model3.json。");
                raster.SetAtlas(next);
                Bitmap previous=selectedAtlas;selectedAtlas=next;next=null;
                if(previous!=null)previous.Dispose();
                selectedPath=file;prefs.LastSkin=file;skinLabel.Text=label;tip.SetToolTip(skinLabel,label);
                exportModel.Enabled=true;Message("皮肤已载入 · "+label,false);return true;
            } catch(Exception ex){Message("载入失败 · "+ex.Message,true);return false;}
            finally{if(next!=null)next.Dispose();}
        }
        void ExportAvatar()
        {
            if(selectedAtlas==null)return;
            using(var dialog=new FolderBrowserDialog {Description="选择保存位置，将在其中新建模型文件夹",ShowNewFolderButton=true}) {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try {
                    string name=selectedPath.EndsWith(".png",StringComparison.OrdinalIgnoreCase)?Path.GetFileNameWithoutExtension(selectedPath):
                        Path.GetFileName(Path.GetDirectoryName(selectedPath));
                    string folder=ModelFiles.NewFolder(dialog.SelectedPath,name+"-Live2D");
                    ModelFiles.Export(selectedAtlas,folder);Message("模型已保存 · "+folder,false);
                    MessageBox.Show(this,folder,"Live2D 模型已导出",MessageBoxButtons.OK,MessageBoxIcon.Information);
                }catch(Exception ex){Message("导出失败 · "+ex.Message,true);}
            }
        }
    }
}
