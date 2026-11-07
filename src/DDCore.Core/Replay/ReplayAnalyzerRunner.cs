using System.Collections.Immutable;
using DDCore.Core.Mechanics;

namespace DDCore.Core.Replay;

public sealed class ReplayAnalyzerRunner(ReplayCodec codec, AnalyzerRegistry registry)
{
    public ImmutableArray<MechanicFrame> Run(ReplayDocument document)
    {
        codec.Validate(document);
        var session = MechanicAnalysisSession.Empty;
        var frames = ImmutableArray.CreateBuilder<MechanicFrame>(document.Snapshots.Length);
        foreach (var snapshot in document.Snapshots)
        {
            var result = registry.Analyze(snapshot, session);
            session = result.Session;
            frames.Add(result.Frame);
        }

        return frames.ToImmutable();
    }
}
