using System.Security.Cryptography;
using System.Xml;

namespace tahsinERP.Updater;

/// <summary>
/// ERP Web.config: relizdagi yangi fayl ustiga serverdagi qiymatlar qaytariladi — machineKey (cookie/antiforgery buzilmasin,
/// Install-Erp.ps1 bilan bir xil) va tanlangan appSettings (SiteBaseDomain, SiteCnameTarget).
/// </summary>
public static class WebConfigMerger
{
    public sealed class Preserved
    {
        public Dictionary<string, string> MachineKey { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> AppSettings { get; } = new(StringComparer.Ordinal);
        public bool HasMachineKey => MachineKey.ContainsKey("validationKey") && MachineKey.ContainsKey("decryptionKey");
    }

    public static Preserved Capture(string webConfig, IEnumerable<string> appSettingKeys)
    {
        var p = new Preserved();
        if (!File.Exists(webConfig)) return p;
        var doc = Load(webConfig);
        if (doc.SelectSingleNode("/configuration/system.web/machineKey") is XmlElement mk)
            foreach (XmlAttribute a in mk.Attributes) p.MachineKey[a.Name] = a.Value;
        foreach (var key in appSettingKeys)
            if (doc.SelectSingleNode($"/configuration/appSettings/add[@key='{key}']") is XmlElement add && add.HasAttribute("value"))
                p.AppSettings[key] = add.GetAttribute("value");
        return p;
    }

    /// <summary>Saqlangan qiymatlarni yozadi. machineKey yo'q bo'lsa — yangi tasodifiy (repodagi qiymat hech qachon qolmaydi).</summary>
    public static void Apply(string webConfig, Preserved p)
    {
        var doc = Load(webConfig);
        var root = doc.DocumentElement ?? throw new InvalidOperationException("Web.config bo'sh.");
        var sw = Child(doc, root, "system.web");
        var mk = Child(doc, sw, "machineKey");
        if (p.HasMachineKey)
            foreach (var (k, v) in p.MachineKey) mk.SetAttribute(k, v);
        else
        {
            mk.SetAttribute("validationKey", Convert.ToHexString(RandomNumberGenerator.GetBytes(64)));
            mk.SetAttribute("decryptionKey", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            mk.SetAttribute("validation", "HMACSHA256");
            mk.SetAttribute("decryption", "AES");
        }
        if (p.AppSettings.Count > 0)
        {
            var app = Child(doc, root, "appSettings");
            foreach (var (k, v) in p.AppSettings)
            {
                if (app.SelectSingleNode($"add[@key='{k}']") is not XmlElement add)
                {
                    add = doc.CreateElement("add");
                    add.SetAttribute("key", k);
                    app.AppendChild(add);
                }
                add.SetAttribute("value", v);
            }
        }
        doc.Save(webConfig);
    }

    private static XmlDocument Load(string path)
    {
        var doc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        using var r = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        doc.Load(r);
        return doc;
    }

    private static XmlElement Child(XmlDocument doc, XmlElement parent, string name)
    {
        if (parent.SelectSingleNode(name) is XmlElement e) return e;
        e = doc.CreateElement(name);
        parent.AppendChild(e);
        return e;
    }
}
