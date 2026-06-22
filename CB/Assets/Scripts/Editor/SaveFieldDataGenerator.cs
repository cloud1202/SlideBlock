#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ToolKit.Editor
{
    public static class SaveFieldDataGenerator
    {
        private const string MenuPath = "Tools/Save Field Data/Generate";
        private const string OutputPath = "Assets/Scripts/Share/SaveFieldData.cs";

        [MenuItem(MenuPath, priority = 10)]
        public static void Generate()
        {
            string[] fieldNames = Enum.GetNames(typeof(SaveFieldType));
            string output = BuildSource(fieldNames);

            File.WriteAllText(OutputPath, output, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(OutputPath);
            AssetDatabase.Refresh();

            Debug.Log($"[SaveFieldDataGenerator] {OutputPath} generated with {fieldNames.Length} fields.");
        }

        private static string BuildSource(string[] fieldNames)
        {
            string values = string.Join(",\n", fieldNames.Select(name => $"        \"{name}\""));

            return $@"public static class SaveFieldData
{{
    public static readonly string[] Fields =
    {{
{values}
    }};
}}
";
        }
    }
}
#endif
