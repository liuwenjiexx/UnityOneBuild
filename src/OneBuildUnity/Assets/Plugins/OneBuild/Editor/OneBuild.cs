﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode.Custom;
using UnityEngine;


namespace UnityEditor.Build
{

    public static class OneBuild
    {
        public static string ConfigDir = "Assets/Config";
        public static bool log = true;

        static Dictionary<string, string[]> configs;

        public static string VersionFileName = "version.txt";
        private static string KeyPrefix = "buildplayer.";
        public static string BuildOutputPathKey = KeyPrefix + "outputpath";
        public static string BuildScenesKey = KeyPrefix + "scenes";
        public static string BuildOptionsKey = KeyPrefix + "options";
        public static string BuildShowFolderKey = KeyPrefix + "showfolder";

        public static string OutputPath
        {
            get { return EditorPrefs.GetString(BuildOutputPathKey, null); }
            set { EditorPrefs.SetString(BuildOutputPathKey, value); }
        }
        public static string[] Scenes
        {
            get
            {
                var str = EditorPrefs.GetString(BuildScenesKey, null);
                if (string.IsNullOrEmpty(str))
                    return new string[0];
                return str.Split(',');
            }
            set { EditorPrefs.SetString(BuildScenesKey, value == null ? string.Empty : string.Join(",", value)); }
        }
        public static BuildOptions Options
        {
            get
            {
                var n = EditorPrefs.GetInt(BuildOptionsKey, (int)BuildOptions.None);
                return (BuildOptions)n;
            }
            set { EditorPrefs.SetInt(BuildOptionsKey, (int)value); }
        }
        public static bool ShowFolder
        {
            get { return EditorPrefs.GetBool(BuildShowFolderKey, false); }
            set { EditorPrefs.SetBool(BuildShowFolderKey, value); }
        }


        static string VersionPath
        {
            get { return ConfigDir + "/version.txt"; }
        }
        private const string LastBuildVersionKey = "OneBuild.LastBuildVersion";
        public static string BuildVersion
        {
            get { return PlayerPrefs.GetString(LastBuildVersionKey, string.Empty); }
            set
            {
                PlayerPrefs.SetString(LastBuildVersionKey, value);
                PlayerPrefs.Save();
            }
        }
        public static Dictionary<string, string[]> Configs
        {
            get { return configs; }
        }

        public static Dictionary<string, object> GlobalVariables;

        public static HashSet<string> CustomMembers = new HashSet<string>()
        {
            "version",
            "versionCode",
            "output.dir",
            "output.filename",
            "loggingError",
            "loggingAssert",
            "loggingWarning",
            "loggingLog",
            "loggingException",
            "clearLog",
            "build.BuildOptions",
            "build.BuildAssetBundleOptions",
            "build.scenes",
            "build.assets",
            "build.showfolder",
        };


        //public static string version;
        //public static string versionCode;
        //    "output.dir",
        //    "output.filename",
        //    "loggingError",
        //    "loggingAssert",
        //    "loggingWarning",
        //    "loggingLog",
        //    "loggingException",
        //    "clearLog",
        //    "build.BuildOptions",
        //    "build.BuildAssetBundleOptions",
        //    "build.scenes",
        //    "build.assets", 



        public static Dictionary<string, string> append = new Dictionary<string, string>()
        {
            { "ScriptingDefineSymbols",";" }
        };

        [MenuItem("Build/Build", priority = 1)]
        public static void BuildMenu()
        {
            Build(GetVersion(null));
        }
        [MenuItem("Build/Update Config", priority = 2)]
        public static void UpdateConfig1()
        {
            UpdateConfig(GetVersion(null), true);
        }

        //[MenuItem("Build/Build Assets", priority = 3)]
        public static void BuildAssetsMenu()
        {
            Build(GetVersion("assets"));
        }

        [MenuItem("Build/Build (Debug)", priority = 20)]
        public static void BuildDebug()
        {
            Build(GetVersion("debug"));
        }


        [MenuItem("Build/Update Config (Debug)", priority = 21)]
        public static void UpdateConfigDebug()
        {
            string version = GetVersion("debug");
            UpdateConfig(version, true);
        }

        public static void Build(string version)
        {
            BuildVersion = version;
            BuildPlayer();
        }




        public static string GetVersion(string version)
        {
            string currentPath = VersionPath;
            string ver = "";
            if (File.Exists(currentPath))
            {
                ver = File.ReadAllText(currentPath);
                ver = ver.Trim();
            }

            if (!string.IsNullOrEmpty(version))
            {
                version = version.Trim();
                if (string.IsNullOrEmpty(ver))
                {
                    ver = version;
                }
                else
                {
                    if (!ver.EndsWith(","))
                    {
                        ver += ",";
                    }
                    ver += version;
                }
            }

            return ver;
        }

        class Member
        {
            public Type type;
            public string memberName;
            public string[] values;
        }


        private static Dictionary<string, string[]> LoadConfig(string version, StringBuilder log = null)
        {
            var configs = new Dictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase);
            Dictionary<string, int> matchs = new Dictionary<string, int>();

            Regex nsRegex = new Regex("type:([^ $]+)", RegexOptions.IgnoreCase);

            GlobalVariables = new Dictionary<string, object>()
            {
                {"DateTime",DateTime.Now },
                {"BuildTargetGroup" , EditorUserBuildSettings.selectedBuildTargetGroup}
            };

            if (!string.IsNullOrEmpty(version))
            {
                foreach (var part in version.Split(',', '\r', '\n'))
                {
                    string ver = part.Trim();
                    if (!string.IsNullOrEmpty(ver))
                    {
                        matchs[ver.ToLower()] = 10;
                    }
                }
            }

            Dictionary<string, int> files = new Dictionary<string, int>();

            matchs.Add(EditorUserBuildSettings.selectedBuildTargetGroup.ToString().ToLower(), 1);

            foreach (var file in Directory.GetFiles(ConfigDir))
            {
                string[] tmp = Path.GetFileNameWithoutExtension(file).Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                tmp = tmp.Distinct().ToArray();
                if (tmp.Where(o => matchs.ContainsKey(o.Trim().ToLower()))
                   .Count() == tmp.Length)
                {
                    files.Add(file, tmp.Sum(o => matchs[o]));
                }
            }
            if (log != null)
                log.Append("*** config file ***").AppendLine();

            Dictionary<string, Type> types = new Dictionary<string, Type>();

            foreach (var file in files.OrderBy(o => o.Value).Select(o => o.Key))
            {
                if (log != null)
                    log.Append(file).AppendLine();
                XmlDocument doc;
                doc = new XmlDocument();
                doc.Load(file);
                foreach (XmlNode node in doc.DocumentElement.SelectNodes("*"))
                {
                    string name;
                    string[] values;
                    name = node.LocalName;
                    Match m = null;
                    if (!string.IsNullOrEmpty(node.NamespaceURI))
                        m = nsRegex.Match(node.NamespaceURI);
                    if (m != null && m.Success)
                    {
                        string typeName;
                        typeName = m.Groups[1].Value;
                        if (!string.IsNullOrEmpty(typeName))
                        {
                            Type type;
                            if (!types.TryGetValue(typeName, out type))
                            {
                                type = Type.GetType(typeName);
                                if (type == null)
                                {
                                    type = AppDomain.CurrentDomain.GetAssemblies()
                                            .SelectMany(o => o.GetTypes())
                                            .Where(o => string.Equals(o.Name, typeName, StringComparison.InvariantCultureIgnoreCase)
                                            || string.Equals(o.FullName, typeName, StringComparison.InvariantCultureIgnoreCase))
                                            .FirstOrDefault();
                                }
                                if (type == null)
                                    throw new Exception("not found type:" + typeName);
                                types[typeName] = type;
                            }
                        }
                    }
                    if (name == "showfolder")
                    {
                        string s = typeof(OneBuild).FullName;
                        s = typeof(OneBuild).AssemblyQualifiedName;
                        Debug.Log(node.Name + "|" + node.NamespaceURI + "|");
                    }
                    var valueNodes = node.SelectNodes("*");
                    if (valueNodes.Count > 0)
                    {
                        values = new string[valueNodes.Count];
                        for (int i = 0; i < valueNodes.Count; i++)
                        {
                            values[i] = valueNodes[i].InnerText;
                        }
                    }
                    else
                    {
                        values = new string[] { node.InnerText };
                    }
                    if (append.ContainsKey(name))
                    {
                        string[] oldValue = null;
                        if (configs.ContainsKey(name))
                        {
                            oldValue = configs[name];
                            if (oldValue[0] != null)
                                oldValue[0] = oldValue[0].TrimEnd();
                        }
                        if (oldValue == null || string.IsNullOrEmpty(oldValue[0]))
                        {
                            configs[name] = values;
                        }
                        else
                        {
                            if (oldValue[0].EndsWith(append[name]))
                            {
                                oldValue[0] = oldValue[0] + values[0];
                            }
                            else
                            {
                                oldValue[0] = oldValue[0] + append[name] + values[0];
                            }
                        }
                    }
                    else
                    {
                        configs[name] = values;
                    }
                }


            }
            ReplaceTemplate(configs);

            if (log != null)
            {
                log.Append("*** config file ***").AppendLine();

                log.Append("config data:").AppendLine();

                var tmp = configs.ToDictionary(o => o.Key, o => string.Join(",", o.Value));
                log.Append(JsonUtility.ToJson(new Serialization<string, string>(tmp), true))
                    .AppendLine();
                //Debug.Log(sb.ToString());
            }

            return configs;
        }


        static object ParseEnum(Type enumType, string str)
        {
            if (!enumType.IsEnum)
                throw new Exception("Not Enum Type: " + enumType.FullName);
            if (enumType.IsDefined(typeof(FlagsAttribute), true))
            {
                return Enum.Parse(enumType, str.Replace(' ', ','));
            }
            else
            {
                return Enum.Parse(enumType, str);
            }
        }



        static MemberInfo FindSetMember(string typeAndMember, string[] args)
        {
            string[] parts = typeAndMember.Split('.');
            MemberInfo member = null;
            Type type = null;
            string memberName;
            if (parts.Length > 1)
            {
                memberName = parts[parts.Length - 1];
                string typeName;
                typeName = typeAndMember.Substring(0, typeAndMember.LastIndexOf('.'));
                type = Type.GetType(typeName);

                if (type == null)
                    type = FindType(typeName);
                if (type == null)
                    type = FindType("UnityEditor." + typeName);

            }
            else
            {
                memberName = parts[0];
            }

            if (type != null)
            {
                member = FindSetMember(type, memberName, args);
            }
            else
            {
                member = FindSetMember(typeof(PlayerSettings), memberName, args);
                if (member == null)
                    member = FindSetMember(typeof(EditorUserBuildSettings), memberName, args);
            }

            return member;
        }

        static MemberInfo FindSetMember(Type type, string memberName, string[] args)
        {
            string lowerName = memberName.ToLower();
            var members = type.GetMembers(BindingFlags.Static | BindingFlags.Public | BindingFlags.SetField | BindingFlags.SetProperty | BindingFlags.InvokeMethod);
            MemberInfo member = null;
            foreach (var mInfo in members)
            {
                if (mInfo.MemberType == MemberTypes.Field || mInfo.MemberType == MemberTypes.Property)
                {
                    if (mInfo.Name.ToLower() == lowerName)
                    {
                        member = mInfo;
                        break;
                    }
                }
            }
            if (member == null)
            {
                string setName = "set" + lowerName;
                foreach (var mInfo in members)
                {
                    if (mInfo.MemberType == MemberTypes.Method)
                    {
                        if (mInfo.Name.ToLower() == lowerName || mInfo.Name.ToLower() == setName)
                        {
                            MethodInfo m = (MethodInfo)mInfo;
                            if (m.GetParameters().Length == args.Length)
                            {
                                member = mInfo;
                                break;
                            }
                        }
                    }
                }
            }
            return member;
        }

        static void SetMember(string typeAndMember, string[] values)
        {
            MemberInfo member = FindSetMember(typeAndMember, values);
            if (member == null)
            {
                Debug.LogError("Not Find Member: " + typeAndMember);
            }
            try
            {
                if (member is PropertyInfo)
                {
                    PropertyInfo pInfo = (PropertyInfo)member;
                    pInfo.SetValue(null, ChangeType(values[0], pInfo.PropertyType), null);
                }
                else if (member is FieldInfo)
                {
                    FieldInfo fInfo = (FieldInfo)member;
                    fInfo.SetValue(null, ChangeType(values[0], fInfo.FieldType));
                }
                else if (member is MethodInfo)
                {
                    MethodInfo mInfo = (MethodInfo)member;
                    object[] args = mInfo.GetParameters()
                        .Select((o, i) => ChangeType(values[i], o.ParameterType))
                        .ToArray();
                    mInfo.Invoke(null, args);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Set Member Error: " + typeAndMember + " = " + string.Join(",", values));
                throw ex;
            }
        }

        static object ChangeType(string value, Type type)
        {
            if (type.IsEnum)
                return ParseEnum(type, value);
            return Convert.ChangeType(value, type);
        }

        static Type FindType(string typeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().SelectMany(o => o.GetTypes()).Where(o => o.FullName == typeName || (o.IsNested && o.FullName.Replace('+', '.') == typeName)).FirstOrDefault();
        }



        public static void UpdateConfig(string version, bool log)
        {
            StringBuilder sb = null;
            if (log)
                sb = new StringBuilder();
            configs = LoadConfig(version, sb);

            BuildTargetGroup buildGroup = EditorUserBuildSettings.selectedBuildTargetGroup;


            foreach (var item in configs)
            {
                if (CustomMembers.Contains(item.Key))
                    continue;
                SetMember(item.Key, item.Value);
            }

            if (buildGroup == BuildTargetGroup.Android || buildGroup == BuildTargetGroup.iOS)
            {
                if (Contains("Version"))
                    PlayerSettings.bundleVersion = Get("Version");
            }

            if (Contains("loggingError"))
                PlayerSettings.SetStackTraceLogType(LogType.Error, Get("loggingError", StackTraceLogType.ScriptOnly));
            if (Contains("loggingAssert"))
                PlayerSettings.SetStackTraceLogType(LogType.Assert, Get("loggingAssert", StackTraceLogType.ScriptOnly));
            if (Contains("loggingWarning"))
                PlayerSettings.SetStackTraceLogType(LogType.Warning, Get("loggingWarning", StackTraceLogType.ScriptOnly));
            if (Contains("loggingLog"))
                PlayerSettings.SetStackTraceLogType(LogType.Log, Get("loggingLog", StackTraceLogType.ScriptOnly));
            if (Contains("loggingException"))
                PlayerSettings.SetStackTraceLogType(LogType.Exception, Get("loggingException", StackTraceLogType.ScriptOnly));

            switch (buildGroup)
            {
                case BuildTargetGroup.Android:
                    if (Contains("VersionCode"))
                        PlayerSettings.Android.bundleVersionCode = Get("VersionCode", 1);
                    break;
                case BuildTargetGroup.iOS:
                    if (Contains("VersionCode"))
                        PlayerSettings.iOS.buildNumber = Get("VersionCode");
                    break;
            }
            if (sb != null)
                Debug.Log("Update Config\n" + sb.ToString());


            string outputDir = Get("Output.Dir");
            string fileName = Get("Output.FileName", string.Empty);
            string outputPath = Path.Combine(outputDir, fileName);
            BuildOptions options;
            string[] scenes = null;


            options = Get<BuildOptions>("Build.BuildOptions", BuildOptions.None);

            if (Contains("Build.Scenes"))
            {
                string tmp = Get("Build.Scenes");
                tmp = tmp.Trim();
                if (!string.IsNullOrEmpty(tmp))
                    scenes = tmp.Split(',');
            }

            if (scenes == null || scenes.Length == 0)
            {
                scenes = EditorBuildSettings.scenes.Select(o => o.path).ToArray();
            }


            Scenes = scenes;
            OutputPath = outputPath;
            Options = options;
            ShowFolder = Get<bool>("Build.ShowFolder", false);

            if (Get("ClearLog", false))
            {
                ClearLog();
            }

            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh();

        }


        static void BuildAssetBundles()
        {
            BuildTargetGroup buildGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
            string outputDir = Get("Output.Dir");
            string[] tmp = Get<string[]>("Build.Assets");

            AssetBundleBuild[] assetBundleBuilds = new AssetBundleBuild[tmp.Length];
            for (int i = 0; i < assetBundleBuilds.Length; i++)
            {
                string assetPath = tmp[i];
                AssetBundleBuild assetBundleBuild = new AssetBundleBuild();
                assetBundleBuild.assetNames = new string[] { assetPath };
                assetBundleBuild.assetBundleName = Path.GetFileNameWithoutExtension(assetPath);

                assetBundleBuilds[i] = assetBundleBuild;
            }


            BuildAssetBundleOptions assetBundleOptions = Get<BuildAssetBundleOptions>("Build.BuildAssetBundleOptions", BuildAssetBundleOptions.None);
            BuildPipeline.BuildAssetBundles(outputDir, assetBundleBuilds, assetBundleOptions, buildTarget);

            Debug.Log("Build Assets Complete.");
        }


        public static void ClearLog()
        {
            if (Application.isEditor)
            {
                try
                {
                    var assembly = Assembly.GetAssembly(typeof(UnityEditor.Editor));
                    var type = assembly.GetType("UnityEditor.LogEntries");
                    var method = type.GetMethod("Clear");
                    method.Invoke(new object(), null);
                }
                catch { }
            }
            else
            {
                Debug.ClearDeveloperConsole();
            }
        }


        [PostProcessBuild(0)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            var sb = new StringBuilder();
            configs = LoadConfig(BuildVersion, sb);
            BuildTargetGroup buildGroup = EditorUserBuildSettings.selectedBuildTargetGroup;


            if (target == BuildTarget.iOS)
            {
                iOSPostProcessBuild(pathToBuiltProject);

                string projPath = pathToBuiltProject + "/Unity-iPhone.xcodeproj/project.pbxproj";

                PBXProject pbxProj = new PBXProject();

                pbxProj.ReadFromFile(projPath);
                string targetGuid = pbxProj.TargetGuidByName("Unity-iPhone");
                pbxProj.SetBuildProperty(targetGuid, "ENABLE_BITCODE", "NO");


                //pbxProj.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-all_load");
                //pbxProj.AddFrameworkToProject(targetGuid, "WebKit.framework", true);
                //pbxProj.AddFrameworkToProject(targetGuid, "StoreKit.framework", false);
                //pbxProj.AddCapability(targetGuid, PBXCapabilityType.InAppPurchase);
                if (Get("iOS.GameCenter", false))
                {
                    //Game Center
                    pbxProj.AddFrameworkToProject(targetGuid, "GameKit.framework", false);
                    pbxProj.AddCapability(targetGuid, PBXCapabilityType.GameCenter);
                }


#if BUGLY_SDK
            //Bugly

            pbxProj.AddFrameworkToProject(targetGuid, "Security.framework", false);
            pbxProj.AddFrameworkToProject(targetGuid, "SystemConfiguration.framework", false);
            pbxProj.AddFrameworkToProject(targetGuid, "JavaScriptCore.framework", true);
            pbxProj.AddFileToBuild(targetGuid, pbxProj.AddFile("usr/lib/libz.tbd", "Frameworks/libz.tbd", PBXSourceTree.Sdk));
            pbxProj.AddFileToBuild(targetGuid, pbxProj.AddFile("usr/lib/libc++.tbd", "Frameworks/libc++.tbd", PBXSourceTree.Sdk));

            pbxProj.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-ObjC");
            pbxProj.SetBuildProperty(targetGuid, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");
            pbxProj.SetBuildProperty(targetGuid, "GENERATE_DEBUG_SYMBOLS", "yes");

            //---Bugly
#endif
                pbxProj.WriteToFile(projPath);


                string plistPath = pathToBuiltProject + "/Info.plist";
                PlistDocument plist = new PlistDocument();
                plist.ReadFromString(File.ReadAllText(plistPath));
                PlistElementDict rootDict = plist.root;

                if (Get("iOS.GameCenter", false))
                {
                    //Game Center
                    var caps = rootDict.CreateArray("UIRequiredDeviceCapabilities");
                    caps.AddString("armv7");
                    caps.AddString("gamekit");
                }

                plist.WriteToFile(plistPath);
            }
            else if (target == BuildTarget.Android)
            {
                AndroidPostProcessBuild(pathToBuiltProject);
            }


            string outputPath = pathToBuiltProject;

            //File.WriteAllText(outputPath + ".txt", sb.ToString());
            Debug.Log("build path\n" + pathToBuiltProject);
        }


        static void AndroidPostProcessBuild(string pathToBuiltProject)
        {


        }



        static void iOSPostProcessBuild(string pathToBuiltProject)
        {

        }





        public static bool Contains(string name)
        {
            return configs.ContainsKey(name);
        }

        public static string Get(string name)
        {
            return Get<string>(name);
        }

        public static T Get<T>(string name)
        {
            string[] obj;
            if (!configs.TryGetValue(name, out obj))
                throw new Exception("Not Key:" + name);
            return Get<T>(name, default(T));
        }

        public static T Get<T>(string name, T defaultValue)
        {
            string[] v;
            try
            {
                if (!configs.TryGetValue(name, out v))
                    return defaultValue;

                if (v == null)
                    return default(T);
                Type type = typeof(T);
                if (type == typeof(string[]))
                    return (T)(object)v;

                if (type == typeof(string))
                {
                    if (v[0] is string)
                        return (T)(object)v[0];
                    return (T)(object)v.ToString();
                }
                if (type.IsEnum)
                {
                    return (T)ParseEnum(type, v[0] as string);
                }

                return (T)Convert.ChangeType(v[0], type);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Debug.LogError("config error name:" + name);
                return defaultValue;
            }

        }
        static Regex tplRegex = new Regex("\\{\\$(.*?)(\\,(.*))?\\}");
        /// <summary>
        /// Template: {$Name}
        /// </summary>
        /// <param name="input"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static string ReplaceTemplate(string input, Func<string, string, string> func)
        {
            if (func == null)
                throw new ArgumentNullException("func");
            string ret = tplRegex.Replace(input, (m) =>
             {
                 string name = m.Groups[1].Value;
                 string format = m.Groups[3].Value;
                 return func(name, format);
             });
            return ret;
        }
        /// <summary>
        /// Template: {$Key,FormatString}
        /// </summary>
        /// <param name="input"></param>
        public static void ReplaceTemplate(Dictionary<string, string[]> input)
        {
            string[] values;
            foreach (var key in input.Keys.ToArray())
            {
                values = input[key];
                if (values == null)
                    continue;
                for (int i = 0; i < values.Length; i++)
                {
                    string value = values[i];

                    if (tplRegex.IsMatch(value))
                    {
                        value = FindReplaceString(input, key, value, key);
                        values[i] = value;
                    }
                }
            }
        }

        static string FindReplaceString(Dictionary<string, string[]> input, string key, string value, string startKey)
        {
            value = tplRegex.Replace(value, (m) =>
            {
                string name = m.Groups[1].Value;
                string format = m.Groups[3].Value;


                //value = ReplaceTemplate(value, (name, format) =>
                //{
                if (input.Comparer.Equals(key, name))
                    throw new Exception("reference self. key: [" + name + "], key1:[" + key + "]" + "], key2:[" + startKey + "]");
                if (input.Comparer.Equals(startKey, name))
                    throw new Exception("loop reference key1:[" + name + "], key2:[" + startKey + "]");
                string newValue;
                if (input.ContainsKey(name))
                {
                    newValue = FindReplaceString(input, name, input[name][0], startKey);
                    if (!string.IsNullOrEmpty(format))
                    {
                        newValue = string.Format(format, newValue);
                    }
                }
                else if (GlobalVariables.ContainsKey(name))
                {
                    object v = GlobalVariables[name];
                    if (v != null && !string.IsNullOrEmpty(format) && v is IFormattable)
                    {
                        newValue = ((IFormattable)v).ToString(format, null);
                    }
                    else
                    {
                        newValue = v.ToString();
                    }

                }
                else
                {
                    throw new Exception("not found key: [" + name + "]");
                }
                return newValue;
            });
            return value;
        }

        enum Architecture
        {
            None = 0,
            ARM64 = 1,
            /// <summary>
            /// Arm7和Arm64
            /// </summary>
            Universal = 2,
        }

        [Serializable]
        private class Serialization<TKey, TValue> : ISerializationCallbackReceiver
        {
            [SerializeField]
            List<TKey> keys;
            [SerializeField]
            List<TValue> values;

            Dictionary<TKey, TValue> target;
            public Dictionary<TKey, TValue> ToDictionary() { return target; }

            public Serialization(Dictionary<TKey, TValue> target)
            {
                this.target = target;
            }

            public void OnBeforeSerialize()
            {
                keys = new List<TKey>(target.Keys);
                values = new List<TValue>(target.Values);
            }

            public void OnAfterDeserialize()
            {
                var count = Math.Min(keys.Count, values.Count);
                target = new Dictionary<TKey, TValue>(count);
                for (var i = 0; i < count; ++i)
                {
                    target.Add(keys[i], values[i]);
                }
            }
        }


        static void BuildPlayer()
        {
            var task = new EditorTask();
            var attrType = typeof(PreProcessBuildAttribute);
            Regex preProcessBuildRegex = new Regex("^PreProcessBuild(?:(_?)([0-9]+))?.*");
            foreach (var mInfo in AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(o => o.GetTypes())
                .SelectMany(o => o.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Where(o => o.IsDefined(attrType, false) || preProcessBuildRegex.IsMatch(o.Name))
                .OrderBy(o =>
                {
                    int order = 0;
                    if (o.IsDefined(attrType, false))
                    {
                        order = (o.GetCustomAttributes(typeof(PreProcessBuildAttribute), false).First() as PreProcessBuildAttribute).CallbackOrder;
                    }
                    else
                    {
                        var m = preProcessBuildRegex.Match(o.Name);
                        if (m.Success && !string.IsNullOrEmpty(m.Groups[2].Value))
                        {
                            int n;
                            if (int.TryParse(m.Groups[2].Value.Replace('_', '-'), out n))
                            {
                                if (m.Groups[1].Value == "_")
                                    n = -n;
                                order = n;
                            }
                        }
                    }
                    Debug.Log(o.Name);
                    return order;
                }
                ))
            {
                if (!mInfo.IsStatic)
                    throw new Exception(string.Format("{0}  method:{1}  only static", attrType.Name, mInfo.Name));

                var ps = mInfo.GetParameters();
                if (ps.Length != 0)
                    throw new Exception(string.Format("{0}  method:{1}  only empty parameter", attrType.Name, mInfo.Name));

                task.Add((Action)Delegate.CreateDelegate(typeof(Action), mInfo));
            }

            task.Run();
        }


        #region PreProcessBuild

        [PreProcessBuild(-1000)]
        static void PreProcessBuild_Config()
        {
            UpdateConfig(BuildVersion, false);
        }

        [PreProcessBuild(-999)]
        static void PreProcessBuild_Clear()
        {
            if (configs == null)
            {
                configs = LoadConfig(BuildVersion);
            }
            BuildTargetGroup buildGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
            string outputDir = Get("Output.Dir");
            string fileName = Get("Output.FileName", string.Empty);
            string outputPath = Path.Combine(outputDir, fileName);

            //if (!Directory.Exists(outputDir))
            //    Directory.CreateDirectory(outputDir);
            //else
            //{

            //foreach (var dir in Directory.GetDirectories(outputDir, "*", SearchOption.TopDirectoryOnly))
            //{
            //    Directory.Delete(dir, false);
            //}

            //}
            //foreach (var file in Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories))
            //{
            //    File.SetAttributes(file, FileAttributes.Normal);
            //    File.Delete(file);
            //}
        }
        static void DeleteDirectoryFiles(string path)
        {
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
        }
        [PreProcessBuild(1)]
        static void PreProcessBuild_BuildPlayer()
        {
            if (configs == null)
            {
                configs = LoadConfig(BuildVersion);
            }

            if (Contains("Build.Assets"))
            {
                BuildAssetBundles();
                return;
            }

            string outputPath = OutputPath;
            string[] scenes = Scenes;
            BuildOptions options = Options;

            if (File.Exists(outputPath))
            {
                DeleteDirectoryFiles(Path.GetDirectoryName(outputPath));
            }
            else if (Directory.Exists(outputPath))
            {
                DeleteDirectoryFiles(outputPath);
            }


            if (scenes == null || scenes.Length == 0)
                throw new Exception("build player scenes empty");


            var report = BuildPipeline.BuildPlayer(scenes, outputPath, EditorUserBuildSettings.activeBuildTarget, options);

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception("" + report.summary.result);
            if (ShowFolder)
                EditorUtility.RevealInFinder(outputPath);
        }

        #endregion

    }

    /// <summary>
    /// use [PreProcessBuild] or method name: [PreProcessBuild]=PreProcessBuild(); [PreProcessBuild(1)]=PreProcessBuild1(); [PreProcessBuild(-1)]=PreProcessBuild_1();
    /// </summary>
    public class PreProcessBuildAttribute : CallbackOrderAttribute
    {
        public PreProcessBuildAttribute()
        {
        }

        public PreProcessBuildAttribute(int callbackOrder)
        {
            base.m_CallbackOrder = callbackOrder;
        }

        public int CallbackOrder
        {
            get { return m_CallbackOrder; }
        }

    }


}