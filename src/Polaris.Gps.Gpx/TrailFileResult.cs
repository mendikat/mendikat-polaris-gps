using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Domain.Model;

namespace Polaris.Gps.Gpx;

/// <summary>Outcome of reading a file: its content plus non-fatal warnings.</summary>
public sealed record TrailFileResult(TrailData Data, IReadOnlyList<LoadWarning> Warnings);
