using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;


class Common
{
    static public string GetApplicationPath()
    {
        string ApplicationPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);
        return ApplicationPath;
    }

    static public bool SaveToFile(byte[] dataBuf, string fileName)
    {
        try
        {
            using (FileStream fs = new FileStream(fileName, FileMode.OpenOrCreate))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    bw.Write(dataBuf);
                    return true;
                }
            }
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    static public bool LoadBufFromFile(string fileName, out byte[] dataBuf)
    {
        dataBuf = null;
        try
        {
            byte[] data = null;
            using (FileStream fs = new FileStream(fileName, FileMode.OpenOrCreate))
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    if (fs.Length > 0)
                    {
                        data = new byte[fs.Length];
                        br.Read(data, 0, data.Length);
                        dataBuf = data;
                        return true;
                    }
                }
            }
            return false;
        }
        catch (System.Exception)
        {
            return false;
        }
    }
    static public void TrimString(ref string strLine)
    {
        if (string.IsNullOrEmpty(strLine))
        {
            return;
        }
        try
        {
            int commentPos = strLine.IndexOf("//");
            if (commentPos >= 0)
            {
                strLine = strLine.Substring(0, commentPos);
            }
            strLine = System.Text.RegularExpressions.Regex.Replace(strLine, "\\s+", " ");
            strLine.Trim();
            if (strLine.Length <= 0)
            {
                return;
            }
            if (strLine[strLine.Length - 1] == ' ' && strLine.Length > 0)
            {
                strLine = strLine.Substring(0, strLine.Length - 1);
            }
            if (strLine.Length > 0 && strLine[0] == ' ')
            {
                strLine = strLine.Substring(1, strLine.Length - 1);
            }
        }
        catch (Exception)
        {
            return;
        }
    }

    public static bool loadIni(string fullPathName, out Dictionary<string, string> retDic)
    {
        retDic = new Dictionary<string, string>();

        string filePath = fullPathName;

        try
        {
            Encoding encoding = Encoding.ASCII;// Encoding.UTF8;
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader sr = new StreamReader(fs, encoding))
                {
                    string strLine;
                    while ((strLine = sr.ReadLine()) != null)
                    {
                        //TrimString(ref strLine);
                        if (strLine.Length < 1) { continue; }

                        int pos = strLine.IndexOf('=');
                        if (pos <= 0)
                            continue;

                        string name = strLine.Substring(0, pos);
                        string value = strLine.Substring(pos + 1);

                        TrimString(ref name);
                        //TrimString(ref value);

                        if (string.IsNullOrEmpty(name))// || string.IsNullOrEmpty(value))
                            continue;


                        name = name.ToLower();
                        //value = value.ToLower();

                        if (!retDic.ContainsKey(name))
                        {
                            retDic[name] = value;
                        }
                    }
                }
            }
            return true;
        }
        catch (Exception )
        {
            return false;
        }

    }


}

