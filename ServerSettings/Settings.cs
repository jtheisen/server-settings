using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace IronStone.ServerSettings
{
    /// <summary>
    /// This class provides access to the server settings.
    /// </summary>
    public static class Settings
    {
        /// <summary>
        /// Looks up a setting by name and provides the string value or the given default if no value was found.
        /// </summary>
        /// <param name="name">The setting's name.</param>
        /// <param name="def">The default if no setting should be found.</param>
        /// <returns>The found setting's value or the provided default.</returns>
        public static String GetString(String name, String def)
        {
            return SettingsConfiguration.RootSettingsSource.GetSetting(name) ?? def;
        }

        /// <summary>
        /// Looks up a setting by name and provides the string value or throws if no value was found.
        /// </summary>
        /// <param name="name">The setting's name.</param>
        /// <returns>The found setting's value.</returns>
        public static String GetString(String name)
        {
            var result = GetString(name, null);
            if (result == null) throw new ArgumentException($"Setting '{name}' is not set.");
            return result;
        }

        /// <summary>
        /// Looks up a setting by name and provides the integer value.
        /// </summary>
        /// <param name="name">The setting's name.</param>
        /// <param name="def">The default.</param>
        /// <returns>The found setting's value or the default if no setting was found.</returns>
        /// <exception cref="System.Exception">When the value can't be parsed.</exception>
        public static Int32 GetInt32(String name, Int32? def = null)
        {
            var s = GetString(name, "");
            if (s == "" && def.HasValue) return def.Value;
            if (!Int32.TryParse(s, out var result)) if (def.HasValue) return def.Value; else Throw(name, s, "Int32");
            return result;
        }

        /// <summary>
        /// Looks up a setting by name and provides the boolean value.
        /// </summary>
        /// <param name="name">The setting's name.</param>
        /// <param name="def">The default.</param>
        /// <returns>The found setting's value or the default if no setting was found.</returns>
        /// <exception cref="System.Exception">When the value can't be parsed.</exception>
        public static Boolean GetBoolean(String name, Boolean? def = null)
        {
            var s = GetString(name, "");
            if (s == "" && def.HasValue) return def.Value;
            if (!Boolean.TryParse(s, out var result)) if (def.HasValue) return def.Value; else Throw(name, s, "Boolean");
            return result;
        }

        static void Throw(String name, String value, String type)
        {
            throw new Exception($"Setting '{name}' has value '{value}' which can't be parsed into a {type}.");
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
                new EnvironmentSettingsSource()
            });
    }

    public abstract class SettingsSource
    {
        /// <summary>
        /// Provides the settings value of the setting with the given name or null if no such value is provided.
        /// </summary>
        /// <param name="name">The name of the setting.</param>
        /// <returns>The provided value or null.</returns>
        public abstract String GetSetting(String name);
    }

    public class AppSettingsSettingsSource : SettingsSource
    {
        /// <summary>
        /// Provides the settings value of the setting with the given name or null if no such value is provided.
        /// </summary>
        /// <param name="name">The name of the setting.</param>
        /// <returns>The provided value or null.</returns>
        public override string GetSetting(string name)
        {
            return ConfigurationManager.AppSettings.Get(name);
        }
    }

    /// <summary>
    /// This source provides settings from an xml file. The root element is settings, in which setting items
    /// live, each having attributes name and value.
    /// </summary>
    public abstract class FileSettingsSource : SettingsSource
    {
        static Logger log = LogManager.GetCurrentClassLogger();

        void AttemptLoading()
        {
            var filepath = GetFileNameSafely();

            if (filepath == null) return;

            try
            {
                loadingAttempted = true;

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
                log.Error(ex, "Failed to load file settings at {0}.", filepath);
            }
        }

        String GetFileNameSafely()
        {
            try
            {
                return GetFileName();
            }
            catch (Exception ex)
            {
                log.Warn(ex, "No file settings because: " + ex.Message);

                return null;
            }
        }

        /// <summary>
        /// An implementation provides the file name of the settings file.
        /// </summary>
        /// <returns></returns>
        protected abstract String GetFileName();

        /// <summary>
        /// Provides the settings value of the setting with the given name or null if no such value is provided.
        /// </summary>
        /// <param name="name">The name of the setting.</param>
        /// <returns>The provided value or null.</returns>
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

    /// <summary>
    /// This source is a FileSettingsSource with the file expected to live at
    /// (entry-assembly-location)/settings.xml.
    /// </summary>
    public class LocalFileSettingsSource : FileSettingsSource
    {
        protected override string GetFileName()
        {
            var basepath = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
            var filepath = Path.Combine(basepath, "settings.xml");
            return filepath;
        }
    }

    /// <summary>
    /// This source is a FileSettingsSource with the file expected to live at
    /// (user-home-directory)\settings\(entry-assembly-name).xml.
    /// </summary>
    public class HomeFileSettingsSource : FileSettingsSource
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

    /// <summary>
    /// This source provides settings from the environment as per the Azure App Services
    /// naming convention as APPSETTING_{name}.
    /// </summary>
    public class EnvironmentSettingsSource : SettingsSource
    {
        public override string GetSetting(string name)
        {
            var result = Environment.ExpandEnvironmentVariables($"%APPSETTING_{name}%");
            if (result.StartsWith("%")) return null;
            return result;
        }
    }

    /// <summary>
    /// This source combines multiple nested sources. The sources are looked up in order
    /// until a source provides a value for the given name, which is the value returned
    /// by the combined settings source.
    /// </summary>
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
