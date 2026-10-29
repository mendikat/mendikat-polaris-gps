using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Domain.Processing;

namespace Mendikat.TrailLab.Tests.Domain;

public sealed class JoinTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 1, 7, 0, 0, TimeSpan.Zero);

    /// <summary>~111 m per 0.001° of latitude; points along a north-south line at -1.6°.</summary>
    private static Track Line(string name, double fromLat, double toLat, int count = 3, int? firstSecond = null)
    {
        var segment = new TrackSegment();
        for (var i = 0; i < count; i++)
        {
            segment.Points.Add(new TrailPoint(new GeoPosition(fromLat + (toLat - fromLat) * i / (count - 1), -1.6))
            {
                Elevation = 500 + i,
                Time = firstSecond is { } s ? Start.AddSeconds(s + 60 * i) : null,
            });
        }

        var track = new Track { Name = name };
        track.Segments.Add(segment);
        return track;
    }

    [Fact]
    public void Tracks_are_chained_by_their_nearest_ends_whatever_their_order_in_the_list()
    {
        var a = Line("A", 42.800, 42.810);
        var b = Line("B", 42.8101, 42.820);
        var c = Line("C", 42.8202, 42.830);

        var plan = TrackJoiner.Plan([c, a, b], maxGapMeters: 200, allowReverse: true);

        Assert.Equal([1, 2, 0], plan.Steps.Select(s => s.Index));
        Assert.All(plan.Steps, s => Assert.False(s.Reversed));
        Assert.Null(plan.Steps[0].GapMeters);
        Assert.InRange(plan.Steps[1].GapMeters!.Value, 10, 12);
        Assert.InRange(plan.Steps[2].GapMeters!.Value, 21, 23);
        Assert.All(plan.Steps, s => Assert.False(s.StartsSegment));
    }

    [Fact]
    public void A_track_drawn_the_other_way_is_reversed_with_its_times_mirrored()
    {
        var a = Line("A", 42.800, 42.810, firstSecond: 0);
        var b = Line("B", 42.820, 42.8101, firstSecond: 3600); // recorded from its far end

        var plan = TrackJoiner.Plan([a, b], 200, allowReverse: true);
        var joined = TrackJoiner.Build(plan);

        Assert.Equal([false, true], plan.Steps.Select(s => s.Reversed));
        var segment = Assert.Single(joined.Segments);
        Assert.Equal(6, segment.Points.Count);
        Assert.Equal(42.8101, segment.Points[3].Position.Latitude, 9);
        Assert.Equal([0, 60, 120, 3600, 3660, 3720], segment.Points.Select(p => (p.Time!.Value - Start).TotalSeconds));
        Assert.Equal([500, 501, 502, 502, 501, 500], segment.Points.Select(p => p.Elevation!.Value));
    }

    [Fact]
    public void Without_reversing_the_direction_of_every_track_is_kept()
    {
        var a = Line("A", 42.800, 42.810);
        var b = Line("B", 42.820, 42.8101);

        var plan = TrackJoiner.Plan([a, b], 200, allowReverse: false);

        Assert.All(plan.Steps, s => Assert.False(s.Reversed));
        Assert.True(plan.Steps[1].GapMeters > 1000);
    }

    [Fact]
    public void A_gap_longer_than_the_maximum_starts_a_new_segment()
    {
        var a = Line("A", 42.800, 42.810);
        var b = Line("B", 42.815, 42.820); // ~555 m away

        var plan = TrackJoiner.Plan([a, b], maxGapMeters: 500, allowReverse: true);
        var joined = TrackJoiner.Build(plan);

        Assert.True(plan.Steps[1].StartsSegment);
        Assert.Equal([3, 3], joined.Segments.Select(s => s.Points.Count));

        var looser = TrackJoiner.Build(TrackJoiner.Plan([a, b], maxGapMeters: 600, allowReverse: true));
        Assert.Equal([6], looser.Segments.Select(s => s.Points.Count));
    }

    [Fact]
    public void The_joined_track_is_a_copy_with_the_first_tracks_data_and_its_inner_segments()
    {
        var a = Line("A", 42.800, 42.810);
        a.Type = "hiking";
        a.OtherElements.Add(new XmlFragment("number", "<number xmlns=\"http://www.topografix.com/GPX/1/1\">7</number>"));
        a.Segments.Add(new TrackSegment()); // empty: dropped
        var b = Line("B", 42.8101, 42.820);
        b.Segments.Add(Line("B2", 42.830, 42.840).Segments[0]);
        b.Segments[0].Points[0].OtherElements.Add(new XmlFragment("extensions", "<extensions xmlns=\"http://www.topografix.com/GPX/1/1\" />"));
        var originals = a.AllPoints.Concat(b.AllPoints).ToList();

        var joined = TrackJoiner.Build(TrackJoiner.Plan([a, b], 200, allowReverse: true));

        Assert.Equal("A", joined.Name);
        Assert.Equal("hiking", joined.Type);
        Assert.Single(joined.OtherElements);
        Assert.Equal([6, 3], joined.Segments.Select(s => s.Points.Count)); // B's own gap between segments is kept
        Assert.Equal(originals.Select(p => p.Position), joined.AllPoints.Select(p => p.Position));
        Assert.DoesNotContain(joined.AllPoints, p => originals.Contains(p, ReferenceEqualityComparer.Instance));
        Assert.Single(joined.Segments[0].Points[3].OtherElements);
        Assert.Equal(2, a.Segments.Count); // the originals do not change
        Assert.Equal(3, a.PointCount);
    }

    [Fact]
    public void On_a_tie_the_chain_keeps_the_tracks_in_their_own_direction()
    {
        // A → B and B reversed → A reversed have the same gap; the forward chain wins.
        var a = Line("A", 42.800, 42.810);
        var b = Line("B", 42.8101, 42.820);

        var plan = TrackJoiner.Plan([b, a], 200, allowReverse: true);

        Assert.Equal([1, 0], plan.Steps.Select(s => s.Index));
        Assert.Equal(0, plan.ReversedCount);
    }

    [Fact]
    public void Fewer_than_two_tracks_or_an_empty_track_cannot_be_joined()
    {
        Assert.Throws<ArgumentException>(() => TrackJoiner.Plan([Line("A", 42.8, 42.81)], 200, true));
        Assert.Throws<ArgumentException>(() => TrackJoiner.Plan([Line("A", 42.8, 42.81), new Track()], 200, true));
    }
}
