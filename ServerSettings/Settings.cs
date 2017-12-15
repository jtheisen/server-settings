using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace MonkeyBusters.ServerSettings
{
    public static class Settings
    {
        public static String GetString(String name, String def)
        {
            return SettingsConfiguration.RootSettingsSource.GetSetting(name) ?? def;
        }

        public static String GetString(String name)
        {
            var result = GetString(name, null);
            if (result == null) throw new ArgumentException($"Setting '{name}' is not set.");
            return result;
        }

        public static Int32 GetInt32(String name, Int32? def = null)
        {
            var s = GetString(name, "");
            if (s == "" && def.HasValue) return def.Value;
            if (!Int32.TryParse(s, out var result)) if (def.HasValue) return def.Value; else Throw(name, s, "Int32");
            return result;
        }

        public static Boolean GetBoolean(String name, Boolean? def = null)
        {
            var s = GetString(name, "");
            if (s == "" && def.HasValue) return def.Value;
            if (!Boolean.TryParse(s, out var result)) if (def.HasValue) return def.Value; else Throw(name, s, "Boolean");
            return result;
        }

        static void Throw(String name, String value, String type)
        {
            throw new Exception("Setting '{name}' has value '{value}' which can't be parsed into an {type}.");
        }
    }

    public static class SettingsConfiguration
    {
        public static SettingsSource RootSettingsSource { get; set; } =
            new CombinedSettingsSource(new SettingsSource[]
            {
                new HomeFileSettingsSource(),
                new LocalFileSettingsSource(),
                new AppSettingsSettingsSource(),
            });
    }

    public abstract class SettingsSource
    {
        public abstract String GetSetting(String name);
    }

    public class AppSettingsSettingsSource : SettingsSource
    {
        public override string GetSetting(string name)
        {
            return ConfigurationManager.AppSettings.Get(name);
        }
    }

    public abstract class FileSettingsSource : SettingsSource
    {
        static Logger log = LogManager.GetCurrentClassLogger();

        void AttemptLoading()
        {
            try
            {
                loadingAttempted = true;

                var filepath = GetFileName();
                if (File.Exists(filepath))
                {
                    var text = File.ReadAllText(filepath);
                    var doc = XDocument.Parse(text);
                    settings = doc.Element("settings");

                    if (settings == null)
                    {
                        log.Error("File settings at {0} should have root element 'settings'.");
                    }
                    else
                    {
                        log.Info("File settings loaded from {0}.", filepath);
                    }
                }
                else
                {
                    log.Debug("No file settings found at {0}.", filepath);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to load file settings.");
            }
        }

        protected abstract String GetFileName();

        public override string GetSetting(string name)
        {
            if (settings == null)
            {
                if (loadingAttempted) return null;
                AttemptLoading();
                if (settings == null) return null;
            }
            var values =
                from s in settings.Elements("setting")
                let nameAttrib = s.Attribute("name")
                where nameAttrib != null && nameAttrib.Value == name
                select s.Attribute("value");
            var value = values.FirstOrDefault();
            if (value != null) return value.Value;
            return null;
        }

        Boolean loadingAttempted;
        XElement settings;
    }

    public class LocalFileSettingsSource : FileSettingsSource
    {
        protected override string GetFileName()
        {
            var basepath = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
            var filepath = Path.Combine(basepath, "settings.xml");
            return filepath;
        }
    }

    public class HomeFileSettingsSource: FileSettingsSource
    {
        protected override string GetFileName()
        {
            var basepath = Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
            var assemblyName = EntryAssembly.GetName().Name;
            var filepath = Path.Combine(basepath, "settings", $"{assemblyName}.xml");
            return filepath;
        }

        Assembly EntryAssembly => EntryAssemblyAttribute.GetEntryAssembly();
    }

    public class CombinedSettingsSource : SettingsSource
    {
        public CombinedSettingsSource(IEnumerable<SettingsSource> sources)
        {
            this.sources = sources.ToArray();
        }

        public override string GetSetting(string name)
        {
            return sources.Select(s => s.GetSetting(name)).FirstOrDefault(s => s != null);
        }

        SettingsSource[] sources;
    }
}
