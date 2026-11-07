// Adapted from BossMod FloorPathfind.cs at commit 6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0.
// Copyright (c) 2022-2024, Andrew Gilewsky. BSD-3-Clause; see THIRD-PARTY-NOTICES.md.
namespace DDCore.Core.Navigation;

[Flags]
public enum RoomConnectionFlags
{
    None = 0,
    North = 1 << 0,
    South = 1 << 1,
    West = 1 << 2,
    East = 1 << 3,
}

/// <summary>
/// Finds a shortest route through the fixed 5x5 Deep Dungeon room grid.
/// The returned route excludes the starting room and includes the destination.
/// </summary>
public static class RoomPathfinder
{
    public const int GridWidth = 5;
    public const int RoomCount = GridWidth * GridWidth;

    public static IReadOnlyList<int> FindPath(
        IReadOnlyList<RoomConnectionFlags> map,
        int startRoom,
        int destinationRoom)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.Count != RoomCount)
        {
            throw new ArgumentException($"A Deep Dungeon room map must contain exactly {RoomCount} rooms.", nameof(map));
        }

        ValidateRoomIndex(startRoom, nameof(startRoom));
        ValidateRoomIndex(destinationRoom, nameof(destinationRoom));
        ValidateConnections(map);

        if (startRoom == destinationRoom)
        {
            return Array.Empty<int>();
        }

        var explored = new bool[RoomCount];
        var previous = new int[RoomCount];
        Array.Fill(previous, -1);
        var queue = new Queue<int>();
        explored[startRoom] = true;
        queue.Enqueue(startRoom);

        while (queue.TryDequeue(out var current))
        {
            foreach (var next in Edges(map, current))
            {
                if (explored[next])
                {
                    continue;
                }

                explored[next] = true;
                previous[next] = current;
                if (next == destinationRoom)
                {
                    return BuildPath(previous, startRoom, destinationRoom);
                }

                queue.Enqueue(next);
            }
        }

        return Array.Empty<int>();
    }

    private static IReadOnlyList<int> BuildPath(int[] previous, int startRoom, int destinationRoom)
    {
        var path = new List<int>();
        for (var room = destinationRoom; room != startRoom; room = previous[room])
        {
            if (room < 0)
            {
                return Array.Empty<int>();
            }

            path.Add(room);
        }

        path.Reverse();
        return path;
    }

    private static IEnumerable<int> Edges(IReadOnlyList<RoomConnectionFlags> map, int roomIndex)
    {
        var flags = map[roomIndex];
        if (flags.HasFlag(RoomConnectionFlags.North))
        {
            yield return roomIndex - GridWidth;
        }

        if (flags.HasFlag(RoomConnectionFlags.South))
        {
            yield return roomIndex + GridWidth;
        }

        if (flags.HasFlag(RoomConnectionFlags.West))
        {
            yield return roomIndex - 1;
        }

        if (flags.HasFlag(RoomConnectionFlags.East))
        {
            yield return roomIndex + 1;
        }
    }

    private static void ValidateConnections(IReadOnlyList<RoomConnectionFlags> map)
    {
        const RoomConnectionFlags knownFlags = RoomConnectionFlags.North
            | RoomConnectionFlags.South
            | RoomConnectionFlags.West
            | RoomConnectionFlags.East;
        for (var room = 0; room < RoomCount; room++)
        {
            var flags = map[room];
            if ((flags & ~knownFlags) != 0)
            {
                throw new InvalidDataException($"Room {room} contains unknown connection flags.");
            }

            var row = room / GridWidth;
            var column = room % GridWidth;
            if ((row == 0 && flags.HasFlag(RoomConnectionFlags.North))
                || (row == GridWidth - 1 && flags.HasFlag(RoomConnectionFlags.South))
                || (column == 0 && flags.HasFlag(RoomConnectionFlags.West))
                || (column == GridWidth - 1 && flags.HasFlag(RoomConnectionFlags.East)))
            {
                throw new InvalidDataException($"Room {room} contains a connection outside the 5x5 grid.");
            }

            ValidateReciprocalConnection(map, room, flags, RoomConnectionFlags.North, -GridWidth, RoomConnectionFlags.South);
            ValidateReciprocalConnection(map, room, flags, RoomConnectionFlags.South, GridWidth, RoomConnectionFlags.North);
            ValidateReciprocalConnection(map, room, flags, RoomConnectionFlags.West, -1, RoomConnectionFlags.East);
            ValidateReciprocalConnection(map, room, flags, RoomConnectionFlags.East, 1, RoomConnectionFlags.West);
        }
    }

    private static void ValidateReciprocalConnection(
        IReadOnlyList<RoomConnectionFlags> map,
        int room,
        RoomConnectionFlags flags,
        RoomConnectionFlags direction,
        int offset,
        RoomConnectionFlags reverse)
    {
        if (!flags.HasFlag(direction))
        {
            return;
        }

        var adjacent = room + offset;
        if (adjacent < 0 || adjacent >= RoomCount || !map[adjacent].HasFlag(reverse))
        {
            throw new InvalidDataException($"Room {room} has a non-reciprocal {direction} connection.");
        }
    }

    private static void ValidateRoomIndex(int room, string parameterName)
    {
        if (room is < 0 or >= RoomCount)
        {
            throw new ArgumentOutOfRangeException(parameterName, room, $"Room index must be between 0 and {RoomCount - 1}.");
        }
    }
}
