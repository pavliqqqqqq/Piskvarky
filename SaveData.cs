using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Piskvorky
{
    public class Rec
    {
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }

        [JsonIgnore] public int Games => Wins + Losses + Draws;
        [JsonIgnore] public double Percent => Games == 0 ? 0 : 100.0 * Wins / Games;
    }

    public class LbEntry
    {
        public string Name { get; set; } = "HRAC";
        public string Level { get; set; } = "EASY";
        public string Result { get; set; } = "W";   // W / L / D
        public int Moves { get; set; }              // kameny, ktere hrac polozil
        public string Opening { get; set; } = "";
        public string Date { get; set; } = "";
    }

    public class SaveData
    {
        public string PlayerName { get; set; } = "HRAC";
        public Dictionary<string, Rec> Stats { get; set; } = new Dictionary<string, Rec>();
        public List<LbEntry> Board { get; set; } = new List<LbEntry>();

        static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Piskvorky", "save.json");

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var d = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(FilePath));
                    if (d != null) return d;
                }
            }
            catch { }
            return new SaveData();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public Rec Get(string key)
        {
            if (!Stats.TryGetValue(key, out var r))
            {
                r = new Rec();
                Stats[key] = r;
            }
            return r;
        }
    }
}
