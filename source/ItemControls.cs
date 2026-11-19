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
        HandItem equipped = VanillaItems.Empty, wanted;
        ComboBox handBox;
        Button fetchItems, importItems;
        bool settingHand, installing;

        void HandControls(TableLayoutPanel table)
        {
            var row = Pair(64);
            handBox = Picker("角色右手物品");
            handBox.Items.AddRange(VanillaItems.Choices);
            handBox.SelectedItem = equipped;
            handBox.Enabled = false;
            handBox.SelectedIndexChanged += delegate {
                if (settingHand) return;
                var choice = (HandItem)handBox.SelectedItem;
                try { Equip(choice); Message("右手 · " + choice.Label, false); }
                catch (FileNotFoundException) {
                    wanted = choice;
                    ShowHand(equipped);
                    Message("点「获取原版贴图」或「离线导入」，完成后会装备" + choice.Label + "。", true);
                }
                catch (Exception ex) { wanted = null; ShowHand(equipped); Message("切换失败 · " + ex.Message, true); }
            };
            row.Controls.Add(LabelFor("右手物品", uiFont, Theme.Ink), 0, 0);
            row.Controls.Add(handBox, 1, 0);
            Row(table, row, 35);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            fetchItems = ActionButton("获取原版贴图", false);
            importItems = ActionButton("离线导入", false);
            tip.SetToolTip(fetchItems, "从 Mojang 官方样例获取五张木制物品贴图，缓存后可离线使用。");
            tip.SetToolTip(importItems, "选择存有同一版本五张原版 PNG 的文件夹。");
            fetchItems.Click += async delegate { await InstallItems(null); };
            importItems.Click += async delegate {
                using (var dlg = new FolderBrowserDialog { Description = "选择存放五张原版木制物品 PNG 的文件夹",
                    ShowNewFolderButton = false }) {
                    if (dlg.ShowDialog(this) == DialogResult.OK) await InstallItems(dlg.SelectedPath);
                }
            };
            actions.Controls.Add(fetchItems); actions.Controls.Add(importItems);
            Row(table, actions, 33);
        }

        void ShowHand(HandItem item)
        {
            settingHand = true;
            try { handBox.SelectedItem = item; }
            finally { settingHand = false; }
        }

        int[] SpriteFor(HandItem item)
        {
            if (item == VanillaItems.Empty) return null;
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
            handBox.Enabled = fetchItems.Enabled = importItems.Enabled = false;
            Message(folder == null ? "正在从 Mojang 官方仓库获取五张原版贴图…" : "正在校验并导入原版贴图…", false);
            try {
                await Task.Run(delegate { VanillaItems.Install(folder); });
                if (IsDisposed || Disposing) return;
                if (wanted != null && selectedPath != null) Equip(wanted);
                Message("五张木制物品贴图已就绪 · 右手：" + equipped.Label, false);
            } catch (Exception ex) {
                if (!IsDisposed && !Disposing)
                    Message("贴图准备失败 · " + ex.Message + " 可重试或选择「离线导入」。", true);
            } finally {
                installing = false;
                if (!IsDisposed && !Disposing) {
                    handBox.Enabled = selectedPath != null;
                    fetchItems.Enabled = importItems.Enabled = true;
                }
            }
        }
    }
}
