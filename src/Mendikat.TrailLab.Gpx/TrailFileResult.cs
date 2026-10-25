using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Gpx;

/// <summary>Outcome of reading a file: its content plus non-fatal warnings.</summary>
public sealed record TrailFileResult(TrailData Data, IReadOnlyList<LoadWarning> Warnings);
