using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SkinLive
{
    internal sealed partial class Studio
    {
        readonly Dictionary<HandItem, int[]> sprites = new Dictionary<HandItem, int[]>();
        readonly CustomItemLibrary customItems = new CustomItemLibrary(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkinLiveOBS", "custom-items"));
        HandItem equipped = VanillaItems.Empty, wanted;
        ComboBox handBox;
        Button fetchItems, importItems, addCustom, editCustom;
        bool settingHand, installing;

        void HandControls(TableLayoutPanel table)
        {
            handBox = Picker("角色右手物品");
            ShowFullNames(handBox);
            handBox.Items.AddRange(VanillaItems.Choices);
            foreach (HandItem item in customItems.Items) handBox.Items.Add(item);
            handBox.SelectedItem = equipped;
            handBox.Enabled = false;
            handBox.SelectedIndexChanged += delegate {
                if (settingHand || handBox.SelectedItem == null) return;
                var choice = (HandItem)handBox.SelectedItem;
                try { Equip(choice); Message("右手 · " + choice.Label, false); }
                catch (FileNotFoundException) {
                    wanted = choice;
                    ShowHand(equipped);
                    Message("点「获取原版贴图」或「原版离线导入」，完成后会装备" + choice.Label + "。", true);
                }
                catch (Exception ex) { wanted = null; ShowHand(equipped); Message("切换失败 · " + ex.Message, true); }
            };
            LabeledRow(table, "右手物品", handBox, Theme.Ink);

            addCustom = ActionButton("导入物品 PNG", false); editCustom = ActionButton("调整握点", false);
            addCustom.Enabled = editCustom.Enabled = false;
            tip.SetToolTip(addCustom, "导入一张物品 PNG，默认使用标准右手握姿。");
            tip.SetToolTip(editCustom, "调整当前自定义物品的握点或旋转角度。");
            addCustom.Click += delegate {
                using (var picker = new OpenFileDialog { Title = "选择右手物品贴图", Filter = "PNG 物品贴图|*.png",
                    CheckFileExists = true, Multiselect = false }) {
                    if (picker.ShowDialog(this) == DialogResult.OK) EditItem(picker.FileName, null);
                }
            };
            editCustom.Click += delegate { EditItem(null, equipped); };
            ButtonRow(table, addCustom, editCustom);

            fetchItems = ActionButton("获取原版贴图", false);
            importItems = ActionButton("原版离线导入", false);
            tip.SetToolTip(fetchItems, "从 Mojang 官方样例获取五张木制物品贴图，缓存后可离线使用。");
            tip.SetToolTip(importItems, "选择存有同一版本五张原版 PNG 的文件夹。");
            fetchItems.Click += async delegate { await InstallItems(null); };
            importItems.Click += async delegate {
                using (var dlg = new FolderBrowserDialog { Description = "选择存放五张原版木制物品 PNG 的文件夹",
                    ShowNewFolderButton = false }) {
                    if (dlg.ShowDialog(this) == DialogResult.OK) await InstallItems(dlg.SelectedPath);
                }
            };
            ButtonRow(table, fetchItems, importItems);
        }

        void ShowHand(HandItem item)
        {
            settingHand = true;
            try {
                HandItem choice = FindHand(item.Id);
                if (choice == null) { handBox.Items.Add(item); choice = item; }
                handBox.SelectedItem = choice;
            }
            finally { settingHand = false; }
            UpdateHandControls();
        }

        HandItem FindHand(string id)
        {
            if (id == null) return null;
            foreach (HandItem item in handBox.Items)
                if (string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)) return item;
            return null;
        }
        void UpdateHandControls()
        {
            handBox.Enabled = addCustom.Enabled = selectedPath != null && !installing;
            editCustom.Enabled = addCustom.Enabled && equipped.Design != null;
        }
        void EditItem(string png, HandItem old)
        {
            try {
                CustomItem source = png != null ? CustomItem.FromPng(png) : old.Design;
                using (var atlas = raster.CopyAtlas())
                using (var dialog = new ItemGripDialog(source, atlas, uiFont)) {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    HandItem choice = customItems.Store(new HandItem(dialog.Result), old);
                    if (old != null && old.Id != choice.Id) {
                        settingHand = true;
                        try { var listed = FindHand(old.Id); if (listed != null) handBox.Items.Remove(listed); }
                        finally { settingHand = false; }
                    }
                    Equip(choice);
                    bool saved = prefs.Save();
                    Message(saved ? "已装备「" + choice.Label + "」" : "物品已保存并装备，上次选择暂时未能写入设置。", !saved);
                }
            } catch (Exception ex) { Message("设置物品失败 · " + ex.Message, true); }
        }

        int[] SpriteFor(HandItem item)
        {
            if (item == VanillaItems.Empty) return null;
            if (item.Design != null) return item.Design.Sprite();
            int[] pixels;
            if (!sprites.TryGetValue(item, out pixels)) {
                pixels = VanillaItems.Load(item);
                sprites.Add(item, pixels);
            }
            return pixels;
        }

        void Equip(HandItem item)
        {
            int[] sprite = SpriteFor(item);
            raster.SetItem(sprite);
            equipped = item; prefs.ItemId = item.Id; wanted = null;
            ShowHand(item);
        }

        async Task InstallItems(string folder)
        {
            if (installing) return;
            installing = true;
            UpdateHandControls();
            fetchItems.Enabled = importItems.Enabled = false;
            Message(folder == null ? "正在从 Mojang 官方仓库获取五张原版贴图…" : "正在校验并导入原版贴图…", false);
            try {
                await Task.Run(delegate { VanillaItems.Install(folder); });
                if (IsDisposed || Disposing) return;
                if (wanted != null && selectedPath != null) Equip(wanted);
                Message("五张木制物品贴图已就绪 · 右手：" + equipped.Label, false);
            } catch (Exception ex) {
                if (!IsDisposed && !Disposing)
                    Message("贴图准备失败 · " + ex.Message + " 可重试或选择「原版离线导入」。", true);
            } finally {
                installing = false;
                if (!IsDisposed && !Disposing) {
                    UpdateHandControls();
                    fetchItems.Enabled = importItems.Enabled = true;
                }
            }
        }
    }
}
