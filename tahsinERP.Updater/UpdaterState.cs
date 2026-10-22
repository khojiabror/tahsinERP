using System.Text;
using System.Text.Json;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.Updater;

public sealed class StateStore(UpdaterOptions options)
{
    public static JsonSerializerOptions Json => UpdaterFiles.Json;

    private readonly object _gate = new();

    public UpdaterState Load()
    {
        lock (_gate)
        {
            string path = options.Paths.StateFile;
            if (!File.Exists(path)) return new UpdaterState();
            var s = JsonSerializer.Deserialize<UpdaterState>(File.ReadAllText(path), Json) ?? new UpdaterState();
            // JSON'dan o'qilgan lug'at registrga sezgir — qayta quramiz
            s.Apps = new Dictionary<string, TargetState>(s.Apps, StringComparer.OrdinalIgnoreCase);
            s.Instances = new Dictionary<string, TargetState>(s.Instances, StringComparer.OrdinalIgnoreCase);
            return s;
        }
    }

    public void Save(UpdaterState state)
    {
        lock (_gate)
        {
            string path = options.Paths.StateFile;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(state, Json), new UTF8Encoding(false));
            File.Move(tmp, path, overwrite: true);
        }
    }

    public void Update(Action<UpdaterState> change)
    {
        lock (_gate)
        {
            var s = Load();
            change(s);
            Save(s);
        }
    }
}

/// <summary>Bosqich va log'ni status faylga yozib boradi (har qatorda — agent jonli ko'rsatadi).</summary>
public sealed class JobReporter(string statusPath, long jobId, ILogger? log = null)
{
    private readonly JobStatus _s = new() { JobId = jobId };
    private readonly object _gate = new();

    public string Log { get { lock (_gate) return _s.Log; } }

    public void Stage(string stage)
    {
        lock (_gate) { _s.Stage = stage; }
        Line("== " + stage);
    }

    public void Line(string text)
    {
        lock (_gate)
        {
            _s.Log += $"{DateTime.Now:HH:mm:ss} {text}\n";
            if (_s.Log.Length > 200_000) _s.Log = "…\n" + _s.Log[^150_000..];
            Flush();
        }
        log?.LogInformation("Job #{JobId}: {Text}", jobId, text);
    }

    public void Finish(UpdateOutcome outcome, string? error, string? installedVersion)
    {
        lock (_gate)
        {
            _s.Final = true; _s.Outcome = outcome; _s.Error = error; _s.InstalledVersion = installedVersion;
            Flush();
        }
    }

    private void Flush()
    {
        _s.UpdatedAtUtc = DateTime.UtcNow;
        Directory.CreateDirectory(Path.GetDirectoryName(statusPath)!);
        string tmp = statusPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_s, StateStore.Json), new UTF8Encoding(false));
        File.Move(tmp, statusPath, overwrite: true);
    }
}
