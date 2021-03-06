﻿using MyStaging.App.Models;
using MyStaging.Common;
using MyStaging.Helpers;
using System;
using System.Collections.Generic;
using System.IO;

namespace MyStaging.App.DAL
{
    public class EnumsDal
    {
        private string projectName = string.Empty;
        private string modelPath = string.Empty;
        private string rootPath = string.Empty;
        private List<PluginsViewModel> plugins;

        public EnumsDal(string rootpath, string modelpath, string projName, List<PluginsViewModel> plugins)
        {
            this.rootPath = rootpath;
            this.modelPath = modelpath;
            this.projectName = projName;
            this.plugins = plugins;
        }

        public void Generate()
        {
            string _sqltext = @"
select a.oid,a.typname,b.nspname from pg_type a 
INNER JOIN pg_namespace b on a.typnamespace = b.oid 
where a.typtype = 'e' order by oid asc";

            List<EnumTypeInfo> list = new List<EnumTypeInfo>();
            PgSqlHelper.ExecuteDataReader(dr =>
            {
                list.Add(new EnumTypeInfo()
                {
                    Oid = Convert.ToInt32(dr["oid"]),
                    TypeName = dr["typname"].ToString(),
                    NspName = dr["nspname"].ToString()
                });
            }, System.Data.CommandType.Text, _sqltext);

            string _fileName = Path.Combine(modelPath, "_Enums.cs");
            using (StreamWriter writer = new StreamWriter(File.Create(_fileName), System.Text.Encoding.UTF8))
            {
                writer.WriteLine("using System;");
                writer.WriteLine();
                writer.WriteLine($"namespace {projectName}.Model");
                writer.WriteLine("{");

                for (int i = 0; i < list.Count; i++)
                {
                    var item = list[i];
                    writer.WriteLine($"\tpublic enum {item.TypeName.ToUpperPascal()}");
                    writer.WriteLine("\t{");
                    string sql = $"select oid,enumlabel from pg_enum WHERE enumtypid = {item.Oid} ORDER BY oid asc";
                    PgSqlHelper.ExecuteDataReader(dr =>
                    {
                        string c = i < list.Count ? "," : "";
                        writer.WriteLine($"\t\t{dr["enumlabel"]}{c}");
                    }, System.Data.CommandType.Text, sql);
                    writer.WriteLine("\t}");
                }
                writer.WriteLine("}");
            }

            GenerateMapping(list);
        }

        public void GenerateMapping(List<EnumTypeInfo> list)
        {
            string _fileName = Path.Combine(rootPath, "_startup.cs");
            using (StreamWriter writer = new StreamWriter(File.Create(_fileName), System.Text.Encoding.UTF8))
            {
                writer.WriteLine($"using {projectName}.Model;");
                writer.WriteLine("using System;");
                writer.WriteLine("using Npgsql;");
                writer.WriteLine("using Microsoft.Extensions.Logging;");
                writer.WriteLine("using MyStaging.Helpers;");
                writer.WriteLine("using MyStaging.Common;");
                writer.WriteLine("using Newtonsoft.Json.Linq;");
                writer.WriteLine("using Microsoft.Extensions.Caching.Distributed;");
                writer.WriteLine();
                writer.WriteLine($"namespace {projectName}");
                writer.WriteLine("{");
                writer.WriteLine("\tpublic class _startup");
                writer.WriteLine("\t{");
                writer.WriteLine("\t\tpublic static void Init(StagingOptions options)");
                writer.WriteLine("\t\t{");
                writer.WriteLine("\t\t\tPgSqlHelper.InitConnection(options);");
                writer.WriteLine();
                writer.WriteLine("\t\t\tType[] jsonTypes = { typeof(JToken), typeof(JObject), typeof(JArray) };");
                writer.WriteLine("\t\t\tNpgsqlNameTranslator translator = new NpgsqlNameTranslator();");
                writer.WriteLine("\t\t\tNpgsqlConnection.GlobalTypeMapper.UseJsonNet(jsonTypes);");
                foreach (var item in plugins)
                {
                    writer.WriteLine($"\t\t\t{item.Mapper}");
                }

                if (list.Count > 0)
                {
                    writer.WriteLine();
                    foreach (var item in list)
                    {
                        writer.WriteLine($"\t\t\tNpgsqlConnection.GlobalTypeMapper.MapEnum<{item.TypeName.ToUpperPascal()}>(\"{item.NspName}.{item.TypeName}\", translator);");
                    }
                }

                writer.WriteLine("\t\t}");
                writer.WriteLine("\t}");
                writer.WriteLine("\tpublic partial class NpgsqlNameTranslator : INpgsqlNameTranslator");
                writer.WriteLine("\t{");
                writer.WriteLine("\t\tpublic string TranslateMemberName(string clrName) => clrName;");
                writer.WriteLine("\t\tpublic string TranslateTypeName(string clrTypeName) => clrTypeName;");
                writer.WriteLine("\t}");
                writer.WriteLine("}"); // namespace end
            }
        }
    }
}
