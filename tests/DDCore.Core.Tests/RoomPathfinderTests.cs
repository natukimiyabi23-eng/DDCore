// Source-faithful comparison helper transcribed from BossMod FloorPathfind.cs at commit
// 6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0. Copyright (c) 2022-2024, Andrew Gilewsky.
// BSD-3-Clause; see THIRD-PARTY-NOTICES.md and provenance/bossmod-phase4.json.
using DDCore.Core.Navigation;

namespace DDCore.Core.Tests;

public sealed class RoomPathfinderTests
{
    [Fact]
    public void StartAtDestinationReturnsEmptyPath()
    {
        var map = EmptyMap();

        Assert.Empty(RoomPathfinder.FindPath(map, 7, 7));
    }

    [Fact]
    public void FindsStraightPathAndExcludesStart()
    {
        var map = EmptyMap();
        Connect(map, 0, 1);
        Connect(map, 1, 2);
        Connect(map, 2, 3);

        Assert.Equal([1, 2, 3], RoomPathfinder.FindPath(map, 0, 3));
    }

    [Fact]
    public void UsesBossModNorthSouthWestEastTraversalOrder()
    {
        var map = EmptyMap();
        Connect(map, 6, 1);
        Connect(map, 1, 2);
        Connect(map, 2, 7);
        Connect(map, 6, 11);
        Connect(map, 11, 12);
        Connect(map, 12, 7);

        var expected = SourceFaithfulPathfind(map, 6, 7);
        var actual = RoomPathfinder.FindPath(map, 6, 7);

        Assert.Equal([1, 2, 7], expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DisconnectedDestinationReturnsEmptyPath()
    {
        var map = EmptyMap();
        Connect(map, 0, 1);

        Assert.Empty(RoomPathfinder.FindPath(map, 0, 24));
    }

    [Fact]
    public void RepeatedCallsDoNotLeakSearchState()
    {
        var map = EmptyMap();
        Connect(map, 0, 1);
        Connect(map, 1, 2);

        Assert.Equal([1, 2], RoomPathfinder.FindPath(map, 0, 2));
        Assert.Equal([1, 0], RoomPathfinder.FindPath(map, 2, 0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 25)]
    public void InvalidRoomIndexIsRejected(int start, int destination)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RoomPathfinder.FindPath(EmptyMap(), start, destination));
    }

    [Fact]
    public void InvalidMapLengthIsRejected()
    {
        Assert.Throws<ArgumentException>(() => RoomPathfinder.FindPath(new RoomConnectionFlags[24], 0, 1));
    }

    [Fact]
    public void ConnectionOutsideGridIsRejected()
    {
        var map = EmptyMap();
        map[0] = RoomConnectionFlags.North;

        Assert.Throws<InvalidDataException>(() => RoomPathfinder.FindPath(map, 0, 1));
    }

    [Fact]
    public void NonReciprocalConnectionIsRejected()
    {
        var map = EmptyMap();
        map[0] = RoomConnectionFlags.East;

        Assert.Throws<InvalidDataException>(() => RoomPathfinder.FindPath(map, 0, 1));
    }

    [Fact]
    public void UnknownConnectionFlagIsRejected()
    {
        var map = EmptyMap();
        map[12] = (RoomConnectionFlags)(1 << 10);

        Assert.Throws<InvalidDataException>(() => RoomPathfinder.FindPath(map, 12, 13));
    }

    [Fact]
    public void ExtractedImplementationMatchesSourceFaithfulOracleForFixtures()
    {
        foreach (var (map, start, destination) in Fixtures())
        {
            Assert.Equal(SourceFaithfulPathfind(map, start, destination), RoomPathfinder.FindPath(map, start, destination));
        }
    }

    [Fact]
    public void ExtractedImplementationMatchesSourceFaithfulOracleForAllRoomPairs()
    {
        var map = EmptyMap();
        foreach (var (first, second) in new[]
        {
            (12, 7), (7, 2), (7, 6), (6, 5), (6, 11), (11, 10), (11, 16), (16, 15),
            (16, 17), (17, 18), (18, 13), (13, 8), (8, 3), (8, 9), (9, 4), (18, 19),
            (19, 14), (19, 24), (24, 23), (23, 22), (22, 21), (21, 20), (2, 1), (1, 0),
        })
        {
            Connect(map, first, second);
        }

        for (var start = 0; start < RoomPathfinder.RoomCount; start++)
        {
            for (var destination = 0; destination < RoomPathfinder.RoomCount; destination++)
            {
                Assert.Equal(
                    SourceFaithfulPathfind(map, start, destination),
                    RoomPathfinder.FindPath(map, start, destination));
            }
        }
    }

    private static IEnumerable<(RoomConnectionFlags[] Map, int Start, int Destination)> Fixtures()
    {
        var branch = EmptyMap();
        Connect(branch, 12, 7);
        Connect(branch, 7, 6);
        Connect(branch, 6, 11);
        Connect(branch, 11, 16);
        Connect(branch, 16, 17);
        yield return (branch, 12, 17);
        yield return (branch, 17, 7);

        var perimeter = EmptyMap();
        foreach (var (left, right) in new[] { (0, 1), (1, 2), (2, 3), (3, 4), (4, 9), (9, 14), (14, 19), (19, 24) })
        {
            Connect(perimeter, left, right);
        }

        yield return (perimeter, 0, 24);
        yield return (perimeter, 24, 0);
    }

    // Test-only transcription of BossMod FloorPathfind lines 22-60 at commit
    // 6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0. Only the native RoomFlags type is substituted.
    private static IReadOnlyList<int> SourceFaithfulPathfind(RoomConnectionFlags[] map, int startRoom, int destinationRoom)
    {
        if (startRoom == destinationRoom)
        {
            return Array.Empty<int>();
        }

        var explored = new bool[RoomPathfinder.RoomCount];
        var queue = new Queue<List<int>>();
        explored[startRoom] = true;
        queue.Enqueue([startRoom]);
        while (queue.TryDequeue(out var path))
        {
            if (path[^1] == destinationRoom)
            {
                path.RemoveAt(0);
                return path;
            }

            foreach (var adjacent in SourceFaithfulEdges(map, path[^1]))
            {
                if (!explored[adjacent])
                {
                    explored[adjacent] = true;
                    queue.Enqueue([.. path, adjacent]);
                }
            }
        }

        return Array.Empty<int>();
    }

    private static IEnumerable<int> SourceFaithfulEdges(RoomConnectionFlags[] map, int room)
    {
        var flags = map[room];
        if (flags.HasFlag(RoomConnectionFlags.North))
        {
            yield return room - RoomPathfinder.GridWidth;
        }

        if (flags.HasFlag(RoomConnectionFlags.South))
        {
            yield return room + RoomPathfinder.GridWidth;
        }

        if (flags.HasFlag(RoomConnectionFlags.West))
        {
            yield return room - 1;
        }

        if (flags.HasFlag(RoomConnectionFlags.East))
        {
            yield return room + 1;
        }
    }

    private static RoomConnectionFlags[] EmptyMap() => new RoomConnectionFlags[RoomPathfinder.RoomCount];

    private static void Connect(RoomConnectionFlags[] map, int first, int second)
    {
        var delta = second - first;
        var (forward, reverse) = delta switch
        {
            -RoomPathfinder.GridWidth => (RoomConnectionFlags.North, RoomConnectionFlags.South),
            RoomPathfinder.GridWidth => (RoomConnectionFlags.South, RoomConnectionFlags.North),
            -1 when first / RoomPathfinder.GridWidth == second / RoomPathfinder.GridWidth => (RoomConnectionFlags.West, RoomConnectionFlags.East),
            1 when first / RoomPathfinder.GridWidth == second / RoomPathfinder.GridWidth => (RoomConnectionFlags.East, RoomConnectionFlags.West),
            _ => throw new ArgumentException("Rooms are not cardinally adjacent."),
        };

        map[first] |= forward;
        map[second] |= reverse;
    }
}
