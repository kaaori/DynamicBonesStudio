using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// Read/Write .ini Files
/// 
/// Version 1, 2009-08-15
/// http://www.Stum.de
/// </summary>
/// <remarks>
/// It supports the simple .INI Format:
/// 
/// [SectionName]
/// Key1=Value1
/// Key2=Value2
/// 
/// [Section2]
/// Key3=Value3
/// 
/// You can have empty lines (they are ignored), but comments are not supported
/// Key4=Value4 ; This is supposed to be a comment, but will be part of Value4
/// 
/// Whitespace is not trimmed from the beginning and end of either Key and Value
/// 
/// Licensed under WTFPL
/// http://sam.zoy.org/wtfpl/
/// </remarks>
public class IniFile
{
    private Dictionary<string, Dictionary<string, string>> _iniFileContent;
    public List<string> SectionNames = new List<string>();
    private string Filename;
    private readonly Regex _sectionRegex = new Regex(@"(?<=\[)(?<SectionName>[^\]]+)(?=\])");
    private readonly Regex _keyValueRegex = new Regex(@"(?<Key>[^=]+)=(?<Value>.+)");

    public IniFile() : this(null) { }

    public IniFile(string filename)
    {
        _iniFileContent = new Dictionary<string, Dictionary<string, string>>();
        Filename = filename;
        if (filename != null) Load(filename);
    }

    public void Refresh()
    {
        if (Filename != "")
        {
            Load(Filename);
        }
    }

    /// <summary>
    /// Get a specific value from the .ini file
    /// </summary>
    /// <param name="sectionName"></param>
    /// <param name="key"></param>
    /// <returns>The value of the given key in the given section, or NULL if not found</returns>
    public string GetValue(string sectionName, string key)
    {
        if (_iniFileContent.ContainsKey(sectionName) && _iniFileContent[sectionName].ContainsKey(key))
            return _iniFileContent[sectionName][key];
        else
            return null;
    }

    /// <summary>
    /// Set a specific value in a section
    /// </summary>
    /// <param name="sectionName"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void SetValue(string sectionName, string key, string value)
    {
        if (!_iniFileContent.ContainsKey(sectionName)) _iniFileContent[sectionName] = new Dictionary<string, string>();
        _iniFileContent[sectionName][key] = value;

        this.Save(Filename);
    }

    /// <summary>
    /// Get all the Values for a section
    /// </summary>
    /// <param name="sectionName"></param>
    /// <returns>A Dictionary with all the Key/Values for that section (maybe empty but never null)</returns>
    public Dictionary<string, string> GetSection(string sectionName)
    {
        if (_iniFileContent.ContainsKey(sectionName))
            return new Dictionary<string, string>(_iniFileContent[sectionName]);
        else
            return new Dictionary<string, string>();
    }

    /// <summary>
    /// Set an entire sections values
    /// </summary>
    /// <param name="sectionName"></param>
    /// <param name="sectionValues"></param>
    public void SetSection(string sectionName, IDictionary<string, string> sectionValues)
    {
        if (sectionValues == null) return;
        _iniFileContent[sectionName] = new Dictionary<string, string>(sectionValues);

        this.Save(Filename);
    }


    /// <summary>
    /// Load an .INI File
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    public bool Load(string filename)
    {
        if (File.Exists(filename))
        {
            try
            {
                SectionNames.Clear();
                var content = File.ReadAllLines(filename);
                _iniFileContent = new Dictionary<string, Dictionary<string, string>>();
                string currentSectionName = string.Empty;
                foreach (var line in content)
                {
                    Match m = _sectionRegex.Match(line);
                    if (m.Success)
                    {
                        currentSectionName = m.Groups["SectionName"].Value;
                        SectionNames.Add(currentSectionName);
                        UnityEngine.Debug.Log("Added to section names");
                    }
                    else
                    {
                        m = _keyValueRegex.Match(line);
                        if (m.Success)
                        {
                            string key = m.Groups["Key"].Value;
                            string value = m.Groups["Value"].Value;

                            Dictionary<string, string> kvpList;
                            if (_iniFileContent.ContainsKey(currentSectionName))
                            {
                                kvpList = _iniFileContent[currentSectionName];
                            }
                            else
                            {
                                kvpList = new Dictionary<string, string>();
                            }
                            kvpList[key] = value;
                            _iniFileContent[currentSectionName] = kvpList;
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.Log(ex);
                return false;
            }

        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Save the content of this class to an INI File
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    private bool Save(string filename)
    {
        var sb = new StringBuilder();
        if (_iniFileContent != null)
        {
            foreach (var sectionName in _iniFileContent)
            {
                sb.AppendFormat("[{0}]\r\n", sectionName.Key);
                foreach (var keyValue in sectionName.Value)
                {
                    sb.AppendFormat("{0}={1}\r\n", keyValue.Key, keyValue.Value);
                }
            }
        }
        try
        {
            File.WriteAllText(filename, sb.ToString());
            return true;
        }
        catch
        {
            return false;
        }
    }
}