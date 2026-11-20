using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SkinLive
{
    internal sealed class CustomItemLibrary
    {
        readonly string directory;
        readonly Dictionary<string, HandItem> items = new Dictionary<string, HandItem>(StringComparer.Ordinal);
        readonly Dictionary<string, string> slots = new Dictionary<string, string>(StringComparer.Ordinal);
        public int Unreadable { get; private set; }
        public IEnumerable<HandItem> Items { get { return items.Values.OrderBy(item => item.Label).ThenBy(item => item.Id); } }

        public CustomItemLibrary(string directory)
        {
            this.directory = directory;
            try {
                if (!Directory.Exists(directory)) return;
                foreach (string file in Directory.GetFiles(directory, "*.json")) {
                    try {
                        var item = new HandItem(CustomItem.Read(file));
                        if (!items.ContainsKey(item.Id)) { items.Add(item.Id, item); slots.Add(item.Id, file); }
                    } catch (InvalidDataException) { Unreadable++; }
                      catch (IOException) { Unreadable++; }
                      catch (UnauthorizedAccessException) { Unreadable++; }
                }
            } catch (IOException) { Unreadable++; }
              catch (UnauthorizedAccessException) { Unreadable++; }
        }
        public HandItem Find(string id)
        {
            HandItem item;
            return id != null && items.TryGetValue(id, out item) ? item : null;
        }

        // File names are local slots; IDs identify the actual portable picture, label and grip.
        public HandItem Store(HandItem item, HandItem previous)
        {
            HandItem existing = Find(item.Id);
            string oldFile = null;
            if (previous != null) slots.TryGetValue(previous.Id, out oldFile);
            if (existing != null) {
                if (oldFile != null && previous.Id != item.Id) {
                    File.Delete(oldFile);
                    items.Remove(previous.Id); slots.Remove(previous.Id);
                }
                return existing;
            }
            Directory.CreateDirectory(directory);
            string slot = oldFile ?? Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
            string temp = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            try {
                item.Design.Write(temp);
                if (File.Exists(slot)) File.Replace(temp, slot, null); else File.Move(temp, slot);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
            if (oldFile != null) { items.Remove(previous.Id); slots.Remove(previous.Id); }
            items.Add(item.Id, item); slots.Add(item.Id, slot);
            return item;
        }
    }
}
