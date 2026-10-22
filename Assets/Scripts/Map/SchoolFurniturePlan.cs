using System;
using System.Collections.Generic;
using Escape4Now.Obstacles;
#if UNITY_5_3_OR_NEWER
using UnitySerializeField = UnityEngine.SerializeField;
#else
using UnitySerializeField = System.NonSerializedAttribute;
#endif

namespace Escape4Now.Map
{
    public enum SchoolFurniturePattern { Rows, Perimeter, Groups, CenterAisle }

    [Serializable]
    public sealed class SchoolRoomPatternSettings
    {
        [UnitySerializeField] private SchoolRoomType room;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Pattern Weights"), UnityEngine.Min(0f)]
#endif
        [UnitySerializeField] private float rowsWeight, perimeterWeight, groupsWeight, centerAisleWeight;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Arrangement"), UnityEngine.Range(2, 4)]
#endif
        [UnitySerializeField] private int rowSpacing = 2;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(1, 2)]
#endif
        [UnitySerializeField] private int aisleWidth = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(2, 5)]
#endif
        [UnitySerializeField] private int groupSpacing = 3;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Min(0f)]
#endif
        [UnitySerializeField] private float wallPreference = 8f;
        [UnitySerializeField] private bool randomizeOrientation = true;
        [UnitySerializeField] private ObstacleFacing frontWall = ObstacleFacing.North;

        public SchoolRoomType Room => room;
        public int RowSpacing => rowSpacing;
        public int AisleWidth => aisleWidth;
        public int GroupSpacing => groupSpacing;
        public float WallPreference => wallPreference;
        public bool RandomizeOrientation => randomizeOrientation;
        public ObstacleFacing FrontWall => frontWall;
        public float Weight(SchoolFurniturePattern pattern) => pattern == SchoolFurniturePattern.Rows ? rowsWeight
            : pattern == SchoolFurniturePattern.Perimeter ? perimeterWeight : pattern == SchoolFurniturePattern.Groups ? groupsWeight : centerAisleWeight;
        public SchoolRoomPatternSettings(SchoolRoomType room, float rows, float perimeter, float groups, float aisle, int aisleWidth = 1)
        { this.room = room; rowsWeight = rows; perimeterWeight = perimeter; groupsWeight = groups; centerAisleWeight = aisle; this.aisleWidth = aisleWidth; }
        public SchoolRoomPatternSettings Copy() => (SchoolRoomPatternSettings)MemberwiseClone();
        public bool IsValid => room != SchoolRoomType.None && Enum.IsDefined(typeof(SchoolRoomType), room)
            && rowsWeight >= 0f && perimeterWeight >= 0f && groupsWeight >= 0f && centerAisleWeight >= 0f
            && !float.IsInfinity(rowsWeight + perimeterWeight + groupsWeight + centerAisleWeight)
            && rowsWeight + perimeterWeight + groupsWeight + centerAisleWeight > 0f
            && rowSpacing >= 2 && rowSpacing <= 4 && aisleWidth >= 1 && aisleWidth <= 2
            && groupSpacing >= 2 && groupSpacing <= 5 && wallPreference >= 0f && !float.IsInfinity(wallPreference)
            && Enum.IsDefined(typeof(ObstacleFacing), frontWall);
    }

    public sealed class SchoolRoomArrangement
    {
        public SchoolRoomType Room { get; }
        public SchoolFurniturePattern Pattern { get; }
        public ObstacleFacing FrontWall { get; }
        public SchoolRoomArrangement(SchoolRoomType room, SchoolFurniturePattern pattern, ObstacleFacing frontWall)
        { Room = room; Pattern = pattern; FrontWall = frontWall; }
    }

    [Serializable]
    public sealed class SchoolFurnitureSettings
    {
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Library")]
        [UnityEngine.Range(2, 16)]
        [UnityEngine.Tooltip("Required number range for library bookshelves.")]
#endif
        [UnitySerializeField] private int libraryShelvesMinimum = 2;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(2, 16)]
#endif
        [UnitySerializeField] private int libraryShelvesMaximum = 3;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(0, 8)]
#endif
        [UnitySerializeField] private int libraryTablesMaximum = 2;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(0, 1)]
#endif
        [UnitySerializeField] private int libraryCartsMaximum = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Computer Lab")]
        [UnityEngine.Range(2, 16)]
#endif
        [UnitySerializeField] private int computerDesksMinimum = 2;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(2, 16)]
#endif
        [UnitySerializeField] private int computerDesksMaximum = 4;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(0, 1)]
#endif
        [UnitySerializeField] private int printersMaximum = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Classroom")]
        [UnityEngine.Range(3, 16)]
#endif
        [UnitySerializeField] private int classroomDesksMinimum = 3;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(3, 16)]
#endif
        [UnitySerializeField] private int classroomDesksMaximum = 5;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(0, 1)]
#endif
        [UnitySerializeField] private int teacherDesksMaximum = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(1, 2)]
#endif
        [UnitySerializeField] private int whiteboardCount = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Science Lab")]
        [UnityEngine.Range(2, 16)]
#endif
        [UnitySerializeField] private int scienceTablesMinimum = 2;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(2, 16)]
#endif
        [UnitySerializeField] private int scienceTablesMaximum = 4;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(0, 1)]
#endif
        [UnitySerializeField] private int scienceCabinetsMaximum = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(0, 1)]
#endif
        [UnitySerializeField] private int scienceSinksMaximum = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Hallways")]
        [UnityEngine.Range(1, 2)]
#endif
        [UnitySerializeField] private int hallwayObjectsMinimum = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(1, 2)]
#endif
        [UnitySerializeField] private int hallwayObjectsMaximum = 2;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Occasional Special Rooms")]
        [UnityEngine.Range(0, 1)]
#endif
        [UnitySerializeField] private int specialRoomExtraObjectsMaximum = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Room Layout Patterns")]
#endif
        [UnitySerializeField] private SchoolRoomPatternSettings[] roomPatterns =
        {
            new SchoolRoomPatternSettings(SchoolRoomType.Library, 3f, 4f, 1f, 2f),
            new SchoolRoomPatternSettings(SchoolRoomType.ComputerLab, 4f, 3f, 2f, 2f),
            new SchoolRoomPatternSettings(SchoolRoomType.Classroom, 4f, 0f, 3f, 3f),
            new SchoolRoomPatternSettings(SchoolRoomType.ScienceLab, 4f, 1f, 3f, 2f)
        };
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Spacing and Safe Access"), UnityEngine.Range(1, 4)]
#endif
        [UnitySerializeField] private int doorClearance = 2;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(0, 3)]
#endif
        [UnitySerializeField] private int furnitureSpacing = 1, spawnClearance = 1, centerClearance = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(1, 6)]
#endif
        [UnitySerializeField] private int hallwaySpacing = 2;
        [UnitySerializeField] private bool preventNewDeadEnds = true;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Room Size and Density"), UnityEngine.Min(1)]
#endif
        [UnitySerializeField] private int sparseRoomArea = 36, fullRoomArea = 100;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(0f, 1f)]
#endif
        [UnitySerializeField] private float countVariation = .15f, optionalFurnitureChance = .8f;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Placement Limits"), UnityEngine.Range(1, 32)]
#endif
        [UnitySerializeField] private int roomLayoutAttempts = 8;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(1, 625)]
#endif
        [UnitySerializeField] private int candidateChecksPerObject = 196;

        public int LibraryShelvesMinimum { get => libraryShelvesMinimum; set => libraryShelvesMinimum = value; }
        public int LibraryShelvesMaximum { get => libraryShelvesMaximum; set => libraryShelvesMaximum = value; }
        public int LibraryTablesMaximum { get => libraryTablesMaximum; set => libraryTablesMaximum = value; }
        public int LibraryCartsMaximum { get => libraryCartsMaximum; set => libraryCartsMaximum = value; }
        public int ComputerDesksMinimum { get => computerDesksMinimum; set => computerDesksMinimum = value; }
        public int ComputerDesksMaximum { get => computerDesksMaximum; set => computerDesksMaximum = value; }
        public int PrintersMaximum { get => printersMaximum; set => printersMaximum = value; }
        public int ClassroomDesksMinimum { get => classroomDesksMinimum; set => classroomDesksMinimum = value; }
        public int ClassroomDesksMaximum { get => classroomDesksMaximum; set => classroomDesksMaximum = value; }
        public int TeacherDesksMaximum { get => teacherDesksMaximum; set => teacherDesksMaximum = value; }
        public int WhiteboardCount => whiteboardCount;
        public int ScienceTablesMinimum { get => scienceTablesMinimum; set => scienceTablesMinimum = value; }
        public int ScienceTablesMaximum { get => scienceTablesMaximum; set => scienceTablesMaximum = value; }
        public int ScienceCabinetsMaximum { get => scienceCabinetsMaximum; set => scienceCabinetsMaximum = value; }
        public int ScienceSinksMaximum { get => scienceSinksMaximum; set => scienceSinksMaximum = value; }
        public int HallwayObjectsMinimum { get => hallwayObjectsMinimum; set => hallwayObjectsMinimum = value; }
        public int HallwayObjectsMaximum { get => hallwayObjectsMaximum; set => hallwayObjectsMaximum = value; }
        public int SpecialRoomExtraObjectsMaximum { get => specialRoomExtraObjectsMaximum; set => specialRoomExtraObjectsMaximum = value; }
        public SchoolRoomPatternSettings[] RoomPatterns => roomPatterns;
        public int DoorClearance => doorClearance;
        public int FurnitureSpacing => furnitureSpacing;
        public int SpawnClearance => spawnClearance;
        public int CenterClearance => centerClearance;
        public int HallwaySpacing => hallwaySpacing;
        public bool PreventNewDeadEnds => preventNewDeadEnds;
        public int SparseRoomArea => sparseRoomArea;
        public int FullRoomArea => fullRoomArea;
        public float CountVariation => countVariation;
        public float OptionalFurnitureChance => optionalFurnitureChance;
        public int RoomLayoutAttempts => roomLayoutAttempts;
        public int CandidateChecksPerObject => candidateChecksPerObject;

        public SchoolFurnitureSettings Copy()
        {
            var copy = (SchoolFurnitureSettings)MemberwiseClone();
            copy.roomPatterns = roomPatterns == null ? null : Array.ConvertAll(roomPatterns, profile => profile?.Copy());
            copy.libraryShelvesMinimum = Math.Clamp(libraryShelvesMinimum, 2, 16);
            copy.libraryShelvesMaximum = Math.Clamp(libraryShelvesMaximum, copy.libraryShelvesMinimum, 16);
            copy.libraryTablesMaximum = Math.Clamp(libraryTablesMaximum, 0, 8);
            copy.libraryCartsMaximum = Math.Clamp(libraryCartsMaximum, 0, 1);
            copy.computerDesksMinimum = Math.Clamp(computerDesksMinimum, 2, 16);
            copy.computerDesksMaximum = Math.Clamp(computerDesksMaximum, copy.computerDesksMinimum, 16);
            copy.printersMaximum = Math.Clamp(printersMaximum, 0, 1);
            copy.classroomDesksMinimum = Math.Clamp(classroomDesksMinimum, 3, 16);
            copy.classroomDesksMaximum = Math.Clamp(classroomDesksMaximum, copy.classroomDesksMinimum, 16);
            copy.teacherDesksMaximum = Math.Clamp(teacherDesksMaximum, 0, 1);
            copy.whiteboardCount = Math.Clamp(whiteboardCount, 1, 2);
            copy.scienceTablesMinimum = Math.Clamp(scienceTablesMinimum, 2, 16);
            copy.scienceTablesMaximum = Math.Clamp(scienceTablesMaximum, copy.scienceTablesMinimum, 16);
            copy.scienceCabinetsMaximum = Math.Clamp(scienceCabinetsMaximum, 0, 1);
            copy.scienceSinksMaximum = Math.Clamp(scienceSinksMaximum, 0, 1);
            copy.hallwayObjectsMinimum = Math.Clamp(hallwayObjectsMinimum, 1, 2);
            copy.hallwayObjectsMaximum = Math.Clamp(hallwayObjectsMaximum, copy.hallwayObjectsMinimum, 2);
            copy.specialRoomExtraObjectsMaximum = Math.Clamp(specialRoomExtraObjectsMaximum, 0, 1);
            return copy;
        }

        public bool IsValid => libraryShelvesMinimum >= 2 && libraryShelvesMinimum <= libraryShelvesMaximum && libraryShelvesMaximum <= 16
            && libraryTablesMaximum >= 0 && libraryTablesMaximum <= 8 && libraryCartsMaximum >= 0 && libraryCartsMaximum <= 1
            && computerDesksMinimum >= 2 && computerDesksMinimum <= computerDesksMaximum && computerDesksMaximum <= 16
            && printersMaximum >= 0 && printersMaximum <= 1
            && classroomDesksMinimum >= 3 && classroomDesksMinimum <= classroomDesksMaximum && classroomDesksMaximum <= 16
            && teacherDesksMaximum >= 0 && teacherDesksMaximum <= 1
            && whiteboardCount >= 1 && whiteboardCount <= 2
            && scienceTablesMinimum >= 2 && scienceTablesMinimum <= scienceTablesMaximum && scienceTablesMaximum <= 16
            && scienceCabinetsMaximum >= 0 && scienceCabinetsMaximum <= 1
            && scienceSinksMaximum >= 0 && scienceSinksMaximum <= 1
            && hallwayObjectsMinimum >= 1 && hallwayObjectsMinimum <= hallwayObjectsMaximum && hallwayObjectsMaximum <= 2
            && specialRoomExtraObjectsMaximum >= 0 && specialRoomExtraObjectsMaximum <= 1
            && roomPatterns != null && roomPatterns.Length == 4 && ValidPatterns()
            && doorClearance >= 1 && doorClearance <= 4 && furnitureSpacing >= 0 && furnitureSpacing <= 3
            && spawnClearance >= 0 && spawnClearance <= 3 && centerClearance >= 0 && centerClearance <= 3
            && hallwaySpacing >= 1 && hallwaySpacing <= 6 && sparseRoomArea >= 1 && fullRoomArea > sparseRoomArea
            && countVariation >= 0f && countVariation <= 1f && optionalFurnitureChance >= 0f && optionalFurnitureChance <= 1f
            && roomLayoutAttempts >= 1 && roomLayoutAttempts <= 32 && candidateChecksPerObject >= 1 && candidateChecksPerObject <= 625;

        private bool ValidPatterns()
        {
            var rooms = new HashSet<SchoolRoomType>();
            foreach (SchoolRoomPatternSettings profile in roomPatterns)
                if (profile == null || !profile.IsValid || !rooms.Add(profile.Room)) return false;
            return true;
        }

    }

    public enum SchoolFurnitureKind
    {
        Bookshelf, ReadingTable, BookCart, ComputerDesk, Printer,
        StudentDesk, TeacherDesk, Whiteboard, LabTable, Cabinet, LabSink,
        Lockers, Bench, WaterFountain, TrashCan,
        StorageShelf, StorageBoxes, SupplyCart, Crate, MopBucket, CleaningCart, CleaningCabinet
    }

    //Each object uses one tile. Its prefab stays inside that tile's footprint.
    public sealed class SchoolFurniturePlacement
    {
        public int Cell { get; }
        public SchoolFurnitureKind Kind { get; }
        public ObstacleFacing Facing { get; }
        public SchoolFurniturePlacement(int cell, SchoolFurnitureKind kind, ObstacleFacing facing = ObstacleFacing.South)
        { Cell = cell; Kind = kind; Facing = facing; }
    }

    //Makes the same spawn and furniture choices on every computer using the map seed.
    public sealed class SchoolFurniturePlan
    {
        private readonly SchoolMapLayout map;
        private readonly SchoolFurnitureSettings settings;
        private readonly Random random;
        private readonly List<int> spawns = new List<int>();
        private readonly List<SchoolFurniturePlacement> furniture = new List<SchoolFurniturePlacement>();
        private readonly HashSet<int> blocked = new HashSet<int>();
        private readonly List<int> specialItems = new List<int>();
        private readonly List<SchoolRoomArrangement> arrangements = new List<SchoolRoomArrangement>();
        private readonly HashSet<int> roomRoutes = new HashSet<int>();
        private static readonly int[] Dx = { 1, 0, -1, 0 };
        private static readonly int[] Dy = { 0, 1, 0, -1 };
        public IReadOnlyList<int> Spawns => spawns.AsReadOnly();
        public IReadOnlyList<SchoolFurniturePlacement> Furniture => furniture.AsReadOnly();
        public IReadOnlyList<int> SpecialItems => specialItems.AsReadOnly();
        public IReadOnlyList<SchoolRoomArrangement> Arrangements => arrangements.AsReadOnly();
        public IReadOnlyCollection<int> RoomRoutes => roomRoutes;

        //Finish furniture first, then choose a clear start in each shuffled room.
        public SchoolFurniturePlan(SchoolMapLayout layout, SchoolFurnitureSettings furnitureSettings = null)
        {
            map = layout ?? throw new ArgumentNullException(nameof(layout));
            settings = furnitureSettings != null ? furnitureSettings.Copy() : new SchoolFurnitureSettings();
            if (!settings.IsValid) throw new InvalidOperationException("Invalid school furniture settings. Check pattern weights, spacing, density, and placement limits.");
            random = new Random(unchecked(layout.Seed ^ 0x51A43C7));
            foreach (SchoolRoom room in map.Rooms)
            {
                ReserveRoomRoutes(room);
                FurnishRoom(room);
            }
            PlaceSpecialRooms();
            var hallways = new List<int>();
            for (int y = 1; y < SchoolMapLayout.Size - 1; y++)
                for (int x = 1; x < SchoolMapLayout.Size - 1; x++)
                    if (map.IsHallway(x, y) && ClearOfDoors(x, y) && BesideWall(x, y)) hallways.Add(y * SchoolMapLayout.Size + x);
            Place(hallways, Pool(SchoolRoomType.None), random.Next(settings.HallwayObjectsMinimum, settings.HallwayObjectsMaximum + 1));
            var rooms = new List<SchoolRoom>(map.Rooms);
            Shuffle(rooms);
            foreach (SchoolRoom room in rooms)
            {
                List<int> cells = SpawnCells(room.Type);
                if (cells.Count == 0) throw new InvalidOperationException("A school room has no safe player start.");
                spawns.Add(cells[random.Next(cells.Count)]);
            }
            FaceFurniture();
            if (!AllRemainingFloorConnected() || !AllFurnitureAccessible())
                throw new InvalidOperationException("The furnished school has an inaccessible walking route or container.");
        }

        private void ReserveRoomRoutes(SchoolRoom room)
        {
            int start = room.CenterY * SchoolMapLayout.Size + room.CenterX;
            var parent = new Dictionary<int, int> { [start] = start };
            var pending = new Queue<int>();
            pending.Enqueue(start);
            while (pending.Count > 0)
            {
                int cell = pending.Dequeue();
                for (int d = 0; d < Dx.Length; d++)
                {
                    int x = cell % SchoolMapLayout.Size + Dx[d], y = cell / SchoolMapLayout.Size + Dy[d];
                    int next = y * SchoolMapLayout.Size + x;
                    if (map.GetRoomType(x, y) != room.Type || parent.ContainsKey(next)) continue;
                    parent[next] = cell; pending.Enqueue(next);
                }
            }
            foreach (int cell in parent.Keys)
                for (int d = 0; d < Dx.Length; d++)
                    if (map.IsDoorFacing(cell % SchoolMapLayout.Size - Dx[d], cell / SchoolMapLayout.Size - Dy[d], Dx[d], Dy[d]))
                    {
                        int cursor = cell;
                        while (parent[cursor] != cursor) { roomRoutes.Add(cursor); cursor = parent[cursor]; }
                        roomRoutes.Add(start);
                    }
        }

        private void FurnishRoom(SchoolRoom room)
        {
            SchoolRoomPatternSettings profile = Array.Find(settings.RoomPatterns, value => value.Room == room.Type);
            var cells = RoomCells(room.Type);
            int area = room.Width * room.Height - room.CutWidth * room.CutHeight;
            int savedCount = furniture.Count;
            for (int attempt = 0; attempt < settings.RoomLayoutAttempts; attempt++)
            {
                SchoolFurniturePattern pattern = ChoosePattern(profile, room);
                ObstacleFacing front = profile.RandomizeOrientation ? (ObstacleFacing)random.Next(4) : profile.FrontWall;
                int phase = random.Next(profile.RowSpacing);
                var addedAisle = new List<int>();
                if (pattern == SchoolFurniturePattern.CenterAisle)
                {
                    bool acrossX = front == ObstacleFacing.North || front == ObstacleFacing.South;
                    int start = ((acrossX ? room.Width : room.Height) - profile.AisleWidth) / 2;
                    foreach (int cell in cells)
                    {
                        int coordinate = acrossX ? cell % SchoolMapLayout.Size - room.X : cell / SchoolMapLayout.Size - room.Y;
                        if (coordinate >= start && coordinate < start + profile.AisleWidth && roomRoutes.Add(cell)) addedAisle.Add(cell);
                    }
                }
                float Score(int cell, SchoolFurnitureKind kind) => PlacementScore(room, profile, pattern, front, phase, cell, kind);
                bool RequiredFurniture(SchoolFurnitureKind kind, int minimum, int maximum)
                {
                    List<int> choices = kind == SchoolFurnitureKind.Whiteboard ? cells.FindAll(cell => AdjacentWalls(cell) > 0) : cells;
                    return Place(choices, new[] { kind }, DensityCount(area, minimum, maximum), cell => Score(cell, kind)) >= minimum;
                }
                void OptionalFurniture(SchoolFurnitureKind kind, int maximum)
                {
                    if (maximum == 0 || random.NextDouble() >= settings.OptionalFurnitureChance) return;
                    Place(cells, new[] { kind }, Math.Max(1, DensityCount(area, 0, maximum)), cell => Score(cell, kind));
                }
                bool valid = false;
                switch (room.Type)
                {
                    case SchoolRoomType.Library:
                        valid = RequiredFurniture(SchoolFurnitureKind.Bookshelf, settings.LibraryShelvesMinimum, settings.LibraryShelvesMaximum);
                        OptionalFurniture(SchoolFurnitureKind.ReadingTable, settings.LibraryTablesMaximum);
                        OptionalFurniture(SchoolFurnitureKind.BookCart, settings.LibraryCartsMaximum);
                        break;
                    case SchoolRoomType.ComputerLab:
                        valid = RequiredFurniture(SchoolFurnitureKind.ComputerDesk, settings.ComputerDesksMinimum, settings.ComputerDesksMaximum);
                        OptionalFurniture(SchoolFurnitureKind.Printer, settings.PrintersMaximum);
                        break;
                    case SchoolRoomType.Classroom:
                        valid = RequiredFurniture(SchoolFurnitureKind.Whiteboard, settings.WhiteboardCount, settings.WhiteboardCount)
                            && RequiredFurniture(SchoolFurnitureKind.StudentDesk, settings.ClassroomDesksMinimum, settings.ClassroomDesksMaximum);
                        OptionalFurniture(SchoolFurnitureKind.TeacherDesk, settings.TeacherDesksMaximum);
                        break;
                    case SchoolRoomType.ScienceLab:
                        valid = RequiredFurniture(SchoolFurnitureKind.LabTable, settings.ScienceTablesMinimum, settings.ScienceTablesMaximum);
                        OptionalFurniture(SchoolFurnitureKind.Cabinet, settings.ScienceCabinetsMaximum);
                        OptionalFurniture(SchoolFurnitureKind.LabSink, settings.ScienceSinksMaximum);
                        break;
                }
                if (valid)
                { arrangements.Add(new SchoolRoomArrangement(room.Type, pattern, front)); return; }
                for (int i = savedCount; i < furniture.Count; i++) blocked.Remove(furniture[i].Cell);
                furniture.RemoveRange(savedCount, furniture.Count - savedCount);
                foreach (int cell in addedAisle) roomRoutes.Remove(cell);
            }
            throw new InvalidOperationException("Cannot fit the required " + room.Type + " furniture safely. Reduce counts or spacing, or enable another layout pattern.");
        }

        private SchoolFurniturePattern ChoosePattern(SchoolRoomPatternSettings profile, SchoolRoom room)
        {
            var choices = new List<SchoolFurniturePattern>();
            float total = 0f;
            foreach (SchoolFurniturePattern pattern in Enum.GetValues(typeof(SchoolFurniturePattern)))
            {
                if (pattern == SchoolFurniturePattern.Groups && Math.Min(room.Width, room.Height) < profile.GroupSpacing + 2) continue;
                if (pattern == SchoolFurniturePattern.CenterAisle && Math.Min(room.Width, room.Height) < profile.AisleWidth + 4) continue;
                if (profile.Weight(pattern) <= 0f) continue;
                choices.Add(pattern); total += profile.Weight(pattern);
            }
            if (choices.Count == 0) throw new InvalidOperationException("No enabled furniture pattern fits " + room.Type + ".");
            double roll = random.NextDouble() * total;
            foreach (SchoolFurniturePattern pattern in choices)
            { roll -= profile.Weight(pattern); if (roll < 0) return pattern; }
            return choices[choices.Count - 1];
        }

        private int DensityCount(int area, int minimum, int maximum)
        {
            if (minimum == maximum) return minimum;
            double density = (area - settings.SparseRoomArea) / (double)(settings.FullRoomArea - settings.SparseRoomArea);
            density += (random.NextDouble() * 2 - 1) * settings.CountVariation;
            return minimum + (int)Math.Round((maximum - minimum) * Math.Max(0, Math.Min(1, density)));
        }

        private float PlacementScore(SchoolRoom room, SchoolRoomPatternSettings profile, SchoolFurniturePattern pattern,
            ObstacleFacing front, int phase, int cell, SchoolFurnitureKind kind)
        {
            int x = cell % SchoolMapLayout.Size, y = cell / SchoolMapLayout.Size;
            ObstacleOrientation.GetStep(front, out int fx, out int fy);
            int u = fx == 0 ? x - room.X : y - room.Y, v = fx == 0 ? y - room.Y : x - room.X;
            float centerU = ((fx == 0 ? room.Width : room.Height) - 1) * .5f;
            float centerV = ((fx == 0 ? room.Height : room.Width) - 1) * .5f;
            int WallDistance(int dx, int dy)
            {
                int distance = 0;
                while (map.GetRoomType(x + dx * (distance + 1), y + dy * (distance + 1)) == room.Type) distance++;
                return distance;
            }
            int wall = Math.Min(Math.Min(WallDistance(1, 0), WallDistance(-1, 0)), Math.Min(WallDistance(0, 1), WallDistance(0, -1)));
            float center = Math.Abs(u - centerU) + Math.Abs(v - centerV);
            if (kind == SchoolFurnitureKind.Whiteboard || kind == SchoolFurnitureKind.TeacherDesk)
                return WallDistance(fx, fy) * profile.WallPreference + Math.Abs(u - centerU);
            if (kind == SchoolFurnitureKind.Printer || kind == SchoolFurnitureKind.Cabinet || kind == SchoolFurnitureKind.LabSink)
                return wall * profile.WallPreference + center * .1f;
            if (kind == SchoolFurnitureKind.BookCart)
            {
                int nearest = SchoolMapLayout.Size;
                foreach (SchoolFurniturePlacement other in furniture)
                    if (other.Kind == SchoolFurnitureKind.Bookshelf) nearest = Math.Min(nearest, Distance(cell, other.Cell));
                return nearest + wall;
            }
            if (kind == SchoolFurnitureKind.ReadingTable) return center + wall * .1f;
            float alignment = ((u + phase) % profile.RowSpacing + (v + phase) % profile.RowSpacing) * 8f;
            if (pattern == SchoolFurniturePattern.Perimeter) return wall * profile.WallPreference + alignment * .25f;
            if (pattern == SchoolFurniturePattern.Groups)
                return (Math.Abs(Math.Abs(u - centerU) - profile.GroupSpacing * .5f)
                    + Math.Abs(Math.Abs(v - centerV) - profile.GroupSpacing * .5f)) * 4f + alignment * .25f;
            if (pattern == SchoolFurniturePattern.CenterAisle)
                return Math.Abs(Math.Abs(u - centerU) - (profile.AisleWidth * .5f + 1f)) * 4f + alignment + center * .1f;
            return alignment + center * .1f;
        }

        //Turn obstacles after placement, without changing tiles, spawns, or random choices.
        private void FaceFurniture()
        {
            int whiteboard = -1;
            foreach (SchoolFurniturePlacement item in furniture)
                if (item.Kind == SchoolFurnitureKind.Whiteboard) whiteboard = item.Cell;
            for (int i = 0; i < furniture.Count; i++)
            {
                SchoolFurniturePlacement item = furniture[i];
                int target = item.Kind == SchoolFurnitureKind.StudentDesk ? whiteboard : -1;
                ObstacleFacing facing = ObstacleOrientation.Choose(map, item.Cell, blocked, target);
                furniture[i] = new SchoolFurniturePlacement(item.Cell, item.Kind, facing);
            }
        }

        //Small rooms get one or two matching obstacles, away from their entrance.
        private void PlaceSpecialRooms()
        {
            foreach (SchoolSecondarySpace space in map.SecondarySpaces)
            {
                SchoolFurnitureKind[] pool = SpecialPool(space.Type);
                if (pool.Length == 0) continue;
                var cells = new List<int>();
                var corners = new List<int>();
                for (int y = space.Y; y < space.Y + space.Height; y++)
                    for (int x = space.X; x < space.X + space.Width; x++)
                    {
                        int cell = y * SchoolMapLayout.Size + x;
                        cells.Add(cell);
                        bool entrance = false;
                        for (int i = 0; i < 4; i++)
                            if (map.GetTile(x + Dx[i], y + Dy[i]) == SchoolTileType.Floor
                                && map.GetSpaceType(x + Dx[i], y + Dy[i]) != space.Type) entrance = true;
                        if (!entrance && ClearOfDoors(x, y)) corners.Add(cell);
                    }
                //Start with an object that shows the room's purpose without a floor label.
                if (Place(corners, space.Type == SchoolSpaceType.Storage ? pool : new[] { pool[0] }, 1,
                    cell => -AdjacentWalls(cell)) != 1)
                    throw new InvalidOperationException("A special room cannot fit its identifying furniture safely.");
                Place(corners, pool, random.Next(settings.SpecialRoomExtraObjectsMaximum + 1), cell => -AdjacentWalls(cell));
                //Keep one tile clear beside the obstacle that will hold the room's item.
                cells.RemoveAll(cell => blocked.Contains(cell));
                if (cells.Count == 0) throw new InvalidOperationException("A special room has no space for its item.");
                specialItems.Add(cells[random.Next(cells.Count)]);
            }
        }

        //Reuse the obstacle prefabs that fit each small room. The first kind identifies it.
        public static SchoolFurnitureKind[] SpecialPool(SchoolSpaceType type)
        {
            if (type == SchoolSpaceType.Bathroom) return new[] { SchoolFurnitureKind.LabSink, SchoolFurnitureKind.TrashCan };
            if (type == SchoolSpaceType.Storage) return new[] { SchoolFurnitureKind.StorageShelf, SchoolFurnitureKind.StorageBoxes,
                SchoolFurnitureKind.Cabinet, SchoolFurnitureKind.SupplyCart, SchoolFurnitureKind.Crate };
            if (type == SchoolSpaceType.JanitorCloset) return new[] { SchoolFurnitureKind.MopBucket, SchoolFurnitureKind.CleaningCart,
                SchoolFurnitureKind.StorageShelf, SchoolFurnitureKind.TrashCan, SchoolFurnitureKind.CleaningCabinet };
            if (type == SchoolSpaceType.StudyArea) return new[] { SchoolFurnitureKind.ReadingTable, SchoolFurnitureKind.Bookshelf, SchoolFurnitureKind.Bench };
            if (type == SchoolSpaceType.Office) return new[] { SchoolFurnitureKind.TeacherDesk, SchoolFurnitureKind.Cabinet, SchoolFurnitureKind.Printer };
            if (type == SchoolSpaceType.LockerArea) return new[] { SchoolFurnitureKind.Lockers, SchoolFurnitureKind.Bench };
            return new SchoolFurnitureKind[0];
        }

        //Leaves a clear tile around players and keeps starts away from doorways.
        private List<int> SpawnCells(SchoolRoomType type)
        {
            List<int> cells = RoomCells(type);
            cells.RemoveAll(cell => { foreach (int other in blocked) if (Distance(cell, other) <= settings.SpawnClearance) return true; return false; });
            return cells;
        }

        //Decorations stay beside walls instead of filling open hallway intersections.
        private bool BesideWall(int x, int y)
        {
            for (int i = 0; i < 4; i++)
                if (map.GetTile(x + Dx[i], y + Dy[i]) == SchoolTileType.Wall) return true;
            return false;
        }

        //Keeps each room's choices separate from every other room and the hallway.
        public static SchoolFurnitureKind[] Pool(SchoolRoomType type)
        {
            switch (type)
            {
                case SchoolRoomType.Library: return new[] { SchoolFurnitureKind.Bookshelf, SchoolFurnitureKind.ReadingTable, SchoolFurnitureKind.BookCart };
                case SchoolRoomType.ComputerLab: return new[] { SchoolFurnitureKind.ComputerDesk, SchoolFurnitureKind.Printer };
                case SchoolRoomType.Classroom: return new[] { SchoolFurnitureKind.StudentDesk, SchoolFurnitureKind.TeacherDesk, SchoolFurnitureKind.Whiteboard };
                case SchoolRoomType.ScienceLab: return new[] { SchoolFurnitureKind.LabTable, SchoolFurnitureKind.Cabinet, SchoolFurnitureKind.LabSink };
                default: return new[] { SchoolFurnitureKind.Lockers, SchoolFurnitureKind.Bench, SchoolFurnitureKind.WaterFountain, SchoolFurnitureKind.TrashCan };
            }
        }

        //Uses the room's real floor shape, including L-shaped rooms, and avoids door approaches.
        private List<int> RoomCells(SchoolRoomType type)
        {
            var cells = new List<int>();
            for (int y = 1; y < SchoolMapLayout.Size - 1; y++)
                for (int x = 1; x < SchoolMapLayout.Size - 1; x++)
                    if (map.GetRoomType(x, y) == type && ClearOfDoors(x, y)) cells.Add(y * SchoolMapLayout.Size + x);
            return cells;
        }

        //Leaves a two-step gap around doorways for turning and entering rooms.
        private bool ClearOfDoors(int x, int y)
        {
            for (int row = y - settings.DoorClearance; row <= y + settings.DoorClearance; row++)
                for (int column = x - settings.DoorClearance; column <= x + settings.DoorClearance; column++)
                {
                    if (Math.Abs(column - x) + Math.Abs(row - y) > settings.DoorClearance) continue;
                    for (int i = 0; i < 4; i++)
                        if (map.IsDoorFacing(column, row, Dx[i], Dy[i])) return false;
                }
            return true;
        }

        //Seeded tie breaking varies the chosen pattern without scattering furniture arbitrarily.
        private int Place(List<int> cells, SchoolFurnitureKind[] pool, int count, Func<int, float> score = null)
        {
            int placed = 0;
            if (count <= 0) return placed;
            Shuffle(cells);
            if (score != null)
            {
                var priorities = new Dictionary<int, float>();
                for (int i = 0; i < cells.Count; i++) priorities[cells[i]] = score(cells[i]) + i / (float)(cells.Count + 1) * .01f;
                cells.Sort((a, b) => priorities[a].CompareTo(priorities[b]));
            }
            int attempts = 0;
            foreach (int cell in cells)
            {
                if (count == 0 || attempts++ >= settings.CandidateChecksPerObject) break;
                if (specialItems.Contains(cell) || roomRoutes.Contains(cell) || blocked.Contains(cell)) continue;
                bool clear = true;
                foreach (SchoolRoom room in map.Rooms)
                    if (Distance(cell, room.CenterY * SchoolMapLayout.Size + room.CenterX) <= settings.CenterClearance) clear = false;
                bool hallway = map.IsHallway(cell % SchoolMapLayout.Size, cell / SchoolMapLayout.Size);
                foreach (int other in blocked) if (Distance(cell, other) <= (hallway ? settings.HallwaySpacing : settings.FurnitureSpacing)) clear = false;
                if (!clear) continue;
                blocked.Add(cell);
                if (!AllRemainingFloorConnected() || !AllFurnitureAccessible(cell)
                    || (settings.PreventNewDeadEnds && CreatesDeadEnd(cell))) { blocked.Remove(cell); continue; }
                SchoolRoomType type = map.GetRoomType(cell % SchoolMapLayout.Size, cell / SchoolMapLayout.Size);
                if (type != SchoolRoomType.None && SpawnCells(type).Count == 0) { blocked.Remove(cell); continue; }
                furniture.Add(new SchoolFurniturePlacement(cell, pool[random.Next(pool.Length)]));
                placed++;
                count--;
                attempts = 0;
            }
            return placed;
        }

        private int AdjacentWalls(int cell)
        {
            int count = 0;
            for (int d = 0; d < Dx.Length; d++)
                if (map.GetTile(cell % SchoolMapLayout.Size + Dx[d], cell / SchoolMapLayout.Size + Dy[d]) == SchoolTileType.Wall) count++;
            return count;
        }

        private bool AllFurnitureAccessible(int candidate = -1)
        {
            bool Accessible(int cell)
            {
                int x = cell % SchoolMapLayout.Size, y = cell / SchoolMapLayout.Size;
                for (int d = 0; d < Dx.Length; d++)
                {
                    int xx = x + Dx[d], yy = y + Dy[d];
                    if (map.GetTile(xx, yy) == SchoolTileType.Floor && !blocked.Contains(yy * SchoolMapLayout.Size + xx)
                        && map.GetRoomType(xx, yy) == map.GetRoomType(x, y) && map.GetSpaceType(xx, yy) == map.GetSpaceType(x, y)) return true;
                }
                return false;
            }
            if (candidate >= 0 && !Accessible(candidate)) return false;
            foreach (SchoolFurniturePlacement item in furniture) if (!Accessible(item.Cell)) return false;
            return true;
        }

        private bool CreatesDeadEnd(int candidate)
        {
            for (int d = 0; d < Dx.Length; d++)
            {
                int x = candidate % SchoolMapLayout.Size + Dx[d], y = candidate / SchoolMapLayout.Size + Dy[d];
                if (map.GetTile(x, y) != SchoolTileType.Floor || blocked.Contains(y * SchoolMapLayout.Size + x)
                    || map.GetSpaceType(x, y) != SchoolSpaceType.None) continue;
                int exits = 0;
                for (int step = 0; step < Dx.Length; step++)
                    if (map.GetTile(x + Dx[step], y + Dy[step]) == SchoolTileType.Floor
                        && !blocked.Contains((y + Dy[step]) * SchoolMapLayout.Size + x + Dx[step])) exits++;
                if (exits == 1) return true;
            }
            return false;
        }

        //Tests all remaining floor, not just the four room centers.
        private bool AllRemainingFloorConnected()
        {
            int start = map.Rooms[0].CenterY * SchoolMapLayout.Size + map.Rooms[0].CenterX;
            var seen = new HashSet<int> { start };
            var pending = new Queue<int>();
            pending.Enqueue(start);
            while (pending.Count > 0)
            {
                int cell = pending.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int x = cell % SchoolMapLayout.Size + Dx[i], y = cell / SchoolMapLayout.Size + Dy[i];
                    int next = y * SchoolMapLayout.Size + x;
                    if (map.GetTile(x, y) == SchoolTileType.Floor && !blocked.Contains(next) && seen.Add(next)) pending.Enqueue(next);
                }
            }
            for (int y = 1; y < SchoolMapLayout.Size - 1; y++)
                for (int x = 1; x < SchoolMapLayout.Size - 1; x++)
                {
                    int cell = y * SchoolMapLayout.Size + x;
                    if (map.GetTile(x, y) == SchoolTileType.Floor && !blocked.Contains(cell) && !seen.Contains(cell)) return false;
                }
            return true;
        }

        //Measures tile spacing without depending on Unity objects.
        private static int Distance(int a, int b) => Math.Abs(a % SchoolMapLayout.Size - b % SchoolMapLayout.Size)
            + Math.Abs(a / SchoolMapLayout.Size - b / SchoolMapLayout.Size);

        //A shuffled list avoids always trying the same corner of a room first.
        private void Shuffle<T>(List<T> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                T saved = values[i]; values[i] = values[other]; values[other] = saved;
            }
        }
    }
}
