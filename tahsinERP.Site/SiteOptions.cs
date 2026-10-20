namespace tahsinERP.Site;

/// <summary>"Site" bo'limi. MasterKey — ERP'dagi TAHSINERP_BOT_KEY bilan bir xil (Site__MasterKey).</summary>
public sealed class SiteOptions
{
    public string MasterKey { get; set; } = "";

    /// <summary>Standart subdomenlar asosi: {slug}.{BaseDomain} (masalan "tahsin.uz").</summary>
    public string BaseDomain { get; set; } = "";

    /// <summary>Rasm keshi va asl nusxalar papkasi (nisbiy bo'lsa — ContentRoot ichida).</summary>
    public string DataRoot { get; set; } = "data";

    /// <summary>ERP'dan katalog nusxasini so'rash oralig'i (ETag bilan — o'zgarmasa 304).</summary>
    public int SyncSeconds { get; set; } = 60;

    public int ErpTimeoutSeconds { get; set; } = 30;

    /// <summary>Development: localhost'da ?tenant=slug bilan istalgan tenantni ochish.</summary>
    public bool AllowTenantQuery { get; set; }
}
