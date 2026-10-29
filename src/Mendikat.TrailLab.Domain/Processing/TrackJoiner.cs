using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Domain.Processing;

/// <summary>One track of a join, in the order it goes into the joined track.</summary>
/// <param name="Index">Position of the track in the list given to <see cref="TrackJoiner.Plan"/>.</param>
/// <param name="Reversed">The track goes in the opposite direction.</param>
/// <param name="GapMeters">Distance from the end of the previous track to the start of this one; null for the first.</param>
/// <param name="StartsSegment">The gap is longer than the maximum, so this track starts a new segment.</param>
public sealed record JoinStep(int Index, bool Reversed, double? GapMeters, bool StartsSegment);

/// <summary>Order and direction of the tracks to join, with the gaps between them.</summary>
public sealed record JoinPlan(IReadOnlyList<Track> Tracks, IReadOnlyList<JoinStep> Steps, double MaxGapMeters)
{
    public double TotalGapMeters => Steps.Sum(s => s.GapMeters ?? 0);

    public int ReversedCount => Steps.Count(s => s.Reversed);
}

/// <summary>
/// Joins two or more tracks by proximity: chooses the order (and, if allowed, the direction) of the tracks so that the
/// end of each one is close to the start of the next. From every track end as a starting point, it repeatedly appends
/// the unused track with the nearest end (greedy nearest neighbour) and keeps the chain with the smallest total gap;
/// on a tie, the one with fewer reversed tracks. Distances are geodesic (WGS84).
/// </summary>
public static class TrackJoiner
{
    /// <summary>Two chains whose total gaps differ by less than this are equally good.</summary>
    private const double TieMeters = 0.001;

    /// <summary>Plans the join; every track must have at least one point.</summary>
    public static JoinPlan Plan(IReadOnlyList<Track> tracks, double maxGapMeters, bool allowReverse)
    {
        if (tracks.Count < 2)
        {
            throw new ArgumentException("At least two tracks are needed.", nameof(tracks));
        }

        var ends = tracks.Select(t => t.AllPoints.Any()
            ? (Start: t.AllPoints.First().Position, End: t.AllPoints.Last().Position)
            : throw new ArgumentException("Every track must have points.", nameof(tracks))).ToList();

        // distance[a, b]: from the exit of track a in direction ra to the entry of track b in direction rb,
        // stored at [2a + ra, 2b + rb]. The exit of a forward track is its end; its entry is its start.
        var n = tracks.Count;
        var distance = new double[2 * n, 2 * n];
        for (var a = 0; a < 2 * n; a++)
        {
            var exit = a % 2 == 0 ? ends[a / 2].End : ends[a / 2].Start;
            for (var b = 0; b < 2 * n; b++)
            {
                var entry = b % 2 == 0 ? ends[b / 2].Start : ends[b / 2].End;
                distance[a, b] = a / 2 == b / 2 ? double.PositiveInfinity : Geodesy.Distance(exit, entry);
            }
        }

        List<(int Track, bool Reversed)>? best = null;
        var bestGap = double.PositiveInfinity;
        var bestReversed = int.MaxValue;
        bool[] directions = allowReverse ? [false, true] : [false];
        foreach (var first in Enumerable.Range(0, n))
        {
            foreach (var reversed in directions)
            {
                var (chain, gap) = Chain(first, reversed, n, distance, allowReverse);
                var reversedCount = chain.Count(c => c.Reversed);
                if (gap < bestGap - TieMeters || (gap < bestGap + TieMeters && reversedCount < bestReversed))
                {
                    (best, bestGap, bestReversed) = (chain, gap, reversedCount);
                }
            }
        }

        var steps = new List<JoinStep>(n);
        for (var i = 0; i < best!.Count; i++)
        {
            var (track, reversed) = best[i];
            double? gap = i == 0 ? null : distance[Node(best[i - 1]), Node(best[i])];
            steps.Add(new JoinStep(track, reversed, gap, gap > maxGapMeters));
        }

        return new JoinPlan(tracks, steps, maxGapMeters);
    }

    /// <summary>
    /// The joined track, made of copies of the points (the original tracks do not change). It keeps the name, type and
    /// other data of the first track. Reversed tracks have their times mirrored as in <see cref="TrackReverser"/>.
    /// Empty segments are dropped.
    /// </summary>
    public static Track Build(JoinPlan plan, string? name = null)
    {
        var first = plan.Tracks[plan.Steps[0].Index];
        var result = new Track
        {
            Name = name ?? first.Name,
            Comment = first.Comment,
            Description = first.Description,
            Type = first.Type,
        };
        result.OtherElements.AddRange(first.OtherElements);

        foreach (var step in plan.Steps)
        {
            var pieces = Pieces(plan.Tracks[step.Index], step.Reversed);
            for (var i = 0; i < pieces.Count; i++)
            {
                if (i == 0 && result.Segments.Count > 0 && !step.StartsSegment)
                {
                    result.Segments[^1].Points.AddRange(pieces[i].Points);
                }
                else
                {
                    result.Segments.Add(pieces[i]);
                }
            }
        }

        return result;
    }

    private static int Node((int Track, bool Reversed) item) => 2 * item.Track + (item.Reversed ? 1 : 0);

    private static (List<(int Track, bool Reversed)> Chain, double Gap) Chain(int first, bool reversed, int n, double[,] distance, bool allowReverse)
    {
        var used = new bool[n];
        used[first] = true;
        var chain = new List<(int Track, bool Reversed)>(n) { (first, reversed) };
        var gap = 0.0;
        while (chain.Count < n)
        {
            var from = Node(chain[^1]);
            var next = -1;
            var nextDistance = double.PositiveInfinity;
            for (var b = 0; b < 2 * n; b++)
            {
                if (!used[b / 2] && (allowReverse || b % 2 == 0) && distance[from, b] < nextDistance)
                {
                    (next, nextDistance) = (b, distance[from, b]);
                }
            }

            used[next / 2] = true;
            chain.Add((next / 2, next % 2 == 1));
            gap += nextDistance;
        }

        return (chain, gap);
    }

    /// <summary>Copies of the non-empty segments of a track, in the direction of the join.</summary>
    private static List<TrackSegment> Pieces(Track track, bool reversed)
    {
        var mirror = reversed ? MirroredTimes(track) : null;
        var segments = reversed ? track.Segments.AsEnumerable().Reverse() : track.Segments;
        var pieces = new List<TrackSegment>();
        foreach (var segment in segments.Where(s => s.Points.Count > 0))
        {
            var piece = new TrackSegment();
            piece.OtherElements.AddRange(segment.OtherElements);
            var points = reversed ? segment.Points.AsEnumerable().Reverse() : segment.Points;
            piece.Points.AddRange(points.Select(p => Copy(p, mirror)));
            pieces.Add(piece);
        }

        return pieces;
    }

    /// <summary>t' = t_first + t_last − t, as when reversing a track; null if no point has a time.</summary>
    private static Func<DateTimeOffset, DateTimeOffset>? MirroredTimes(Track track)
    {
        var times = track.AllPoints.Where(p => p.Time is not null).Select(p => p.Time!.Value).ToList();
        if (times.Count == 0)
        {
            return null;
        }

        var first = times.Min().ToUniversalTime();
        var last = times.Max();
        return t => first + (last - t);
    }

    private static TrailPoint Copy(TrailPoint point, Func<DateTimeOffset, DateTimeOffset>? mirror)
    {
        var copy = new TrailPoint(point.Position)
        {
            Elevation = point.Elevation,
            Time = point.Time is { } time && mirror is not null ? mirror(time) : point.Time,
            Name = point.Name,
            Comment = point.Comment,
            Description = point.Description,
            Symbol = point.Symbol,
            Type = point.Type,
            IsAdded = point.IsAdded,
            ElevationSource = point.ElevationSource,
        };
        copy.OtherElements.AddRange(point.OtherElements);
        return copy;
    }
}
