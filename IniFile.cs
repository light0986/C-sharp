using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace INIManager
{
    public class IniFile
    {
        private string Path { get; set; } = "";

        public IniFile(string path)
        {
            Path = path;
            if (!File.Exists(Path))
            {
                _ = File.Create(Path);
            }
        }

        #region Search
        public List<string> GetSections()
        {
            string[] Lines = File.ReadAllLines(Path);
            List<string> Sections = new List<string>();
            foreach (string line in Lines)
            {
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    Sections.Add(line.Substring(1, line.Length - 2));
                }
            }

            return Sections;
        }

        public List<string> GetKeys(string Section)
        {
            string[] Lines = File.ReadAllLines(Path);
            List<string> Keys = new List<string>();
            int StartIndex = Lines.ToList().FindIndex(x => x == "[" + Section + "]");
            if (StartIndex != -1)
            {
                for (int i = StartIndex + 1; i < Lines.Length; i++)
                {
                    if (Lines[i].StartsWith("[") && Lines[i].EndsWith("]")) { break; }
                    else
                    {
                        int equalSignPos = Lines[i].IndexOf('=');
                        if (equalSignPos != -1)
                        {
                            Keys.Add(Lines[i].Substring(0, equalSignPos));
                        }
                    }
                }
            }

            return Keys;
        }

        public string GetValue(string Section, string Key)
        {
            string[] Lines = File.ReadAllLines(Path);
            int StartIndex = Lines.ToList().FindIndex(x => x == "[" + Section + "]");
            if (StartIndex != -1)
            {
                for (int i = StartIndex + 1; i < Lines.Length; i++)
                {
                    if (Lines[i].StartsWith("[") && Lines[i].EndsWith("]")) { break; }
                    else
                    {
                        int equalSignPos = Lines[i].IndexOf('=');
                        if (equalSignPos != -1)
                        {
                            if (Lines[i].Substring(0, equalSignPos) == Key)
                            {
                                return Lines[i].Substring(equalSignPos + 1, Lines[i].Length - equalSignPos - 1);
                            }
                        }
                    }
                }
            }

            return null;
        }

        public void GetKeysValues(string Section, ref List<string> Keys, ref List<string> Values)
        {
            string[] Lines = File.ReadAllLines(Path);
            int StartIndex = Lines.ToList().FindIndex(x => x == "[" + Section + "]");
            if (StartIndex != -1)
            {
                for (int i = StartIndex + 1; i < Lines.Length; i++)
                {
                    if (Lines[i].StartsWith("[") && Lines[i].EndsWith("]")) { break; }
                    else
                    {
                        int equalSignPos = Lines[i].IndexOf('=');
                        if (equalSignPos != -1)
                        {
                            Keys.Add(Lines[i].Substring(0, equalSignPos));
                            Values.Add(Lines[i].Substring(equalSignPos + 1, Lines[i].Length - equalSignPos - 1));
                        }
                    }
                }
            }
        }

        public void GetKeysValues(string Section, ref string[] Keys, ref string[] Values)
        {
            List<string> _Keys = new List<string>();
            List<string> _Values = new List<string>();
            GetKeysValues(Section, ref _Keys, ref _Values);

            Keys = _Keys.ToArray();
            Values = _Values.ToArray();
        }

        public Dictionary<string, string> GetKeysValues(string Section)
        {
            List<string> Keys = new List<string>();
            List<string> Values = new List<string>();
            GetKeysValues(Section, ref Keys, ref Values);

            Dictionary<string, string> Dic = new Dictionary<string, string>();
            for (int i = 0; i < Keys.Count; i++)
            {
                Dic.Add(Keys[i], Values[i]);
            }
            return Dic;
        }
        #endregion

        #region Add
        //使用此處資料必須不存在
        public void AddSection(string Section)
        {
            if (Section.Length > 0)
            {
                List<string> Sections = GetSections();
                if (Sections.Count(x => x == Section) == 0)
                {
                    List<string> Lines = File.ReadAllLines(Path).ToList();
                    int SIndex = Lines.IndexOf("[" + Section + "]");
                    if (SIndex == -1)
                    {
                        Lines.Add("[" + Section + "]");
                        File.WriteAllLines(Path, Lines);
                    }
                }
            }
        }

        public void AddKey(string Section, string Key)
        {
            if (Section.Length > 0 && Key.Length > 0)
            {
                List<string> Keys = GetKeys(Section);
                if (Keys.Count(x => x == Key) == 0)
                {
                    List<string> Lines = File.ReadAllLines(Path).ToList();
                    int SIndex = Lines.IndexOf("[" + Section + "]");
                    if (SIndex != -1)
                    {
                        Lines.Insert(SIndex + 1, Key + "=");
                        File.WriteAllLines(Path, Lines);
                    }
                }
            }
        }

        public void AddValue(string Section, string Key, string Value)
        {
            if (Section.Length > 0 && Key.Length > 0)
            {
                List<string> Keys = GetKeys(Section);
                if (Keys.Count(x => x == Key) == 1)
                {
                    List<string> Lines = File.ReadAllLines(Path).ToList();
                    int SIndex = Lines.IndexOf("[" + Section + "]");
                    if (SIndex != -1)
                    {
                        for (int i = SIndex + 1; i < Lines.Count; i++)
                        {
                            if (Lines[i].StartsWith("[") && Lines[i].EndsWith("]")) { break; }

                            if (Lines[i] == Key + "=")
                            {
                                Lines[i] = Key + "=" + Value;
                                break;
                            }
                        }
                        File.WriteAllLines(Path, Lines);
                    }
                }
            }
        }
        #endregion

        #region Change
        //使用此處資料必須存在1
        public void ChangeSection(string Section, string NewSection)
        {
            if (Section.Length > 0 && NewSection.Length > 0)
            {
                List<string> Sections = GetSections();
                if (Sections.Count(x => x == Section) == 1 && Sections.Count(x => x == NewSection) == 0)
                {
                    List<string> Lines = File.ReadAllLines(Path).ToList();
                    int SIndex = Lines.IndexOf("[" + Section + "]");
                    if (SIndex != -1)
                    {
                        Lines[SIndex] = "[" + NewSection + "]";
                        File.WriteAllLines(Path, Lines);
                    }
                }
            }
        }

        public void ChangeKey(string Section, string Key, string newKey)
        {
            if (Section.Length > 0 && Key.Length > 0 && newKey.Length > 0)
            {
                List<string> Keys = GetKeys(Section);
                if (Keys.Count(x => x == Key) == 1 && Keys.Count(x => x == newKey) == 0)
                {
                    List<string> Lines = File.ReadAllLines(Path).ToList();
                    int SIndex = Lines.IndexOf("[" + Section + "]");
                    if (SIndex != -1)
                    {
                        for (int i = SIndex + 1; i < Lines.Count; i++)
                        {
                            if (Lines[i].StartsWith("[") && Lines[i].EndsWith("]")) { break; }

                            int equalSignPos = Lines[i].IndexOf('=');
                            if (equalSignPos != -1)
                            {
                                string nowKey = Lines[i].Substring(0, equalSignPos);
                                if (nowKey == Key)
                                {
                                    string nowValue = Lines[i].Substring(equalSignPos + 1, Lines[i].Length - equalSignPos - 1);
                                    Lines[i] = newKey + "=" + nowValue;
                                    break;
                                }
                            }
                        }
                        File.WriteAllLines(Path, Lines);
                    }
                }
            }
        }

        public void ChangeValue(string Section, string Key, string Value)
        {
            if (Section.Length > 0 && Key.Length > 0 && Value.Length > 0)
            {
                List<string> Keys = GetKeys(Section);
                if (Keys.Count(x => x == Key) == 1)
                {
                    List<string> Lines = File.ReadAllLines(Path).ToList();
                    int SIndex = Lines.IndexOf("[" + Section + "]");
                    if (SIndex != -1)
                    {
                        for (int i = SIndex + 1; i < Lines.Count; i++)
                        {
                            if (Lines[i].StartsWith("[") && Lines[i].EndsWith("]")) { break; }

                            int equalSignPos = Lines[i].IndexOf('=');
                            if (equalSignPos != -1)
                            {
                                string nowKey = Lines[i].Substring(0, equalSignPos);
                                if (nowKey == Key)
                                {
                                    Lines[i] = Key + "=" + Value;
                                    break;
                                }
                            }
                        }
                        File.WriteAllLines(Path, Lines);
                    }
                }
            }
        }
        #endregion

        #region Delete
        //使用此處資料必須存在1
        public void DeleteSection(string Section)
        {
            if (Section.Length > 0)
            {
                List<string> Sections = GetSections();
                if (Sections.Count(x => x == Section) == 1)
                {
                    List<string> Lines = File.ReadAllLines(Path).ToList();
                    int SIndex = Lines.IndexOf("[" + Section + "]");
                    if (SIndex != -1)
                    {
                        do
                        {
                            Lines.RemoveAt(SIndex);

                            if (SIndex < Lines.Count)
                            {
                                if (Lines[SIndex].StartsWith("[") && Lines[SIndex].EndsWith("]"))
                                {
                                    break;
                                }
                            }
                            else { break; }
                        }
                        while (true);
                        File.WriteAllLines(Path, Lines);
                    }
                }
            }
        }

        public void DeleteKey(string Section, string Key)
        {
            if (Section.Length > 0 && Key.Length > 0)
            {
                List<string> Keys = GetKeys(Section);
                if (Keys.Count(x => x == Key) == 1)
                {
                    List<string> Lines = File.ReadAllLines(Path).ToList();
                    int SIndex = Lines.IndexOf("[" + Section + "]");
                    if (SIndex != -1)
                    {
                        for (int i = SIndex + 1; i < Lines.Count; i++)
                        {
                            if (Lines[i].StartsWith("[") && Lines[i].EndsWith("]")) { break; }

                            if (Lines[i] == Key + "=")
                            {
                                Lines.RemoveAt(i);
                                break;
                            }
                        }
                        File.WriteAllLines(Path, Lines);
                    }
                }
            }
        }

        public void DeleteValue(string Section, string Key)
        {
            if (Section.Length > 0 && Key.Length > 0)
            {
                List<string> Keys = GetKeys(Section);
                if (Keys.Count(x => x == Key) == 1)
                {
                    List<string> Lines = File.ReadAllLines(Path).ToList();
                    int SIndex = Lines.IndexOf("[" + Section + "]");
                    if (SIndex != -1)
                    {
                        for (int i = SIndex + 1; i < Lines.Count; i++)
                        {
                            if (Lines[i].StartsWith("[") && Lines[i].EndsWith("]")) { break; }

                            int equalSignPos = Lines[i].IndexOf('=');
                            if (equalSignPos != -1)
                            {
                                string nowKey = Lines[i].Substring(0, equalSignPos);
                                if (nowKey == Key)
                                {
                                    Lines[i] = Key + "=";
                                    break;
                                }
                            }
                        }
                        File.WriteAllLines(Path, Lines);
                    }
                }
            }
        }
        #endregion
    }
}
