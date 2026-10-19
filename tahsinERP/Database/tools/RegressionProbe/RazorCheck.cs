// Barcha Razor view'larni (Views/**/*.cshtml) kompilyatsiya qilib xatolarni ro'yxatlaydi — build ularni ko'rmaydi
// (masalan model turi double -> decimal o'zgarganda `as double?` yoki decimal*double arifmetikasi faqat ish vaqtida chiqadi).
//
//   RazorCheck.exe <webProjectDir> <outDir> <csc.exe>
//
// MVC Razor host (MvcWebPageRazorHost) bilan C# kod generatsiya qilinadi va bin/*.dll ga qarshi Roslyn csc bilan bitta
// assembly'ga kompilyatsiya qilinadi. Chiqish kodi: 0 — xato yo'q, 1 — xatolar bor. Run-RazorCheck.ps1 orqali ishlating.
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc.Razor;
using System.Web.Razor;
using System.Xml.Linq;
using Microsoft.CSharp;

class RazorCheck
{
    static int Main(string[] args)
    {
        string web = Path.GetFullPath(args[0]), outDir = Path.GetFullPath(args[1]), csc = args[2];
        Directory.CreateDirectory(outDir);
        foreach (var f in Directory.GetFiles(outDir, "*.cs")) File.Delete(f);

        // Har view uchun eng yaqin web.config (Views/ yoki Areas/X/Views/) dagi Razor namespace'lari
        var nsCache = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        Func<string, List<string>> namespacesFor = null;
        namespacesFor = dir =>
        {
            List<string> list;
            if (nsCache.TryGetValue(dir, out list)) return list;
            var cfg = Path.Combine(dir, "Web.config");
            if (File.Exists(cfg) && XDocument.Load(cfg).Descendants("system.web.webPages.razor").Any())
                list = XDocument.Load(cfg).Descendants("namespaces").Elements("add").Select(e => (string)e.Attribute("namespace")).ToList();
            else if (dir.Length > web.Length)
                list = namespacesFor(Path.GetDirectoryName(dir));
            else
                list = new List<string>();
            return nsCache[dir] = list;
        };

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // generatsiya fayli -> view
        int n = 0, genErrors = 0;
        var provider = new CSharpCodeProvider();
        foreach (var view in Directory.GetFiles(Path.Combine(web, "Views"), "*.cshtml", SearchOption.AllDirectories)
                     .Concat(Directory.Exists(Path.Combine(web, "Areas")) ? Directory.GetFiles(Path.Combine(web, "Areas"), "*.cshtml", SearchOption.AllDirectories) : new string[0]))
        {
            var virt = "~/" + view.Substring(web.Length).TrimStart('\\').Replace('\\', '/');
            var host = new MvcWebPageRazorHost(virt, view) { DefaultBaseClass = "System.Web.Mvc.WebViewPage" };
            foreach (var ns in namespacesFor(Path.GetDirectoryName(view))) host.NamespaceImports.Add(ns);
            var engine = new RazorTemplateEngine(host);
            var cls = "V" + (++n);
            GeneratorResults res;
            using (var r = new StreamReader(view)) res = engine.GenerateCode(r, cls, "RazorCheckViews", view);
            if (!res.Success)
            {
                foreach (var e in res.ParserErrors) { Console.WriteLine("{0}({1}): RAZOR {2}", virt, e.Location.LineIndex + 1, e.Message); genErrors++; }
                continue;
            }
            var file = Path.Combine(outDir, cls + ".cs");
            using (var w = new StreamWriter(file)) provider.GenerateCodeFromCompileUnit(res.GeneratedCode, w, new CodeGeneratorOptions());
            map[Path.GetFileName(file)] = virt;
        }

        var bin = Path.Combine(web, "bin");
        var fw = Path.Combine(Environment.GetEnvironmentVariable("WINDIR"), @"Microsoft.NET\Framework64\v4.0.30319");
        var refs = Directory.GetFiles(bin, "*.dll").Where(d => !Path.GetFileName(d).StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase)
                                                            && !Path.GetFileName(d).StartsWith("System.Collections.Immutable", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var a in new[] { "System.dll", "System.Core.dll", "System.Web.dll", "System.Data.dll", "System.Xml.dll", "System.Xml.Linq.dll",
                                  "System.ComponentModel.DataAnnotations.dll", "Microsoft.CSharp.dll", "System.Runtime.dll", "System.Web.Extensions.dll",
                                  "System.Configuration.dll", "System.Web.ApplicationServices.dll", "System.Data.DataSetExtensions.dll" })
            if (File.Exists(Path.Combine(fw, a))) refs.Add(Path.Combine(fw, a));
        var facade = Path.Combine(fw, @"..\..\..\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades");
        var rsp = Path.Combine(outDir, "build.rsp");
        File.WriteAllLines(rsp, new[] { "/nologo", "/target:library", "/nowarn:1701,1702,0618,0612,0168,0219", "/out:" + Path.Combine(outDir, "views.dll"),
                                        "/langversion:latest" }
            .Concat(refs.Distinct(StringComparer.OrdinalIgnoreCase).Select(r => "/r:\"" + r + "\""))
            .Concat(new[] { "/r:\"C:\\Program Files (x86)\\Reference Assemblies\\Microsoft\\Framework\\.NETFramework\\v4.8\\Facades\\netstandard.dll\"" })
            .Concat(new[] { "\"" + Path.Combine(outDir, "*.cs") + "\"" }));

        var psi = new ProcessStartInfo(csc, "@\"" + rsp + "\"") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
        var p = Process.Start(psi);
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();

        int errors = genErrors;
        var rx = new Regex(@"^(?<f>[^(]+)\((?<l>\d+),\d+\): error (?<c>CS\d+): (?<m>.*)$");
        foreach (var line in output.Split('\n').Select(x => x.TrimEnd('\r')))
        {
            var m = rx.Match(line);
            if (!m.Success) continue;
            errors++;
            // #line direktivalari tufayli csc xatoni to'g'ridan-to'g'ri .cshtml fayli va qatoriga bog'laydi
            var file = m.Groups["f"].Value.Trim();
            string virt;
            if (!map.TryGetValue(Path.GetFileName(file), out virt))
                virt = file.StartsWith(web, StringComparison.OrdinalIgnoreCase) ? file.Substring(web.Length + 1) : file;
            int viewLine = int.Parse(m.Groups["l"].Value);
            Console.WriteLine("{0}({1}): {2}: {3}", virt, viewLine, m.Groups["c"].Value, m.Groups["m"].Value);
        }
        Console.WriteLine("view: {0}; xato: {1}", n, errors);
        return errors == 0 ? 0 : 1;
    }
}
