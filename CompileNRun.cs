using Microsoft.CSharp;
using Microsoft.VisualBasic;
using Mono.Cecil;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CSharp2PawnLib
{
    public class CompileNRun
    { //2015.06.20-21.
        private const string WorkingDir = "csharpscripts";
        private static PluginInterface.logprintf_t LogPrint;
        private static string Indent;
        public static void Load(PluginInterface.logprintf_t logprint, string indent)
        {
            LogPrint = logprint;
            Indent = indent;
            if (!Directory.Exists(WorkingDir))
                Directory.CreateDirectory(WorkingDir);

            ConvertIncludes();

            CompilerParameters parameters = new CompilerParameters();
            parameters.GenerateExecutable = false;
            parameters.GenerateInMemory = false;
            parameters.ReferencedAssemblies.AddRange(Assembly.GetExecutingAssembly().GetReferencedAssemblies().Select(entry => entry.Name).ToArray());
            parameters.WarningLevel = 4;

            string[] csfiles = Directory.GetFiles(WorkingDir, "*.cs");
            LogPrint(Indent + "Loading " + csfiles.Length + " CSharp files...");
            foreach (string file in csfiles)
            {
                PrepCompilerParameters(file, parameters);
                //if (csfiles.Length > 0)
                //{
                CSharpCodeProvider csprovider = new CSharpCodeProvider();
                //CompilerResults results = csprovider.CompileAssemblyFromFile(parameters, csfiles);
                CompilerResults results = csprovider.CompileAssemblyFromFile(parameters, file);
                Parse(results);
                //}
                //parameters.ReferencedAssemblies.Add(file); //Hozzáadja az aktuális fájlt a következőhöz, hogy... - Csak a legutolsó kapná meg az összeset
            }
            LogPrint(Indent + "Loaded " + csfiles.Length + " CSharp files.");

            string[] vbfiles = Directory.GetFiles(WorkingDir, "*.vb");
            LogPrint(Indent + "Loading " + vbfiles.Length + " VisualBasic files...");
            //if (vbfiles.Length > 0)
            foreach (string file in vbfiles)
            {
                PrepCompilerParameters(file, parameters);
                VBCodeProvider vbprovider = new VBCodeProvider();
                CompilerResults results = vbprovider.CompileAssemblyFromFile(parameters, vbfiles);
                Parse(results);
            }
            LogPrint(Indent + "Loaded " + vbfiles.Length + " VisualBasic files.");
        }

        private static IEnumerable<string> Includes;
        /// <summary>
        /// <para>Konvertálja az include-okat C# nyelvre, hogy hivatkozhassunk rá a szkriptekből</para>
        /// <para>Az include-okban található #include utasításokat nem veszi figyelembe, a mappában található összes include-ot lefordítja</para>
        /// <para>Nem támogatja a funkciók létrehozását include-okban</para>
        /// </summary>
        private static void ConvertIncludes()
        {
            LogPrint(Indent + "Converting includes...");
            if (!Directory.Exists(WorkingDir + Path.DirectorySeparatorChar + "includes"))
                Directory.CreateDirectory(WorkingDir + Path.DirectorySeparatorChar + "includes");
            string[] files = Directory.GetFiles(WorkingDir + Path.DirectorySeparatorChar + "includes", "*.inc");
            Includes = files.Select(entry => Path.GetFileNameWithoutExtension(entry));
            List<List<string>> filelines = new List<List<string>>(); //2015.06.21.
            foreach (string file in files)
            { //2015.06.21.
                //string[] lines = File.ReadAllLines(file);
                List<string> lines = new List<string>(File.ReadAllLines(file));
                for (int i = 0; i < lines.Count; i++)
                { //2015.06.21.
                    while (lines[i].EndsWith(" "))
                        lines[i] = lines[i].Remove(lines[i].Length - 1);
                    while (lines[i].EndsWith("\\") || lines[i].EndsWith(","))
                    {
                        lines[i] += " " + lines[i + 1];
                        lines.RemoveAt(i + 1); //Eltávolítja a következő sort, mert már hozzáadta a mostanihoz
                    }
                }
                foreach (string line in lines)
                {
                    ProcessLine(line, true);
                }
                filelines.Add(lines);
            }
            int fileindex = 0;
            foreach (string file in files)
            {
                //string[] lines = File.ReadAllLines(file);
                List<string> lines = filelines[fileindex];
                CharPos commentstart = new CharPos { Column = -1, Line = -1 };
                CharPos commentend = new CharPos { Column = -1, Line = -1 };
                int lineindex = 0;
                bool resetcomment = false;
                string filename = Path.GetFileNameWithoutExtension(file);
                if (IsKeyword(filename))
                    continue; //2015.06.21. - Nem tölti be a float és egyéb típusokat
                string outfilename = WorkingDir + Path.DirectorySeparatorChar + "includes" + Path.DirectorySeparatorChar + filename + ".cs";
                File.WriteAllText(outfilename, "using System;\n\r");
                File.AppendAllText(outfilename, "namespace Includes\n\r");
                File.AppendAllText(outfilename, "{\n\r");
                File.AppendAllText(outfilename, "public abstract class " + filename + "\n\r");
                File.AppendAllText(outfilename, "{\n\r");
                foreach (string line in lines)
                {
                    //LogPrint("Line: " + line);
                    if(resetcomment) //Visszaállítja a következő sorhoz
                    {
                        commentstart.Column = -1;
                        commentstart.Line = -1;
                        resetcomment = false;
                    }
                    if (commentstart.IsEmpty())
                    {
                        //LogPrint("CommentStart is empty.");
                        commentstart.Column = line.IndexOf("/*");
                        commentstart.Line = lineindex;
                    }
                    //if (commentstart.Line == lineindex && !commentstart.IsEmpty()) //A fenti kód akkor is beállítja a sort, ha továbbra sem talál megjegyzést
                    if (!commentstart.IsEmpty()) //Akkor is keresse meg a komment végét, ha nem ebben a sorban kezdődött
                    {
                        //LogPrint("CommentStart==lineindex");
                        int index = line.Substring(commentstart.Column, line.Length - commentstart.Column).IndexOf("*/");
                        if (index != -1)
                        {
                            commentend.Column = index;
                            commentend.Line = lineindex;
                            resetcomment = true;
                        }
                    }
                    string linetext = null;
                    if (!commentstart.IsEmpty()) //Ha van benne komment, töröljük
                    {
                       //LogPrint("CommentStart is not empty");
                        if (commentstart.Line == lineindex)
                        {
                            if (commentstart.Column > 0)
                                linetext = line.Substring(0, commentstart.Column);
                        }
                        if (commentend.Line == lineindex)
                        {
                            if (commentend.Column < line.Length - 1)
                                linetext = line.Substring(commentend.Column);
                        }
                    }
                    else
                        linetext = line;
                    if (linetext != null)
                    {
                        //LogPrint("LineText!=null");
                        int ind = linetext.IndexOf("//");
                        if (ind != -1)
                            linetext = linetext.Substring(ind);
                        File.AppendAllText(outfilename, ProcessLine(linetext, false)); //2015.06.21.
                    }
                    lineindex++;
                }
                File.AppendAllText(outfilename, "}\n\r");
                File.AppendAllText(outfilename, "}\n\r");
                fileindex++; //2015.06.21.
            }
            LogPrint(Indent + "Converted " + files.Length + " files.");
        }

        private static Dictionary<string, string> Defines = new Dictionary<string, string>();
        private static string ProcessLine(string linetext, bool defines) //<-- 2015.06.21.
        {
            int ind = linetext.IndexOf("#");
            if (ind == -1 && !defines) //Nincs benne #define és hasonlók
            {
                int index = linetext.IndexOf("native");
                if (index != -1)
                {
                    string funcheader = linetext.Substring(index + "native".Length + 1);
                    //File.AppendAllText(outfilename, "public delegate " + type + " " + funcname + "(" + parameters + ");\n\r");
                    string type, funcname, parameters;
                    ProcessHeader(funcheader, out type, out funcname, out parameters);
                    return "public delegate " + type + " " + funcname + "(" + parameters + ");\n\r";
                }
                index = linetext.IndexOf("forward"); //2015.06.21.
                if (index != -1)
                { //2015.06.21.
                    string funcheader = linetext.Substring(index + "forward".Length + 1);
                    string type, funcname, parameters;
                    ProcessHeader(funcheader, out type, out funcname, out parameters);
                    return "public abstract " + type + " " + funcname + "(" + parameters + ");\n\r";
                }
                return "";
            }
            else if (defines && ind != -1) //Csak akkor foglalkozik vele, ha a paraméterben is az van megadva
            { //2015.06.21.
                int index = linetext.IndexOf(' ', ind + 1);
                if (index == -1)
                    index = linetext.IndexOf('\t', ind + 1);
                if (index == -1)
                    index = linetext.Length;
                //LogPrint("index: " + index);
                string dir = linetext.Substring(1, index - 1);
                switch (dir)
                {
                    case "define":
                        int index2 = linetext.IndexOf(' ', index + 1);
                        if (index2 == -1)
                            index2 = linetext.IndexOf('\t', index + 1);
                        //LogPrint("define: " + linetext);
                        //LogPrint("index: " + index + " index2: " + index2);
                        if (!(index2 == -1 || index2 < index + 1 || linetext.Substring(index2).All(entry => entry == ' ' || entry == '\t')))
                        {
                            //LogPrint("linetext.Substring1: " + linetext.Substring(index + 1, index2 - index) + " 2: " + linetext.Substring(index2));
                            string substr = linetext.Substring(index + 1, index2 - index);
                            //LogPrint("substr: " + substr);
                            substr = substr.Replace(" ", "").Replace("\t", "");
                            if (!Defines.ContainsKey(substr))
                                Defines.Add(substr, linetext.Substring(index2));
                        }
                        break;
                }
                return "";
            }
            return "";
        }

        private static void ProcessHeader(string funcheader, out string type, out string funcname, out string parameters)
        {
            funcname = funcheader.Substring(0, funcheader.IndexOf('('));
            //LogPrint("funcheader: " + funcheader);
            //LogPrint("funcname: " + funcname);
            //ind = funcheader.IndexOf(':');
            int ind = funcname.IndexOf(':'); //2015.06.21.
            //string type;
            if (ind != -1)
            {
                //type = funcheader.Substring(0, ind + 1);
                type = funcname.Substring(0, ind);
                //LogPrint("func type: " + type);
                switch (type)
                {
                    case "Float":
                        type = "float";
                        break;
                }
                funcname = funcname.Remove(0, type.Length + 1);
            }
            else
                type = "int"; //Az int visszatérési értékhez nem kell Pawn-ban megadni semmit
            //LogPrint("funcname: " + funcname);
            //string parameters = funcheader.Substring(funcheader.IndexOf('(') + 1, funcheader.Length - funcname.Length - 2);
            parameters = funcheader.Substring(funcheader.IndexOf('(') + 1, funcheader.IndexOf(')') - funcheader.IndexOf('(') - 1); //2015.06.21.
            parameters = ProcessParameters(parameters);
        }

        //private static bool printed = false; //TMP - 2015.06.21.
        private static string ProcessParameters(string parameters)
        {
            //LogPrint("parameters: " + parameters);
            //parameters = parameters.Replace(" ", ""); //2015.06.21.
            /*foreach (var item in Defines)
            {
                if (!printed)
                    LogPrint("Defines item: " + item.Key + " - " + item.Value); //2015.06.21.
                parameters = parameters.Replace(item.Key, item.Value); //2015.06.21.
            }
            printed = true; //TMP*/
            string[] parameterss = parameters.Split(',');
            parameterss = parameterss.Select(entry => (entry.StartsWith(" ")) ? entry.Remove(0, 1) : entry).ToArray(); //2015.06.21.
            bool isparams = false;
            for (int i = 0; i < parameterss.Length; i++)
            {
                if (parameterss[i].Any(entry => entry == '{' || entry == '}'))  //2015.06.21.
                {
                    if (!isparams) //Csak akkor írja ki, ha még nem tette
                    {
                        parameterss[i] = "params object[] args"; //2015.06.21.
                        isparams = true;
                    }
                    else
                        parameterss[i] = ""; //2015.06.21.
                    continue; //2015.06.21.
                }
                bool isout = false; //2015.06.21.
                isout = parameterss[i].Contains('&'); //2015.06.21.
                parameterss[i] = parameterss[i].Replace("&", ""); //2015.06.21.
                int index = parameterss[i].IndexOf(':');
                string paramtype;
                string paramname; //2015.06.21.
                if (index != -1)
                {
                    //LogPrint("index(:) != -1");
                    paramtype = parameterss[i].Substring(0, index);
                    //LogPrint("SubString");
                    //LogPrint("parameterss[i]:" + parameterss[i]);
                    //LogPrint("index + 1: " + (index + 1) + " parameterss[i].Length - index:" + (parameterss[i].Length - index));
                    paramname = parameterss[i].Substring(index + 1, parameterss[i].Length - index - 1); //2015.06.21.
                    //LogPrint("paramname");
                    switch (paramtype)
                    {
                        case "Float":
                            paramtype = "float";
                            break;
                    }
                    //funcheader = funcheader.Remove(0, paramtype.Length);
                }
                else
                {
                    paramtype = "int";
                    paramname = parameterss[i];
                }

                //index = parameterss[i].IndexOf("const ");
                index = paramname.IndexOf("const ");
                bool isconst = false;
                if (index != -1)
                {
                    isconst = true;
                    //parameterss[i] = parameterss[i].Remove(index, "const ".Length);
                    paramname = paramname.Remove(index, "const ".Length); //2015.06.21.
                }
                //index = parameterss[i].IndexOf("[]");
                index = paramname.IndexOf("[]");
                if (index != -1)
                {
                    if (isconst && paramtype == "int")
                        paramtype = "string";
                    else
                        paramtype += "[]";
                    paramname = paramname.Remove(paramname.Length - 2); //Eltávolítja a []-t
                }
                paramname = paramname.Replace(" ", ""); //2015.06.21.
                if (paramname.Length == 0)
                { //2015.06.21.
                    parameterss[i] = "";
                    continue;
                }
                if (IsKeyword(paramname))
                    paramname = paramname.Insert(0, "_"); //2015.06.21.
                parameterss[i] = (isout ? "ref " : "") + paramtype + " " + paramname; //2015.06.21.
            }
            //parameters = parameterss.Aggregate((entry1, entry2) => entry1 + ", " + entry2);
            parameterss = parameterss.Where(entry => entry != string.Empty).ToArray(); //2015.06.21.
            //parameterss = parameterss.Select(entry => entry.Replace(" ", "")).Where(entry => entry != string.Empty).ToArray(); //2015.06.21.
            if (parameterss.Length > 0)
                return parameterss.Aggregate((entry1, entry2) => entry1 + ", " + entry2);
            else
                return ""; //2015.06.21.
        }

        private static bool IsKeyword(string name)
        { //2015.06.21.
            switch (name) //2015.06.21.
            {
                case "abstract":
                case "as":
                case "base":
                case "bool":
                case "break":
                case "byte":
                case "case":
                case "catch":
                case "char":
                case "checked":
                case "class":
                case "const":
                case "continue":
                case "decimal":
                case "default":
                case "delegate":
                case "do":
                case "double":
                case "else":
                case "enum":
                case "event":
                case "explicit":
                case "extern":
                case "false":
                case "finally":
                case "fixed":
                case "float":
                case "for":
                case "foreach":
                case "goto":
                case "if":
                case "implicit":
                case "in":
                case "int":
                case "interface":
                case "internal":
                case "is":
                case "lock":
                case "long":
                case "namespace":
                case "new":
                case "null":
                case "object":
                case "operator":
                case "out":
                case "override":
                case "params":
                case "private":
                case "protected":
                case "public":
                case "readonly":
                case "ref":
                case "return":
                case "sbyte":
                case "sealed":
                case "short":
                case "sizeof":
                case "stackalloc":
                case "static":
                case "string":
                case "struct":
                case "switch":
                case "this":
                case "throw":
                case "true":
                case "try":
                case "typeof":
                case "uint":
                case "ulong":
                case "unchecked":
                case "unsafe":
                case "ushort":
                case "using":
                case "virtual":
                case "void":
                case "volatile":
                case "while":
                    return true;
            }
            return false;
        }

        private static void PrepCompilerParameters(string file, CompilerParameters parameters)
        {
            if (!Directory.Exists(WorkingDir + Path.DirectorySeparatorChar + "compiled"))
                Directory.CreateDirectory(WorkingDir + Path.DirectorySeparatorChar + "compiled");
            parameters.OutputAssembly = WorkingDir + Path.DirectorySeparatorChar + "compiled"
                + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(file) + ".dll";
        }

        private static void Parse(CompilerResults results)
        {
            if(results.Errors.HasErrors)
            {
                LogPrint(Indent + "Compiler error! ----------");
                foreach (CompilerError error in results.Errors)
                    LogPrint(Indent + error);
                LogPrint(Indent + "--------------------");
            }
            else
            {
                if(results.Errors.HasWarnings)
                {
                    LogPrint(Indent + "Warnings! ----------");
                    foreach (CompilerError error in results.Errors)
                        LogPrint(Indent + error);
                    LogPrint(Indent + "--------------------");
                }

                var assembly = AssemblyDefinition.ReadAssembly(results.PathToAssembly);
                foreach (TypeDefinition type in assembly.MainModule.GetTypes())
                {
                    //Type t = Type.GetType(type.FullName + ", " + type.Module.Assembly.FullName);
                    string filename = Path.GetFileNameWithoutExtension(results.PathToAssembly) + "_" + type.Name;
                    List<string> Lines = new List<string>();
                    //Lines.Add("#include <a_samp>");
                    //type.Fields.Where(entry => entry.IsStatic && entry.IsPrivate && entry.Name.StartsWith("Include"));
                    foreach (MethodDefinition method in type.Methods)
                    {
                        ParseMethod(method); //2015.06.21.
                    }
                }
            }
        }
        private static void ParseMethod(MethodDefinition method)
        { //2015.06.21.
            if (!method.HasBody)
                return; //2015.06.21.
        }
    }
}
