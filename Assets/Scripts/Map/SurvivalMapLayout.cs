using System;
using System.Collections.Generic;

namespace Escape4Now.Map
{
    public sealed class OutdoorArea
    {
        public OutdoorAreaKind Kind { get; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public bool Fenced { get; }
        public bool IsPocket { get; internal set; }
        public OutdoorCourtSport CourtSport { get; internal set; }
        public int WaterX { get; internal set; }
        public int WaterY { get; internal set; }
        public int WaterWidth { get; internal set; }
        public int WaterHeight { get; internal set; }
        public int AnchorCell { get; internal set; }
        internal readonly List<int> gates = new List<int>();
        public IReadOnlyList<int> Entrances => gates.AsReadOnly();
        public bool Contains(int x, int y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
        internal OutdoorArea(OutdoorAreaKind kind, int x, int y, int width, int height, bool fenced)
        { Kind = kind; X = x; Y = y; Width = width; Height = height; Fenced = fenced; }
    }

    public sealed class OutdoorPropPlacement
    {
        public OutdoorPropKind Kind { get; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public bool BlocksMovement { get; }
        public int QuarterTurns { get; }
        internal OutdoorPropPlacement(OutdoorPropKind kind, int x, int y, int width, int height, bool blocksMovement, int quarterTurns)
        { Kind = kind; X = x; Y = y; Width = width; Height = height; BlocksMovement = blocksMovement; QuarterTurns = quarterTurns; }
    }

    public sealed class OutdoorParkingBay
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public OutdoorArea Area { get; }
        internal OutdoorParkingBay(int x, int y, int width, int height, OutdoorArea area)
        { X = x; Y = y; Width = width; Height = height; Area = area; }
    }

    //Pure grid data shared by rendering, spawn selection, and future monster navigation.
    public sealed class SurvivalMapLayout
    {
        private const int Bands = 3;
        private static readonly int[] StepX = { 1, 0, -1, 0 }, StepY = { 0, 1, 0, -1 };
        private readonly SurvivalGenerationSettings settings;
        private readonly SurvivalObstacleSettings obstacleSettings;
        private readonly Random random;
        private readonly OutdoorSurface[] surface;
        private readonly OutdoorAreaKind[] areaTypes;
        private readonly bool[] blocked, protectedPath, mainPath, occupied, parkingCells;
        private readonly List<OutdoorParkingBay> parkingBays = new List<OutdoorParkingBay>();
        private readonly List<OutdoorArea> areas = new List<OutdoorArea>();
        private readonly List<OutdoorPropPlacement> props = new List<OutdoorPropPlacement>();
        private readonly List<int> spawns = new List<int>(), spawnCandidates = new List<int>();
        public int Width { get; }
        public int Height { get; }
        public int Seed { get; }
        public int AttemptsUsed { get; }
        public IReadOnlyList<OutdoorArea> Areas => areas.AsReadOnly();
        public IReadOnlyList<OutdoorPropPlacement> Props => props.AsReadOnly();
        public IReadOnlyList<int> PlayerSpawns => spawns.AsReadOnly();
        public IReadOnlyList<int> SpawnCandidates => spawnCandidates.AsReadOnly();
        public IReadOnlyList<OutdoorParkingBay> ParkingBays => parkingBays.AsReadOnly();

        public SurvivalMapLayout(int seed, SurvivalGenerationSettings generationSettings = null, SurvivalObstacleSettings obstacles = null)
        {
            settings = generationSettings ?? new SurvivalGenerationSettings();
            settings.Validate();
            obstacleSettings = obstacles ?? new SurvivalObstacleSettings();
            obstacleSettings.Validate();
            Width = settings.Width; Height = settings.Height; Seed = seed;
            random = new Random(seed);
            int count = Width * Height;
            surface = new OutdoorSurface[count]; areaTypes = new OutdoorAreaKind[count];
            blocked = new bool[count]; protectedPath = new bool[count]; mainPath = new bool[count]; occupied = new bool[count];
            parkingCells = new bool[count];
            for (int attempt = 1; attempt <= settings.GenerationAttempts; attempt++)
            {
                AttemptsUsed = attempt;
                if (TryBuild() && Validate(out _)) return;
            }
            throw new InvalidOperationException($"Survival generation failed validation after {settings.GenerationAttempts} attempts (seed {seed}). Check area sizes, object counts, paths, and spawn separation.");
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool IsWalkable(int x, int y) => InBounds(x, y) && !blocked[y * Width + x];
        public OutdoorSurface GetSurface(int x, int y) => InBounds(x, y) ? surface[y * Width + x] : OutdoorSurface.Fence;
        public OutdoorAreaKind GetArea(int x, int y) => InBounds(x, y) ? areaTypes[y * Width + x] : OutdoorAreaKind.None;
        public bool IsMainPath(int x, int y) => InBounds(x, y) && mainPath[y * Width + x];

        public IEnumerable<int> GetNeighbours(int cell)
        {
            if (cell < 0 || cell >= Width * Height || !IsWalkable(cell % Width, cell / Width)) yield break;
            for (int d = 0; d < StepX.Length; d++)
            {
                int x = cell % Width + StepX[d], y = cell / Width + StepY[d];
                if (IsWalkable(x, y)) yield return y * Width + x;
            }
        }

        private bool TryBuild()
        {
            Array.Clear(surface, 0, surface.Length); Array.Clear(areaTypes, 0, areaTypes.Length);
            Array.Clear(blocked, 0, blocked.Length); Array.Clear(protectedPath, 0, protectedPath.Length);
            Array.Clear(mainPath, 0, mainPath.Length); Array.Clear(occupied, 0, occupied.Length);
            Array.Clear(parkingCells, 0, parkingCells.Length);
            areas.Clear(); props.Clear(); spawns.Clear(); spawnCandidates.Clear(); parkingBays.Clear();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (x == 0 || y == 0 || x == Width - 1 || y == Height - 1)
                { surface[y * Width + x] = OutdoorSurface.Fence; blocked[y * Width + x] = true; }

            int[] widths = Partition(Width), heights = Partition(Height);
            if (widths == null || heights == null) return false;
            var slots = new Rect[Bands * Bands];
            int yy = 1 + settings.PerimeterPathWidth;
            for (int row = 0; row < Bands; row++)
            {
                int xx = 1 + settings.PerimeterPathWidth;
                for (int column = 0; column < Bands; column++)
                { slots[row * Bands + column] = new Rect(xx, yy, widths[column], heights[row]); xx += widths[column] + settings.PathWidth; }
                yy += heights[row] + settings.PathWidth;
            }
            for (int y = 1; y < Height - 1; y++)
            for (int x = 1; x < Width - 1; x++)
            {
                bool insideSlot = false;
                foreach (Rect slot in slots) if (slot.Contains(x, y)) { insideSlot = true; break; }
                if (!insideSlot)
                { int cell = y * Width + x; surface[cell] = OutdoorSurface.Path; protectedPath[cell] = mainPath[cell] = true; }
            }

            OutdoorAreaRule field = Array.Find(settings.Areas, rule => rule.Area == OutdoorAreaKind.TrackField);
            var pairs = new List<(int first, int second, bool vertical)>();
            for (int i = 0; i < slots.Length; i++)
            {
                if (i % Bands < Bands - 1 && CanFit(field, Merge(slots[i], slots[i + 1]), false)) pairs.Add((i, i + 1, false));
                if (i / Bands < Bands - 1 && CanFit(field, Merge(slots[i], slots[i + Bands]), true)) pairs.Add((i, i + Bands, true));
            }
            if (pairs.Count == 0) return false;
            var pair = pairs[random.Next(pairs.Count)];
            BuildArea(field, Merge(slots[pair.first], slots[pair.second]), pair.vertical);
            var available = new List<int>();
            for (int i = 0; i < slots.Length; i++) if (i != pair.first && i != pair.second) available.Add(i);
            Shuffle(available);
            //Required areas get first choice; optional areas never crowd one out.
            foreach (bool required in new[] { true, false })
            foreach (OutdoorAreaRule rule in settings.Areas)
            {
                if (rule.Area == OutdoorAreaKind.TrackField || rule.Required != required || (!required && random.NextDouble() >= rule.Chance)) continue;
                int chosen = available.FindIndex(i => CanFit(rule, slots[i], false));
                if (chosen < 0) { if (required) return false; continue; }
                BuildArea(rule, slots[available[chosen]], false);
                available.RemoveAt(chosen);
            }
            foreach (int unused in available)
                if (random.NextDouble() < settings.CourtyardPocket.Chance && CanFit(settings.CourtyardPocket, slots[unused], false))
                {
                    BuildArea(settings.CourtyardPocket, slots[unused], false);
                    areas[areas.Count - 1].IsPocket = true;
                }
            if (!IsConnected()) return false;
            foreach (OutdoorPropRule rule in obstacleSettings.Rules)
            {
                if (Array.IndexOf(rule.AllowedAreas, OutdoorAreaKind.None) >= 0 && !PlaceProps(rule, null)) return false;
                foreach (OutdoorArea area in areas)
                    if (Array.IndexOf(rule.AllowedAreas, area.Kind) >= 0
                        && (area.Kind != OutdoorAreaKind.Courts || rule.CourtSport == OutdoorCourtSport.Any || rule.CourtSport == area.CourtSport)
                        && !PlaceProps(rule, area)) return false;
            }
            return ChooseSpawns();
        }

        private int[] Partition(int dimension)
        {
            //Three bands provide nine macro slots; the field joins two adjacent slots.
            int available = dimension - 2 - 2 * settings.PerimeterPathWidth - (Bands - 1) * settings.PathWidth;
            if (available < Bands * 5) return null;
            var values = new[] { available / Bands, available / Bands, available - 2 * (available / Bands) };
            for (int i = 0; i < settings.PartitionJitter * Bands; i++)
            {
                int from = random.Next(Bands), to = random.Next(Bands);
                if (from != to && values[from] > 5) { values[from]--; values[to]++; }
            }
            return values;
        }

        private int MinimumWidth(OutdoorAreaRule rule) => rule.Area == OutdoorAreaKind.Pool
            ? Math.Max(rule.MinimumWidth, settings.MinimumPoolWaterWidth + 2 * (settings.PoolDeckWidth + (rule.Fenced ? 1 : 0)))
            : rule.Area == OutdoorAreaKind.Parking ? Math.Max(rule.MinimumWidth, ParkingMinimum(rule)) : rule.MinimumWidth;
        private int MinimumHeight(OutdoorAreaRule rule) => rule.Area == OutdoorAreaKind.Pool
            ? Math.Max(rule.MinimumHeight, settings.MinimumPoolWaterHeight + 2 * (settings.PoolDeckWidth + (rule.Fenced ? 1 : 0)))
            : rule.Area == OutdoorAreaKind.Parking ? Math.Max(rule.MinimumHeight, ParkingMinimum(rule)) : rule.MinimumHeight;
        private int ParkingMinimum(OutdoorAreaRule rule) => 2 * (settings.AreaWalkwayWidth + (rule.Fenced ? 1 : 0))
            + Math.Max(settings.ParkingBayWidth, settings.ParkingBayDepth + settings.ParkingAisleWidth);
        private bool CanFit(OutdoorAreaRule rule, Rect slot, bool vertical) =>
            slot.Width >= (vertical ? MinimumHeight(rule) : MinimumWidth(rule))
            && slot.Height >= (vertical ? MinimumWidth(rule) : MinimumHeight(rule))
            && rule.MaximumWidth >= MinimumWidth(rule) && rule.MaximumHeight >= MinimumHeight(rule);

        private void BuildArea(OutdoorAreaRule rule, Rect slot, bool vertical)
        {
            int width = random.Next(vertical ? MinimumHeight(rule) : MinimumWidth(rule),
                Math.Min(slot.Width, vertical ? rule.MaximumHeight : rule.MaximumWidth) + 1);
            int height = random.Next(vertical ? MinimumWidth(rule) : MinimumHeight(rule),
                Math.Min(slot.Height, vertical ? rule.MaximumWidth : rule.MaximumHeight) + 1);
            var area = new OutdoorArea(rule.Area, slot.X + random.Next(slot.Width - width + 1),
                slot.Y + random.Next(slot.Height - height + 1), width, height, rule.Fenced);
            areas.Add(area);
            int inset = area.Fenced ? 1 : 0;
            if (area.Kind == OutdoorAreaKind.Pool)
            {
                int waterSpaceX = width - 2 * (inset + settings.PoolDeckWidth);
                int waterSpaceY = height - 2 * (inset + settings.PoolDeckWidth);
                area.WaterWidth = Math.Max(settings.MinimumPoolWaterWidth, (int)Math.Round(waterSpaceX * settings.PoolWaterFill));
                area.WaterHeight = Math.Max(settings.MinimumPoolWaterHeight, (int)Math.Round(waterSpaceY * settings.PoolWaterFill));
                area.WaterX = area.X + inset + settings.PoolDeckWidth + random.Next(waterSpaceX - area.WaterWidth + 1);
                area.WaterY = area.Y + inset + settings.PoolDeckWidth + random.Next(waterSpaceY - area.WaterHeight + 1);
            }
            if (area.Kind == OutdoorAreaKind.Courts)
                area.CourtSport = random.NextDouble() < settings.TennisCourtChance ? OutdoorCourtSport.Tennis : OutdoorCourtSport.Basketball;
            for (int y = area.Y; y < area.Y + height; y++)
            for (int x = area.X; x < area.X + width; x++)
            {
                int cell = y * Width + x;
                areaTypes[cell] = area.Kind; surface[cell] = AreaSurface(area.Kind);
                if (area.CourtSport == OutdoorCourtSport.Tennis) surface[cell] = OutdoorSurface.TennisCourt;
                mainPath[cell] = false; protectedPath[cell] = false;
                bool fence = area.Fenced && (x == area.X || y == area.Y || x == area.X + width - 1 || y == area.Y + height - 1);
                if (fence) { blocked[cell] = true; surface[cell] = OutdoorSurface.Fence; continue; }
                int edge = Math.Min(Math.Min(x - area.X - inset, area.X + width - 1 - inset - x),
                    Math.Min(y - area.Y - inset, area.Y + height - 1 - inset - y));
                if (edge < settings.AreaWalkwayWidth) protectedPath[cell] = true;
                if (area.Kind == OutdoorAreaKind.Pool && x >= area.WaterX && y >= area.WaterY
                    && x < area.WaterX + area.WaterWidth && y < area.WaterY + area.WaterHeight)
                { surface[cell] = OutdoorSurface.Water; blocked[cell] = true; }
                if (area.Kind == OutdoorAreaKind.TrackField)
                {
                    double dx = x + .5 - (area.X + width * .5), dy = y + .5 - (area.Y + height * .5);
                    double outer = dx * dx / (width * width * .25) + dy * dy / (height * height * .25);
                    double rx = Math.Max(.5, width * .5 - settings.TrackWidth), ry = Math.Max(.5, height * .5 - settings.TrackWidth);
                    double inner = dx * dx / (rx * rx) + dy * dy / (ry * ry);
                    surface[cell] = outer > 1 ? OutdoorSurface.Grass : inner >= 1 ? OutdoorSurface.Track : OutdoorSurface.Field;
                    if (surface[cell] == OutdoorSurface.Field) protectedPath[cell] = true;
                }
            }
            int firstSide = random.Next(4);
            for (int entrance = 0; entrance < rule.EntranceCount; entrance++)
            {
                int side = (firstSide + (entrance == 1 ? 2 : entrance == 2 ? 1 : entrance == 3 ? 3 : 0)) % 4;
                bool eastWest = side % 2 == 0;
                int span = eastWest ? height : width;
                int opening = Math.Min(settings.PathWidth, span - 2);
                int offset = random.Next(1, span - opening);
                for (int step = 0; step < opening; step++)
                {
                    int gx = eastWest ? (side == 0 ? area.X + width - 1 : area.X) : area.X + offset + step;
                    int gy = eastWest ? area.Y + offset + step : (side == 1 ? area.Y + height - 1 : area.Y);
                    if (step == 0) area.gates.Add(gy * Width + gx);
                    for (int length = 0; InBounds(gx, gy) && gx > 0 && gy > 0 && gx < Width - 1 && gy < Height - 1; length++)
                    {
                        int cell = gy * Width + gx;
                        bool reachesMainPath = mainPath[cell];
                        blocked[cell] = false; protectedPath[cell] = true;
                        if (surface[cell] == OutdoorSurface.Fence || GetArea(gx, gy) == OutdoorAreaKind.None) surface[cell] = OutdoorSurface.Path;
                        if (length > 0 && reachesMainPath) break;
                        gx += StepX[side]; gy += StepY[side];
                    }
                }
            }
            area.AnchorCell = area.gates[0];
            if (area.Kind == OutdoorAreaKind.Garden) BuildGardenPaths(area);
            if (area.Kind == OutdoorAreaKind.Parking) BuildParkingBays(area);
        }

        private static OutdoorSurface AreaSurface(OutdoorAreaKind area) => area == OutdoorAreaKind.Pool ? OutdoorSurface.Deck
            : area == OutdoorAreaKind.Courts ? OutdoorSurface.Court : area == OutdoorAreaKind.Courtyard ? OutdoorSurface.Paving
            : area == OutdoorAreaKind.Maintenance ? OutdoorSurface.Service : area == OutdoorAreaKind.Parking ? OutdoorSurface.Asphalt
            : OutdoorSurface.Grass;

        private void BuildGardenPaths(OutdoorArea area)
        {
            int cx = Math.Clamp(area.X + area.Width / 2 + random.Next(-settings.GardenPathBend, settings.GardenPathBend + 1),
                area.X + 1, area.X + area.Width - 2);
            int cy = Math.Clamp(area.Y + area.Height / 2 + random.Next(-settings.GardenPathBend, settings.GardenPathBend + 1),
                area.Y + 1, area.Y + area.Height - 2);
            foreach (int gate in area.gates)
            {
                int x = gate % Width, y = gate / Width;
                bool horizontalFirst = random.Next(2) == 0;
                while (x != cx || y != cy)
                {
                    CarveGardenTile(area, x, y);
                    if ((horizontalFirst && x != cx) || y == cy) x += Math.Sign(cx - x);
                    else y += Math.Sign(cy - y);
                }
                CarveGardenTile(area, cx, cy);
            }
        }

        private void CarveGardenTile(OutdoorArea area, int x, int y)
        {
            for (int dy = 0; dy < settings.GardenPathWidth; dy++) for (int dx = 0; dx < settings.GardenPathWidth; dx++)
            {
                int xx = x + dx, yy = y + dy;
                if (!area.Contains(xx, yy) || !IsWalkable(xx, yy)) continue;
                int cell = yy * Width + xx;
                surface[cell] = OutdoorSurface.Path; protectedPath[cell] = true;
            }
        }

        private void BuildParkingBays(OutdoorArea area)
        {
            int margin = (area.Fenced ? 1 : 0) + settings.AreaWalkwayWidth;
            bool alongX = area.Width >= area.Height;
            int cross = (alongX ? area.Height : area.Width) - margin * 2;
            int length = (alongX ? area.Width : area.Height) - margin * 2;
            int depth = settings.ParkingBayDepth;
            if (cross < depth + settings.ParkingAisleWidth || length < 1) return;
            bool twoRows = cross >= depth * 2 + settings.ParkingAisleWidth;
            bool farSide = !twoRows && random.Next(2) == 0;
            int aisleStart = farSide ? cross - depth - settings.ParkingAisleWidth : depth;
            for (int y = area.Y + margin; y < area.Y + area.Height - margin; y++)
            for (int x = area.X + margin; x < area.X + area.Width - margin; x++)
            {
                int coordinate = alongX ? y - area.Y - margin : x - area.X - margin;
                if (coordinate >= aisleStart && coordinate < aisleStart + settings.ParkingAisleWidth)
                    protectedPath[y * Width + x] = true;
            }
            for (int row = 0; row < (twoRows ? 2 : 1); row++)
            for (int position = 0; position + settings.ParkingBayWidth <= length; position += settings.ParkingBayWidth + settings.ParkingBaySpacing)
            {
                int offset = row == 1 || farSide ? cross - depth : 0;
                var bay = new OutdoorParkingBay(area.X + margin + (alongX ? position : offset),
                    area.Y + margin + (alongX ? offset : position), alongX ? settings.ParkingBayWidth : depth,
                    alongX ? depth : settings.ParkingBayWidth, area);
                parkingBays.Add(bay);
                for (int y = bay.Y; y < bay.Y + bay.Height; y++) for (int x = bay.X; x < bay.X + bay.Width; x++)
                    parkingCells[y * Width + x] = true;
            }
        }

        private bool PlaceProps(OutdoorPropRule rule, OutdoorArea area)
        {
            if (random.NextDouble() >= rule.Chance) return true;
            int desired = random.Next(rule.MinimumCount, rule.MaximumCount + 1), placed = 0;
            Rect bounds = area == null ? new Rect(1, 1, Width - 2, Height - 2) : new Rect(area.X, area.Y, area.Width, area.Height);
            int anchor = -1;
            if (rule.Pattern == OutdoorPlacementPattern.Clustered)
            {
                var candidates = new List<int>();
                for (int y = bounds.Y; y < bounds.Y + bounds.Height; y++) for (int x = bounds.X; x < bounds.X + bounds.Width; x++)
                    if (CanPlace(x, y, 1, 1, 0, area?.Kind ?? OutdoorAreaKind.None)) candidates.Add(y * Width + x);
                if (candidates.Count > 0) anchor = candidates[random.Next(candidates.Count)];
            }
            for (int item = 0; item < desired; item++)
            {
                bool success = false;
                for (int attempt = 0; attempt < obstacleSettings.PlacementAttempts; attempt++)
                {
                    if (!ChoosePropPosition(rule, area, bounds, item, anchor, out int x, out int y, out int w, out int h, out int rotation)) continue;
                    if (!CanPlace(x, y, w, h, rule.Spacing, area?.Kind ?? OutdoorAreaKind.None,
                        rule.Pattern == OutdoorPlacementPattern.ParkingBays)) continue;
                    SetFootprint(x, y, w, h, rule.BlocksMovement);
                    if (rule.BlocksMovement && (!IsConnected() || LongestDeadEnd() > settings.MaximumDeadEndLength))
                    { SetFootprint(x, y, w, h, false); continue; }
                    for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) occupied[yy * Width + xx] = true;
                    props.Add(new OutdoorPropPlacement(rule.Prop, x, y, w, h, rule.BlocksMovement, rotation));
                    placed++; success = true; break;
                }
                if (!success) break;
            }
            return placed >= rule.MinimumCount;
        }

        private bool ChoosePropPosition(OutdoorPropRule rule, OutdoorArea area, Rect bounds, int item, int anchor,
            out int x, out int y, out int w, out int h, out int rotation)
        {
            rotation = random.Next(2);
            bool longX = bounds.Width >= bounds.Height;
            if (rule.Pattern == OutdoorPlacementPattern.Ends)
                rotation = (longX ? 0 : 1) + (item % 2 == 0 ? 0 : 2);
            if (rule.Pattern == OutdoorPlacementPattern.Center)
                rotation = longX == (rule.FootprintHeight >= rule.FootprintWidth) ? 0 : 1;
            w = rotation % 2 == 0 ? rule.FootprintWidth : rule.FootprintHeight;
            h = rotation % 2 == 0 ? rule.FootprintHeight : rule.FootprintWidth;
            x = y = 0;
            if (bounds.Width < w || bounds.Height < h) return false;
            if (rule.Pattern == OutdoorPlacementPattern.ParkingBays)
            {
                int longSize = Math.Max(w, h), shortSize = Math.Min(w, h);
                var choices = parkingBays.FindAll(bay => bay.Area == area
                    && Math.Max(bay.Width, bay.Height) >= longSize && Math.Min(bay.Width, bay.Height) >= shortSize);
                if (choices.Count == 0) return false;
                OutdoorParkingBay bay = choices[random.Next(choices.Count)];
                rotation = (bay.Width > bay.Height) == (rule.FootprintWidth > rule.FootprintHeight) ? 0 : 1;
                w = rotation == 0 ? rule.FootprintWidth : rule.FootprintHeight;
                h = rotation == 0 ? rule.FootprintHeight : rule.FootprintWidth;
                x = bay.X + (bay.Width - w) / 2; y = bay.Y + (bay.Height - h) / 2;
                return true;
            }
            x = bounds.X + random.Next(bounds.Width - w + 1); y = bounds.Y + random.Next(bounds.Height - h + 1);
            if (rule.Pattern == OutdoorPlacementPattern.Center)
            { x = bounds.X + (bounds.Width - w) / 2; y = bounds.Y + (bounds.Height - h) / 2; }
            if (rule.Pattern == OutdoorPlacementPattern.Clustered)
            {
                if (anchor < 0) return false;
                x = Math.Clamp(anchor % Width + random.Next(-rule.ClusterRadius, rule.ClusterRadius + 1), bounds.X, bounds.X + bounds.Width - w);
                y = Math.Clamp(anchor / Width + random.Next(-rule.ClusterRadius, rule.ClusterRadius + 1), bounds.Y, bounds.Y + bounds.Height - h);
            }
            int margin = (area?.Fenced == true ? 1 : 0) + settings.AreaWalkwayWidth;
            if (rule.Pattern == OutdoorPlacementPattern.Ends)
            {
                x = longX ? (item % 2 == 0 ? bounds.X + margin : bounds.X + bounds.Width - margin - w) : bounds.X + (bounds.Width - w) / 2;
                y = longX ? bounds.Y + (bounds.Height - h) / 2 : (item % 2 == 0 ? bounds.Y + margin : bounds.Y + bounds.Height - margin - h);
            }
            if (rule.Pattern == OutdoorPlacementPattern.Edges)
            {
                if (area == null)
                {
                    bool nearEdge = false;
                    for (int dy = -rule.EdgeBandWidth; dy <= rule.EdgeBandWidth; dy++)
                    for (int dx = -rule.EdgeBandWidth; dx <= rule.EdgeBandWidth; dx++)
                        if (GetArea(x + dx, y + dy) != OutdoorAreaKind.None || IsMainPath(x + dx, y + dy)) nearEdge = true;
                    return nearEdge;
                }
                int side = random.Next(4), offset = random.Next(rule.EdgeBandWidth);
                if (side % 2 == 0) x = side == 0 ? bounds.X + margin + offset : bounds.X + bounds.Width - margin - w - offset;
                else y = side == 1 ? bounds.Y + margin + offset : bounds.Y + bounds.Height - margin - h - offset;
            }
            return x >= bounds.X && y >= bounds.Y && x + w <= bounds.X + bounds.Width && y + h <= bounds.Y + bounds.Height;
        }

        private bool CanPlace(int x, int y, int w, int h, int spacing, OutdoorAreaKind kind, bool usesParkingBay = false)
        {
            for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++)
                if (!IsWalkable(xx, yy) || protectedPath[yy * Width + xx] || occupied[yy * Width + xx]
                    || (parkingCells[yy * Width + xx] && !usesParkingBay) || GetArea(xx, yy) != kind) return false;
            for (int yy = y - spacing; yy < y + h + spacing; yy++) for (int xx = x - spacing; xx < x + w + spacing; xx++)
                if (InBounds(xx, yy) && occupied[yy * Width + xx]) return false;
            return true;
        }

        private void SetFootprint(int x, int y, int w, int h, bool value)
        { for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) blocked[yy * Width + xx] = value; }

        private bool ChooseSpawns()
        {
            for (int y = 1; y < Height - 1; y++) for (int x = 1; x < Width - 1; x++)
            {
                bool safe = IsWalkable(x, y) && !occupied[y * Width + x];
                for (int dy = -settings.SpawnClearance; safe && dy <= settings.SpawnClearance; dy++)
                for (int dx = -settings.SpawnClearance; safe && dx <= settings.SpawnClearance; dx++) safe = IsWalkable(x + dx, y + dy);
                if (safe) spawnCandidates.Add(y * Width + x);
            }
            var candidates = new List<int>(spawnCandidates);
            Shuffle(candidates);
            while (spawns.Count < settings.PlayerSpawnCount)
            {
                int best = -1, score = -1;
                foreach (int cell in candidates)
                {
                    if (spawns.Contains(cell)) continue;
                    int separation = Width + Height;
                    foreach (int spawn in spawns) separation = Math.Min(separation, Distance(cell, spawn));
                    if (separation >= settings.MinimumSpawnDistance && separation > score) { score = separation; best = cell; }
                }
                if (best < 0) return false;
                spawns.Add(best);
            }
            return true;
        }

        private int Distance(int a, int b) => Math.Abs(a % Width - b % Width) + Math.Abs(a / Width - b / Width);
        private int Degree(int cell)
        {
            int count = 0;
            for (int d = 0; d < StepX.Length; d++)
                if (IsWalkable(cell % Width + StepX[d], cell / Width + StepY[d])) count++;
            return count;
        }

        public int LongestDeadEnd()
        {
            int longest = 0;
            for (int cell = 0; cell < blocked.Length; cell++)
            {
                if (blocked[cell] || Degree(cell) != 1) continue;
                int previous = -1, current = cell, length = 0;
                do
                {
                    int next = -1;
                    foreach (int neighbour in GetNeighbours(current)) if (neighbour != previous) { next = neighbour; break; }
                    length++; previous = current; current = next;
                } while (current >= 0 && Degree(current) == 2 && length <= blocked.Length);
                longest = Math.Max(longest, length);
            }
            return longest;
        }

        private bool IsConnected()
        {
            int start = Array.FindIndex(blocked, value => !value);
            if (start < 0) return false;
            var seen = new bool[blocked.Length]; var queue = new int[blocked.Length];
            int head = 0, tail = 1; queue[0] = start; seen[start] = true;
            while (head < tail)
            {
                int cell = queue[head++];
                for (int d = 0; d < StepX.Length; d++)
                {
                    int x = cell % Width + StepX[d], y = cell / Width + StepY[d];
                    if (!IsWalkable(x, y)) continue;
                    int next = y * Width + x;
                    if (!seen[next]) { seen[next] = true; queue[tail++] = next; }
                }
            }
            for (int i = 0; i < blocked.Length; i++) if (!blocked[i] && !seen[i]) return false;
            return true;
        }

        public bool Validate(out string reason)
        {
            reason = null;
            if (!IsConnected()) reason = "Walkable tiles are disconnected.";
            else if (LongestDeadEnd() > settings.MaximumDeadEndLength) reason = "A dead end exceeds the configured limit.";
            else if (HasMainPathBottleneck()) reason = "The main route network has a single-tile bottleneck.";
            else if (spawns.Count != settings.PlayerSpawnCount) reason = "Player spawn reservations are missing.";
            OutdoorArea field = areas.Find(area => area.Kind == OutdoorAreaKind.TrackField);
            if (field == null) reason = "The Track / Field is missing.";
            foreach (OutdoorArea area in areas)
            {
                if (area.Entrances.Count < 2 || !IsWalkable(area.AnchorCell % Width, area.AnchorCell / Width)) reason = "An area lacks reachable entrances.";
                if (field != null && area.Width * area.Height > field.Width * field.Height) reason = "Track / Field must remain one of the largest areas.";
                foreach (int gate in area.Entrances) if (blocked[gate]) reason = "An entrance is blocked.";
            }
            for (int i = 0; i < blocked.Length; i++) if (mainPath[i] && blocked[i]) reason = "An object blocked a main path.";
            for (int i = 0; i < spawns.Count; i++)
            {
                if (blocked[spawns[i]]) reason = "A player spawn is blocked.";
                for (int j = 0; j < i; j++) if (Distance(spawns[i], spawns[j]) < settings.MinimumSpawnDistance) reason = "Player spawns are too close.";
            }
            return reason == null;
        }

        //Iterative articulation-point check: future chases must not depend on one main-route tile.
        private bool HasMainPathBottleneck()
        {
            int start = Array.FindIndex(blocked, value => !value);
            if (start < 0) return true;
            var discovery = new int[blocked.Length];
            var low = new int[blocked.Length];
            var parent = new int[blocked.Length];
            var children = new int[blocked.Length];
            var direction = new int[blocked.Length];
            Array.Fill(parent, -1);
            var stack = new Stack<int>();
            int time = 1; discovery[start] = low[start] = time; stack.Push(start);
            while (stack.Count > 0)
            {
                int cell = stack.Peek();
                if (direction[cell] < StepX.Length)
                {
                    int step = direction[cell]++;
                    int x = cell % Width + StepX[step], y = cell / Width + StepY[step];
                    if (!IsWalkable(x, y)) continue;
                    int next = y * Width + x;
                    if (discovery[next] == 0)
                    {
                        parent[next] = cell; children[cell]++;
                        discovery[next] = low[next] = ++time; stack.Push(next);
                    }
                    else if (next != parent[cell]) low[cell] = Math.Min(low[cell], discovery[next]);
                }
                else
                {
                    stack.Pop();
                    int previous = parent[cell];
                    if (previous < 0)
                    { if (children[cell] > 1 && mainPath[cell]) return true; }
                    else
                    {
                        low[previous] = Math.Min(low[previous], low[cell]);
                        if (parent[previous] >= 0 && low[cell] >= discovery[previous] && mainPath[previous]) return true;
                    }
                }
            }
            return false;
        }

        private void Shuffle<T>(List<T> list)
        { for (int i = list.Count - 1; i > 0; i--) { int j = random.Next(i + 1); T value = list[i]; list[i] = list[j]; list[j] = value; } }

        private readonly struct Rect
        {
            public readonly int X, Y, Width, Height;
            public Rect(int x, int y, int width, int height) { X = x; Y = y; Width = width; Height = height; }
            public bool Contains(int x, int y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
        }
        private static Rect Merge(Rect a, Rect b) => new Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
            Math.Max(a.X + a.Width, b.X + b.Width) - Math.Min(a.X, b.X), Math.Max(a.Y + a.Height, b.Y + b.Height) - Math.Min(a.Y, b.Y));
    }
}
