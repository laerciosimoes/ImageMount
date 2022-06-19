using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ImageMounter.IO
{


    /// <summary>
    /// Class that caches a text INI file
    /// </summary>
    [ComVisible(false)]
    public class CachedIniFile : NullSafeDictionary<string, NullSafeDictionary<string, string>>
    {
        protected override NullSafeDictionary<string, string> GetDefaultValue(string Key)
        {
            NullSafeStringDictionary new_section =
                new NullSafeStringDictionary(StringComparer.CurrentCultureIgnoreCase);
            Add(Key, new_section);
            return new_section;
        }

        /// <summary>
        /// Flushes registry mapping for all INI files. is thrown.
        /// </summary>
        public static void Flush()
        {
            UnsafeNativeMethods.WritePrivateProfileString(
                null /* TODO Change to default(_) if this is not a reference type */,
                null /* TODO Change to default(_) if this is not a reference type */,
                null /* TODO Change to default(_) if this is not a reference type */,
                null /* TODO Change to default(_) if this is not a reference type */);
        }

        public static IEnumerable<string> EnumerateFileSectionNames(string filename)
        {


            var sectionnames = new char[32766];

            var size = UnsafeNativeMethods.GetPrivateProfileSectionNames(sectionnames, sectionnames.Length,
                filename);

            return NativeFileIO.ParseDoubleTerminatedString(sectionnames, size);
        }

        public static IEnumerable<KeyValuePair<string, string>> EnumerateFileSectionValuePairs(string filename,
            string section)
        {

            var valuepairs = new char[32766];

            var size = UnsafeNativeMethods.GetPrivateProfileSection(section, valuepairs, valuepairs.Length,
                filename);

            foreach (var valuepair in NativeFileIO.ParseDoubleTerminatedString(valuepairs, size))
            {
                var pos = valuepair.IndexOf('=');

                if (pos < 0)
                    continue;

                var key = valuepair.Remove(pos);
                var value = valuepair.Substring(pos + 1);

                yield return new KeyValuePair<string, string>(key, value);
            }
        }

        /// <summary>
        ///     Saves a value to an INI file by calling Win32 API function WritePrivateProfileString. If call fails and exception
        ///     is thrown.
        ///     </summary>
        ///     <param name="FileName">Name and path of INI file where to save value</param>
        ///     <param name="SectionName">Name of INI file section where to save value</param>
        ///     <param name="SettingName">Name of value to save</param>
        ///     <param name="Value">Value to save</param>
        public static void SaveValue(string FileName, string SectionName, string SettingName, string Value)
        {
            NativeFileIO.Win32Try(
                UnsafeNativeMethods.WritePrivateProfileString(SectionName, SettingName, Value, FileName));
        }

        /// <summary>
        ///     Saves a current value from this object to an INI file by calling Win32 API function WritePrivateProfileString.
        ///     If call fails and exception is thrown.
        ///     </summary>
        ///     <param name="FileName">Name and path of INI file where to save value</param>
        ///     <param name="SectionName">Name of INI file section where to save value</param>
        ///     <param name="SettingName">Name of value to save</param>
        public void SaveValue(string FileName, string SectionName, string SettingName)
        {
            SaveValue(SectionName, SettingName, Item(SectionName)(SettingName), FileName);
        }

        /// <summary>
        ///     Saves a current value from this object to INI file that this object last loaded values from, either through constructor
        ///     call with filename parameter or by calling Load method with filename parameter.
        ///     Operation is carried out by calling Win32 API function WritePrivateProfileString.
        ///     If call fails and exception is thrown.
        ///     </summary>
        ///     <param name="SectionName">Name of INI file section where to save value</param>
        ///     <param name="SettingName">Name of value to save</param>
        public void SaveValue(string SectionName, string SettingName)
        {
            if (string.IsNullOrEmpty(Filename))
                throw new InvalidOperationException("Filename property not set on this object.");
            SaveValue(SectionName, SettingName, Item(SectionName)(SettingName), Filename);
        }

        /// <summary>
        ///     Saves current contents of this object to INI file that this object last loaded values from, either through constructor
        ///     call with filename parameter or by calling Load method with filename parameter.
        ///     </summary>
        public void Save()
        {
            File.WriteAllText(Filename, ToString(), Encoding);
        }

        /// <summary>
        ///     Saves current contents of this object to an INI file. If the file already exists, it is overwritten.
        ///     </summary>
        public void Save(string Filename, Encoding Encoding)
        {
            File.WriteAllText(Filename, ToString(), Encoding);
        }

        /// <summary>
        ///     Saves current contents of this object to an INI file. If the file already exists, it is overwritten.
        ///     </summary>
        public void Save(string Filename)
        {
            File.WriteAllText(Filename, ToString(), Encoding);
        }

        public override string ToString()
        {
            using (StringWriter Writer = new StringWriter())
            {
                WriteTo(Writer);
                return Writer.ToString();
            }
        }

        public void WriteTo(Stream Stream)
        {
            WriteTo(new StreamWriter(Stream, Encoding) { AutoFlush = true });
        }

        public void WriteTo(TextWriter Writer)
        {
            WriteSectionTo(string.Empty, Writer);
            foreach (var SectionKey in Keys)
            {
                if (string.IsNullOrEmpty(SectionKey))
                    continue;

                WriteSectionTo(SectionKey, Writer);
            }

            Writer.Flush();
        }

        public void WriteSectionTo(string SectionKey, TextWriter Writer)
        {
            if (!ContainsKey(SectionKey))
                return;

            var Section = Item(SectionKey);

            var any_written = false;

            if (!string.IsNullOrEmpty(SectionKey))
            {
                Writer.WriteLine($"[{SectionKey}]");
                any_written = true;
            }

            foreach (var key in Section.Keys.OfType<string>())
            {
                Writer.WriteLine($"{key}={Section(key)}");
                any_written = true;
            }

            if (any_written)
                Writer.WriteLine();
        }

        /// <summary>
        ///     Name of last INI file loaded into this object.
        ///     </summary>
        public string Filename { get; }

        /// <summary>
        ///     Text encoding of last INI file loaded into this object.
        ///     </summary>
        public Encoding Encoding { get; }

        /// <summary>
        ///     Creates a new empty CachedIniFile object
        ///     </summary>
        public CachedIniFile() : base(StringComparer.CurrentCultureIgnoreCase)
        {
        }

        /// <summary>
        ///     Creates a new CachedIniFile object and fills it with the contents of the specified
        ///     INI file
        ///     </summary>
        ///     <param name="Filename">Name of INI file to read into the created object</param>
        ///     <param name="Encoding">Text encoding used in INI file</param>
        public CachedIniFile(string Filename, Encoding Encoding) : this()
        {
            Load(Filename, Encoding);
        }

        /// <summary>
        ///     Creates a new CachedIniFile object and fills it with the contents of the specified
        ///     INI file
        ///     </summary>
        ///     <param name="Filename">Name of INI file to read into the created object</param>
        public CachedIniFile(string Filename) : this(Filename, Encoding.Default)
        {
        }

        /// <summary>
        ///     Creates a new CachedIniFile object and fills it with the contents of the specified
        ///     INI file
        ///     </summary>
        ///     <param name="Stream">Stream that contains INI settings to read into the created object</param>
        ///     <param name="Encoding">Text encoding used in INI file</param>
        public CachedIniFile(Stream Stream, Encoding Encoding) : this()
        {
            Load(Stream, Encoding);
        }

        /// <summary>
        ///     Creates a new CachedIniFile object and fills it with the contents of the specified
        ///     INI file
        ///     </summary>
        ///     <param name="Stream">Stream that contains INI settings to read into the created object</param>
        public CachedIniFile(Stream Stream) : this(Stream, Encoding.Default)
        {
        }

        /// <summary>
        ///     Reloads settings from disk file. This is only supported if this object was created using
        ///     a constructor that takes a filename or if a Load() method that takes a filename has been
        ///     called earlier.
        ///     </summary>
        public void Reload()
        {
            Load(Filename, Encoding);
        }

        /// <summary>
        ///     Loads settings from an INI file into this CachedIniFile object. Existing settings
        ///     in object is replaced.
        ///     </summary>
        ///     <param name="Filename">INI file to load</param>
        public void Load(string Filename)
        {
            Load(Filename, Encoding.Default);
        }

        /// <summary>
        ///     Loads settings from an INI file into this CachedIniFile object. Existing settings
        ///     in object is replaced.
        ///     </summary>
        ///     <param name="Filename">INI file to load</param>
        ///     <param name="Encoding">Text encoding for INI file</param>
        public void Load(string Filename, Encoding Encoding)
        {
            Filename = Filename;
            Encoding = Encoding;

            try
            {
                using (FileStream fs = new FileStream(Filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                           20480, FileOptions.SequentialScan))
                {
                    Load(fs, Encoding);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        ///     Loads settings from an INI file into this CachedIniFile object. Existing settings
        ///     in object is replaced.
        ///     </summary>
        ///     <param name="Stream">Stream containing INI file data</param>
        ///     <param name="Encoding">Text encoding for INI stream</param>
        public void Load(Stream Stream, Encoding Encoding)
        {
            try
            {
                StreamReader sr = new StreamReader(Stream, Encoding, false, 1048576);

                Load(sr);

                Encoding = Encoding;
            }
            catch
            {
            }
        }

        /// <summary>
        ///     Loads settings from an INI file into this CachedIniFile object using Default text
        ///     encoding. Existing settings in object is replaced.
        ///     </summary>
        ///     <param name="Stream">Stream containing INI file data</param>
        public void Load(Stream Stream)
        {
            Load(Stream, Encoding.Default);
        }

        /// <summary>
        ///     Loads settings from an INI file into this CachedIniFile object. Existing settings
        ///     in object is replaced.
        ///     </summary>
        ///     <param name="Stream">Stream containing INI file data</param>
        public void Load(TextReader Stream)
        {
            lock (SyncRoot)
            {
                try
                {
                    {
                        var withBlock = Stream;
                        var CurrentSection = Item(string.Empty);

                        do
                        {
                            var Linestr = withBlock.ReadLine();
                            if (Linestr == null)
                                break;
                            var Line = Linestr.AsSpan().Trim();
                            if (Line.Length == 0 || Line.StartsWith(";".AsSpan(), StringComparison.Ordinal))
                                continue;
                            if (Line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                                Line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                            {
                                var SectionKey = Line.Slice(1, Line.Length - 2).Trim().ToString();
                                CurrentSection = Item(SectionKey);
                                continue;
                            }

                            var EqualSignPos = Line.IndexOf('=');
                            if (EqualSignPos < 0)
                                continue;
                            var Key = Line.Slice(0, EqualSignPos).Trim().ToString();
                            var Value = Line.Slice(EqualSignPos + 1).Trim().ToString();
                            CurrentSection(Key) = Value;
                        } while (true);
                    }
                }
                catch
                {
                }
            }
        }
    }
}