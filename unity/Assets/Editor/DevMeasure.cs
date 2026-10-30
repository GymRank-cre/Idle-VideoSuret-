using Unity.Profiling;

// Outil de vérification en Play (appelé par le MCP) : mémoire allouée par image sur les dernières images.
public static class DevMeasure
{
    const int Frames = 240;

    static ProfilerRecorder recorder;

    public static string Start()
    {
        recorder.Dispose();
        recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", Frames);
        return "mesure lancée";
    }

    public static string Read()
    {
        if (!recorder.Valid) return "pas de mesure";
        int count = recorder.Count, zero = 0;
        long total = 0, max = 0;
        for (int i = 0; i < count; i++)
        {
            long bytes = recorder.GetSample(i).Value;
            total += bytes;
            if (bytes > max) max = bytes;
            if (bytes == 0) zero++;
        }
        recorder.Dispose();
        return count + " images : moyenne " + (count > 0 ? total / count : 0) + " o, maximum " + max + " o, " + zero + " images à 0 o";
    }
}
