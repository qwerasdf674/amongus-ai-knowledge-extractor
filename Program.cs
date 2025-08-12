using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using ShellProgressBar;
using System.Text.RegularExpressions;

namespace AssemblyKnowledgeExtractor
{
    class Program
    {
        // =========================================================================================
        // === CONFIGURAÇÕES GERAIS E FILTROS ===
        // =========================================================================================
        private const int MAXIMO_TOKENS_APROXIMADO = 1_000_000;

        private static readonly HashSet<string> CLASSES_PARA_DETALHE_COMPLETO = new HashSet<string>
        {
            "PlayerControl",
            "GameManager",
            "AmongUsClient",
            "InnerNet.InnerNetClient" // Exemplo de classe com namespace
        };

        private static readonly bool INCLUDE_PRIVATE_MEMBERS_PADRAO = false; // Usado para classes que NÃO estão na lista acima

        private static readonly List<string> NAMESPACES_PARA_INCLUIR = new List<string>
        {
            "Assembly-CSharp", "InnerNet"
        };
        private static readonly List<string> NAMESPACES_PARA_IGNORAR = new List<string>
        {
            "System", "Unity", "UnityEngine", "UnityEditor", "Microsoft", "Mono", "TMPro",
            "Cinemachine", "Photon", "PlayFab", "Firebase", "Google", "Newtonsoft", "I2",
            "Plugins", "OdinInspector", "Rewired"
        };
        // =========================================================================================

        private static readonly string Separator = new string('=', 70);

        static void Main(string[] args)
        {
            Console.Title = "Knowledge Base Generator for AI v5.0 (On-Demand Detail)";
            PrintHeader();

            if (args.Length == 0)
            {
                Console.WriteLine("  Usage: Drag and drop the 'Assembly-CSharp.dll' onto this executable.");
                Console.WriteLine("\n  The program will generate full code for specific classes and an");
                Console.WriteLine("  optimized summary for the others, using multiple CPU cores.");
                Console.WriteLine("\n\n  Press any key to exit...");
                Console.ReadKey();
                return;
            }

            foreach (var dllPath in args) { ProcessAssembly(dllPath); }

            Console.WriteLine(Separator);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n Knowledge extraction for all files completed!");
            Console.ResetColor();
            Console.WriteLine(" Press any key to close this window...");
            Console.ReadKey();
        }

        private static void ProcessAssembly(string dllPath)
        {
            Console.WriteLine(Separator);
            Console.WriteLine($"Starting extraction of: {Path.GetFileName(dllPath)}");
            Console.WriteLine(Separator);

            if (!File.Exists(dllPath) || Path.GetExtension(dllPath)?.ToLower() != ".dll")
            {
                SetColorAndWrite(ConsoleColor.Red, $"ERROR: Invalid or missing file. Skipping: '{dllPath}'\n");
                return;
            }

            try
            {
                ModuleDefMD module = ModuleDefMD.Load(dllPath);
                WriteStep(1, "Module successfully loaded.");

                WriteStep(2, "Generating index of types and members...");
                var (nsCount, typeCount, memberCount) = GenerateIndexTxt(module, dllPath);

                WriteStep(3, "Generating knowledge base (this may take a few minutes)...");
                GenerateKnowledgeBaseTxt(module, dllPath);

                SetColorAndWrite(ConsoleColor.DarkGray, $"Index summary: {nsCount} namespaces, {typeCount} types, {memberCount} members.\n");
                SetColorAndWrite(ConsoleColor.Green, "Process finished successfully!\n");
            }
            catch (Exception ex)
            {
                SetColorAndWrite(ConsoleColor.Red, $"\nCRITICAL ERROR processing '{Path.GetFileName(dllPath)}': {ex.Message}\n{ex.StackTrace}\n");
            }
        }

        private static void GenerateKnowledgeBaseTxt(ModuleDefMD module, string inputDllPath)
        {
            string outputDir = Path.GetDirectoryName(inputDllPath) ?? string.Empty;
            string baseName = Path.GetFileNameWithoutExtension(inputDllPath);
            string outputTxtPath = Path.Combine(outputDir, $"{baseName}-knowledge-base-completo.txt");
            var sb = new StringBuilder();

            var decompilerSettings = new DecompilerSettings(LanguageVersion.CSharp7_3) { ThrowOnAssemblyResolveErrors = false };
            var decompiler = new CSharpDecompiler(inputDllPath, decompilerSettings);

            sb.AppendLine($"// === Hybrid Knowledge Base for Assembly: {module.Name} ===");
            sb.AppendLine($"// Generated at: {DateTime.Now} with On-Demand Detail.");
            sb.AppendLine(Separator);

            var allTypes = module.GetTypes().Where(t => !t.Name.String.Contains("<")).ToList();
            var typesByNamespace = allTypes.OrderBy(t => t.FullName)
                                           .GroupBy(t => t.Namespace.String)
                                           .OrderBy(g => g.Key);

            // Preparação do progresso (somente namespaces incluídos)
            var typesForProgress = allTypes.Where(t => ShouldProcessNamespace(t.Namespace.String)).ToList();
            var progressOptions = new ProgressBarOptions
            {
                ProgressCharacter = '─',
                ForegroundColor = ConsoleColor.Yellow,
                BackgroundColor = ConsoleColor.DarkGray,
                CollapseWhenFinished = true
            };

            bool tokenLimitReached = false;
            int namespacesProcessados = typesForProgress.Select(t => string.IsNullOrEmpty(t.Namespace.String) ? "Global" : t.Namespace.String).Distinct().Count();
            int tiposEmitidos = 0;

            using (var pbar = new ProgressBar(typesForProgress.Count, "Processing types...", progressOptions))
            {
                foreach (var type in allTypes.OrderBy(t => t.FullName))
                {
                    if (!ShouldProcessNamespace(type.Namespace.String)) continue;
                    if ((sb.Length / 4) > MAXIMO_TOKENS_APROXIMADO) { tokenLimitReached = true; break; }

                    pbar.Tick($"{(string.IsNullOrEmpty(type.Namespace.String) ? "Global" : type.Namespace.String)}.{type.Name}");

                    try
                    {
                        var typeHandle = (TypeDefinitionHandle)MetadataTokens.EntityHandle((int)type.MDToken.Raw);
                        string decompiledClass = decompiler.DecompileAsString(typeHandle);
                        // Limpeza de comentários de IL indesejados do decompilador (e.g., "//IL_009a: Unknown result type ...")
                        decompiledClass = CleanDecompiledCode(decompiledClass);
                        sb.AppendLine(decompiledClass);
                        sb.AppendLine();
                        tiposEmitidos++;
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"// Failed to decompile type: {type.FullName} -> {ex.Message}");
                    }
                }
            }

            File.WriteAllText(outputTxtPath, sb.ToString());
            SetColorAndWrite(ConsoleColor.Green, $"✔ Success: '{Path.GetFileName(outputTxtPath)}' generated!");
            SetColorAndWrite(ConsoleColor.DarkGray, $"Summary: {namespacesProcessados} namespaces included, {typesForProgress.Count} types analyzed, {tiposEmitidos} types emitted.");
            if (tokenLimitReached)
            {
                SetColorAndWrite(ConsoleColor.Yellow, "WARNING: Token limit reached. Output file may be incomplete.");
            }
        }

        private static string CleanDecompiledCode(string decompiledCode)
        {
            var lines = decompiledCode.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            // Remove qualquer linha de comentário que contenha marcação de IL do decompilador, com ou sem indentação
            // Exemplos:
            //   "        //IL_0035: Unknown result type ..."
            //   "// IL_00A0: ..."
            //   "// il_00a0: ..."
            var cleanedLines = lines.Where(line =>
            {
                var trimmed = line.TrimStart();
                if (!trimmed.StartsWith("//")) return true;
                return trimmed.IndexOf("IL_", StringComparison.OrdinalIgnoreCase) < 0;
            }).ToArray();
            return string.Join("\n", cleanedLines);
        }

        #region Helpers and Visuals
        private static (int namespaces, int types, int members) GenerateIndexTxt(ModuleDefMD module, string inputDllPath)
        {
            string outputDir = Path.GetDirectoryName(inputDllPath) ?? string.Empty;
            string baseName = Path.GetFileNameWithoutExtension(inputDllPath);
            string outputIndexPath = Path.Combine(outputDir, $"{baseName}-knowledge-base-index.txt");

            var sb = new StringBuilder();
            sb.AppendLine($"// === Index of Types and Members: {module.Name} ===");
            sb.AppendLine($"// Generated at: {DateTime.Now}");
            sb.AppendLine(Separator);

            var allTypes = module.GetTypes().Where(t => !t.Name.String.Contains("<")).ToList();
            var grouped = allTypes
                .Where(t => ShouldProcessNamespace(t.Namespace.String))
                .OrderBy(t => t.FullName)
                .GroupBy(t => t.Namespace.String)
                .OrderBy(g => g.Key);

            sb.AppendLine("// ");
            sb.AppendLine("// Types:");
            sb.AppendLine("// ");
            foreach (var t in allTypes.Where(t => ShouldProcessNamespace(t.Namespace.String)).OrderBy(t => t.Name))
            {
                sb.AppendLine($"// {t.Name}");
            }
            sb.AppendLine(Separator);

            int nsCount = 0;
            int typeCount = 0;
            int memberCount = 0;

            foreach (var ns in grouped)
            {
                nsCount++;
                string nsName = string.IsNullOrEmpty(ns.Key) ? "Global" : ns.Key;
                sb.AppendLine($"namespace {nsName}");
                sb.AppendLine("{");

                foreach (var type in ns)
                {
                    typeCount++;
                    sb.AppendLine($"    {GetTypeKeyword(type)} {type.Name}");

                    var fields = type.Fields.Where(f => INCLUDE_PRIVATE_MEMBERS_PADRAO || f.IsPublic || f.IsFamily).ToList();
                    if (fields.Any())
                    {
                        sb.AppendLine("        // Fields");
                        foreach (var f in fields.OrderBy(f => f.Name))
                        {
                            var fieldType = GetFriendlyTypeName(f.FieldSig?.Type);
                            var access = GetAccessibility(f);
                            var mods = f.IsStatic ? " static" : string.Empty;
                            sb.AppendLine($"        - {access}{mods} {fieldType} {f.Name}");
                            memberCount++;
                        }
                    }

                    var props = type.Properties.Where(p =>
                        INCLUDE_PRIVATE_MEMBERS_PADRAO ||
                        (p.GetMethod?.IsPublic == true) || (p.SetMethod?.IsPublic == true) ||
                        (p.GetMethod?.IsFamily == true) || (p.SetMethod?.IsFamily == true)).ToList();
                    if (props.Any())
                    {
                        sb.AppendLine("        // Properties");
                        foreach (var p in props.OrderBy(p => p.Name))
                        {
                            var propType = GetFriendlyTypeName(p.PropertySig?.RetType);
                            var hasGet = p.GetMethod != null;
                            var hasSet = p.SetMethod != null;
                            var accessors = $"{{{(hasGet ? " get;" : string.Empty)}{(hasSet ? " set;" : string.Empty)} }}".Replace("  ", " ").Trim();
                            var accessorMethod = p.GetMethod ?? p.SetMethod;
                            var access = accessorMethod != null ? GetAccessibility(accessorMethod) : "internal";
                            var mods = (p.GetMethod?.IsStatic == true || p.SetMethod?.IsStatic == true) ? " static" : string.Empty;
                            sb.AppendLine($"        - {access}{mods} {propType} {p.Name} {accessors}");
                            memberCount++;
                        }
                    }

                    var eventsList = type.Events.Where(e =>
                    {
                        var add = e.AddMethod;
                        var rem = e.RemoveMethod;
                        return INCLUDE_PRIVATE_MEMBERS_PADRAO ||
                               (add?.IsPublic == true) || (rem?.IsPublic == true) ||
                               (add?.IsFamily == true) || (rem?.IsFamily == true);
                    }).ToList();
                    if (eventsList.Any())
                    {
                        sb.AppendLine("        // Events");
                        foreach (var ev in eventsList.OrderBy(e => e.Name))
                        {
                            var evType = GetFriendlyTypeName(ev.EventType);
                            var evtMethod = ev.AddMethod ?? ev.RemoveMethod;
                            var access = evtMethod != null ? GetAccessibility(evtMethod) : "internal";
                            var mods = (ev.AddMethod?.IsStatic == true || ev.RemoveMethod?.IsStatic == true) ? " static" : string.Empty;
                            sb.AppendLine($"        - {access}{mods} {evType} {ev.Name}");
                            memberCount++;
                        }
                    }

                    var ctors = type.Methods.Where(m => m.IsConstructor && (INCLUDE_PRIVATE_MEMBERS_PADRAO || m.IsPublic || m.IsFamily)).ToList();
                    if (ctors.Any())
                    {
                        sb.AppendLine("        // Constructors");
                        foreach (var c in ctors.OrderBy(c => c.Name))
                        {
                            sb.AppendLine($"        - {FormatMethodSignature(c)}");
                            memberCount++;
                        }
                    }

                    var methods = type.Methods.Where(m => !m.IsConstructor && !m.IsSpecialName && (INCLUDE_PRIVATE_MEMBERS_PADRAO || m.IsPublic || m.IsFamily)).ToList();
                    if (methods.Any())
                    {
                        sb.AppendLine("        // Methods");
                        foreach (var m in methods.OrderBy(m => m.Name))
                        {
                            sb.AppendLine($"        - {FormatMethodSignature(m)}");
                            memberCount++;
                        }
                    }

                    sb.AppendLine();
                }

                sb.AppendLine("}");
            }

            File.WriteAllText(outputIndexPath, sb.ToString());
            SetColorAndWrite(ConsoleColor.Green, $"✔ Success: '{Path.GetFileName(outputIndexPath)}' (index) generated!");
            return (nsCount, typeCount, memberCount);
        }

        private static string GetFriendlyTypeName(ITypeDefOrRef? typeRef)
        {
            if (typeRef == null) return "void";
            return SimplifyTypeName(typeRef.FullName);
        }

        private static string GetFriendlyTypeName(TypeSig? typeSig)
        {
            if (typeSig == null) return "void";
            return SimplifyTypeName(typeSig.FullName);
        }

        private static string SimplifyTypeName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return fullName;
            // Remover namespaces comuns e mapear tipos primitivos para C#
            string name = fullName
                .Replace("System.", "")
                .Replace("Void", "void")
                .Replace("Boolean", "bool")
                .Replace("Byte", "byte")
                .Replace("SByte", "sbyte")
                .Replace("Char", "char")
                .Replace("Decimal", "decimal")
                .Replace("Double", "double")
                .Replace("Single", "float")
                .Replace("Int32", "int")
                .Replace("Int64", "long")
                .Replace("Int16", "short")
                .Replace("UInt32", "uint")
                .Replace("UInt64", "ulong")
                .Replace("UInt16", "ushort")
                .Replace("String", "string")
                .Replace("Object", "object");

            // Substituir separador de namespace remanescente
            // e simplificar nomes genéricos `1 -> <T>
            int tickIndex = name.IndexOf('`');
            if (tickIndex >= 0)
            {
                name = name.Substring(0, tickIndex);
            }
            // Simplificar nomes totalmente qualificados, mantendo o último segmento
            if (name.Contains('.'))
            {
                name = name.Split('.').Last();
            }
            return name;
        }

        private static string FormatMethodSignature(MethodDef method)
        {
            var access = GetAccessibility(method);
            var mods = new List<string>();
            if (method.IsStatic) mods.Add("static");
            if (method.IsAbstract) mods.Add("abstract");
            else if (method.IsVirtual)
            {
                if (method.IsFinal && method.IsVirtual) mods.Add("sealed override");
                else if (method.IsReuseSlot) mods.Add("virtual");
                else mods.Add("override");
            }

            var returnType = GetFriendlyTypeName(method.MethodSig?.RetType);
            var paramStrings = new List<string>();
            foreach (var p in method.Parameters)
            {
                if (p.IsHiddenThisParameter) continue; // ignora 'this'
                var pType = GetFriendlyTypeName(p.Type);
                string prefix = string.Empty;
                if (p.Type is ByRefSig) prefix = (p.ParamDef?.IsOut == true) ? "out " : "ref ";
                var pName = string.IsNullOrWhiteSpace(p.Name) ? null : p.Name;
                paramStrings.Add(pName == null ? ($"{prefix}{pType}") : $"{prefix}{pType} {pName}");
            }
            string parameters = string.Join(", ", paramStrings);
            string modsStr = mods.Count > 0 ? (" " + string.Join(" ", mods)) : string.Empty;
            return $"{access}{modsStr} {returnType} {method.Name}({parameters})";
        }

        private static string TryStripOuterNamespace(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;
            var lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            int firstNonEmpty = lines.FindIndex(l => !string.IsNullOrWhiteSpace(l));
            if (firstNonEmpty < 0) return code;
            string first = lines[firstNonEmpty].TrimStart();
            if (!first.StartsWith("namespace ")) return string.Empty;

            int braceBalance = 0;
            int startIndex = -1;
            for (int i = firstNonEmpty; i < lines.Count; i++)
            {
                string ln = lines[i];
                foreach (char ch in ln)
                {
                    if (ch == '{')
                    {
                        braceBalance++;
                        if (startIndex == -1)
                        {
                            startIndex = i + 1;
                        }
                    }
                    else if (ch == '}')
                    {
                        braceBalance--;
                        if (braceBalance == 0)
                        {
                            var innerLines = lines.Skip(startIndex).Take(i - startIndex).ToList();
                            return string.Join("\n", innerLines);
                        }
                    }
                }
            }
            return string.Empty;
        }

        

        private static bool ShouldProcessNamespace(string namespaceName)
        {
            string currentNs = string.IsNullOrEmpty(namespaceName) ? "Assembly-CSharp" : namespaceName;
            if (NAMESPACES_PARA_IGNORAR.Any(ignored => currentNs.StartsWith(ignored, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
            if (NAMESPACES_PARA_INCLUIR.Any() && !NAMESPACES_PARA_INCLUIR.Any(included => currentNs.StartsWith(included, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
            return true;
        }

        private static string GetTypeKeyword(TypeDef type)
        {
            if (type.IsInterface) return "interface";
            if (type.IsEnum) return "enum";
            if (type.IsValueType) return "struct";
            if (type.IsAbstract && type.IsSealed) return "static class";
            if (type.IsAbstract) return "abstract class";
            return "class";
        }

        private static string GetAccessibility(IMemberDef member)
        {
            if (member is TypeDef t)
            {
                if (t.IsPublic || t.IsNestedPublic) return "public";
                if (t.IsNestedFamily) return "protected";
                return "private";
            }
            if (member is MethodDef m)
            {
                if (m.IsPublic) return "public";
                if (m.IsFamily) return "protected";
                return "private";
            }
            return "internal";
        }

        private static string GetInheritanceString(TypeDef type)
        {
            var parts = new List<string>();
            if (type.BaseType != null && type.BaseType.FullName != "System.Object") { parts.Add(type.BaseType.Name.Replace("`1", "")); }
            foreach (var iface in type.Interfaces) { parts.Add(iface.Interface.Name.Replace("`1", "")); }
            return parts.Any() ? " : " + string.Join(", ", parts) : "";
        }

        private static void PrintHeader()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(@"
    ██████╗ ██████╗ ██████╗ ██╗  ██╗   ██╗ ██╗  ██╗███████╗███████╗██████╗ 
    ██╔══██╗██╔══██╗██╔══██╗╚██╗██╔╝   ██║ ██║  ██║██╔════╝██╔════╝██╔══██╗
    ██████╔╝██║  ██║██████╔╝ ╚███╔╝    ███████║  ██║█████╗  █████╗  ██████╔╝
    ██╔═══╝ ██║  ██║██╔══██╗ ██╔██╗    ██╔══██║  ██║██╔══╝  ██╔══╝  ██╔══██╗
    ██║     ██████╔╝██║  ██║██╔╝ ██╗██╗██║  ██║███████╗███████╗██║  ██║
    ╚═╝     ╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝╚═╝  ╚═╝╚══════╝╚══════╝╚═╝  ╚═╝
         --- Gerador de Base de Conhecimento para IA v5.0 (Final) ---
");
            Console.ResetColor();
        }

        private static void SetColorAndWrite(ConsoleColor color, string message, bool newLine = true)
        {
            Console.ForegroundColor = color;
            if (newLine) Console.WriteLine(message);
            else Console.Write(message);
            Console.ResetColor();
        }

        private static void WriteStep(int stepNumber, string message, ConsoleColor? color = null)
        {
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = color ?? ConsoleColor.Cyan;
            Console.WriteLine($"[{stepNumber}/3] {message}");
            Console.ForegroundColor = previousColor;
        }
        #endregion
    }
}