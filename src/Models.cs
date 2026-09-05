using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace GenshinDesktopPet
{
    public sealed class CharacterCatalog
    {
        public List<CharacterDefinition> characters { get; set; }

        public static CharacterCatalog Load(string path)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            CharacterCatalog catalog = serializer.Deserialize<CharacterCatalog>(File.ReadAllText(path, Encoding.UTF8));
            if (catalog == null || catalog.characters == null || catalog.characters.Count == 0)
            {
                throw new InvalidDataException("characters.json 没有有效角色。 ");
            }
            return catalog;
        }
    }

    public sealed class CharacterDefinition
    {
        public string id { get; set; }
        public string displayName { get; set; }
        public string folder { get; set; }
    }

    public sealed class AppSettings
    {
        public List<string> ActiveCharacterIds { get; set; }
        public Dictionary<string, int> CharacterScalePercent { get; set; }
        public Dictionary<string, double> CharacterNormalizedX { get; set; }
        public bool AlwaysOnTop { get; set; }
        public bool ClickThrough { get; set; }
        public int QuickChatBarMode { get; set; }

        public static AppSettings CreateDefault()
        {
            AppSettings settings = new AppSettings();
            settings.ActiveCharacterIds = new List<string> { "paimon" };
            settings.CharacterScalePercent = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            settings.CharacterNormalizedX = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            settings.AlwaysOnTop = true;
            settings.ClickThrough = false;
            settings.QuickChatBarMode = 1;
            settings.CharacterScalePercent["paimon"] = 100;
            settings.CharacterNormalizedX["paimon"] = 0.08;
            return settings;
        }

        public void Normalize(IEnumerable<CharacterDefinition> definitions)
        {
            if (QuickChatBarMode != 1 && QuickChatBarMode != 2) QuickChatBarMode = 1;
            HashSet<string> validIds = new HashSet<string>(definitions.Select(d => d.id), StringComparer.OrdinalIgnoreCase);
            if (ActiveCharacterIds == null)
            {
                ActiveCharacterIds = new List<string>();
            }
            ActiveCharacterIds = ActiveCharacterIds.Where(validIds.Contains).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList();
            if (ActiveCharacterIds.Count == 0 && validIds.Contains("paimon"))
            {
                ActiveCharacterIds.Add("paimon");
            }
            if (CharacterScalePercent == null)
            {
                CharacterScalePercent = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }
            if (CharacterNormalizedX == null)
            {
                CharacterNormalizedX = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            }
            foreach (CharacterDefinition definition in definitions)
            {
                int scale;
                if (!CharacterScalePercent.TryGetValue(definition.id, out scale) || (scale != 100 && scale != 125 && scale != 150))
                {
                    CharacterScalePercent[definition.id] = 100;
                }
                double x;
                if (!CharacterNormalizedX.TryGetValue(definition.id, out x) || double.IsNaN(x) || double.IsInfinity(x))
                {
                    CharacterNormalizedX[definition.id] = 0.08;
                }
                else
                {
                    CharacterNormalizedX[definition.id] = Math.Max(0.0, Math.Min(1.0, x));
                }
            }
        }
    }

    public sealed class SettingsStore
    {
        private readonly string path;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        public SettingsStore()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GenshinDesktopPet");
            path = Path.Combine(folder, "settings.json");
        }

        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(path))
                {
                    return AppSettings.CreateDefault();
                }
                AppSettings settings = serializer.Deserialize<AppSettings>(File.ReadAllText(path, Encoding.UTF8));
                return settings ?? AppSettings.CreateDefault();
            }
            catch
            {
                return AppSettings.CreateDefault();
            }
        }

        public void Save(AppSettings settings)
        {
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, serializer.Serialize(settings), new UTF8Encoding(false));
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
    }
}
