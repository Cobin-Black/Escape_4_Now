using System.Collections.Generic;
using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Arena
{
    // Behaviors share the existing movement, floor rendering, falling animation and connectivity checks.
    internal sealed class ArenaEnvironmentalEvents
    {
        private readonly ArenaEventDirector owner;
        private readonly System.Random random;
        private ArenaMatch Match => owner.Match;
        private ArenaMap Map => Match.Map;
        private ArenaEventPool Pool => owner.Pool;

        internal ArenaEnvironmentalEvents(ArenaEventDirector owner, System.Random random)
        {
            this.owner = owner;
            this.random = random;
        }

        // Choose a safe place or target before showing the warning.
        internal bool TryPrepare(ArenaEventEntry rule, out ArenaEventState state)
        {
            state = new ArenaEventState
            {
                Rule = rule
            };
            var alive = Match.Players.FindAll(p => p.Alive);
            if (alive.Count < 2)
                return false;
            if (rule.kind == ArenaEventKind.PlayerSwap)
            {
                ArenaLayout.Shuffle(alive, random);
                state.first = alive[0];
                state.second = alive[1];
                state.Center = alive[0].Position;
                return true;
            }

            if (rule.IsBlackout || rule.IsInstant || rule.kind == ArenaEventKind.Spotlight)
            {
                if (Match is HotPotatoController potato)
                {
                    if (potato.BetweenRounds || potato.Holder == null)
                        return false;
                    if (rule.kind == ArenaEventKind.TimeCut && potato.RemainingSeconds <= rule.warningSeconds + Pool.minimumPotatoTimeAfterCut)
                        return false;
                    state.Center = potato.Holder.Position;
                }
                else
                    state.Center = alive[0].Position;
                return true;
            }

            if (rule.kind == ArenaEventKind.BleachersMove || rule.kind == ArenaEventKind.CourtShuffle)
                return PrepareEquipment(rule, state);
            if (rule.IsBlock && rule.obstaclePrefab == null)
                return false;
            var cells = Map.Layout.Cells();
            ArenaLayout.Shuffle(cells, random);
            cells.RemoveAll(c => owner.Reserved(c) || !ArenaEventSafety.ClearOfPlayers(Match, c, Pool.playerClearance) || !Compatible(rule, c));
            int tries = 0;
            foreach (var cell in cells)
            {
                if (++tries > Pool.safePlacementAttempts)
                    break;
                state.Center = cell;
                state.blockCell = cell;
                state.Cells.Clear();
                if (rule.kind == ArenaEventKind.SoundBlast)
                    foreach (var d in ArenaLayout.Directions)
                        if (!Map.Layout.Walkable(cell + d))
                        {
                            state.sourceCell = cell + d;
                            break;
                        }

                if (rule.IsBlock)
                {
                    if (!owner.CanChange(new[] { cell }))
                        continue;
                    state.Cells.Add(cell);
                    return true;
                }

                if (rule.kind == ArenaEventKind.BallBarrage || rule.kind == ArenaEventKind.MovingStageLights)
                {
                    var directions = new List<Vector2Int>(ArenaLayout.Directions);
                    ArenaLayout.Shuffle(directions, random);
                    foreach (var direction in directions)
                    {
                        state.Cells.Clear();
                        state.Cells.Add(cell);
                        for (int step = 1; step <= rule.travelDistance; step++)
                        {
                            var next = cell + direction * step;
                            if (!Map.Layout.Walkable(next) || owner.Reserved(next) || !Compatible(rule, next))
                                break;
                            state.Cells.Add(next);
                        }

                        if (state.Cells.Count >= 3)
                        {
                            state.Direction = direction;
                            return true;
                        }
                    }

                    continue;
                }

                state.Cells.AddRange(SurvivalEnvironmentalPlacement.NaturalPatch(cell, cells, Mathf.Max(1, rule.zoneTiles), random));
                if (state.Cells.Count < Mathf.Min(3, rule.zoneTiles))
                    continue;
                if (rule.kind == ArenaEventKind.HazardZone && !owner.CanChange(state.Cells))
                    continue;
                return true;
            }

            return false;
        }

        private bool Compatible(ArenaEventEntry rule, Vector2Int cell)
        {
            var surface = Map.Layout.surfaces[cell.x, cell.y];
            if (rule.kind == ArenaEventKind.BallBarrage || rule.kind == ArenaEventKind.SlipperyFloor)
                return surface == ArenaSurface.Court || surface == ArenaSurface.Wood;
            if (rule.kind == ArenaEventKind.SoundBlast)
            {
                if (surface != ArenaSurface.Stage)
                    return false;
                foreach (var d in ArenaLayout.Directions)
                    if (!Map.Layout.Walkable(cell + d))
                        return true;
                return false;
            }

            if (rule.kind == ArenaEventKind.CurtainDrop || rule.kind == ArenaEventKind.MovingStageLights)
                return surface == ArenaSurface.Stage;
            if (rule.kind == ArenaEventKind.SeatBlock)
            {
                if (surface != ArenaSurface.Carpet)
                    return false;
                foreach (var d in ArenaLayout.Directions)
                    if (Map.Layout.seats.Contains(cell + d))
                        return true;
                return false;
            }

            return true;
        }

        private bool PrepareEquipment(ArenaEventEntry rule, ArenaEventState state)
        {
            var props = new List<ArenaMap.EquipmentView>(Map.EquipmentViews);
            ArenaLayout.Shuffle(props, random);
            foreach (var prop in props)
            {
                if (prop.Visual == null || owner.Reserved(prop.Cell) || rule.kind == ArenaEventKind.BleachersMove && !prop.IsBleachers || rule.kind == ArenaEventKind.CourtShuffle && prop.IsBleachers)
                    continue;
                var targets = rule.kind == ArenaEventKind.BleachersMove ? new List<Vector2Int>() : Map.Layout.Cells();
                if (rule.kind == ArenaEventKind.BleachersMove)
                    foreach (var d in ArenaLayout.Directions)
                        targets.Add(prop.Cell + d);
                ArenaLayout.Shuffle(targets, random);
                int tries = 0;
                foreach (var cell in targets)
                {
                    if (++tries > Pool.safePlacementAttempts)
                        break;
                    if (owner.Reserved(cell) || !Map.Layout.Walkable(cell) || Vector2.Distance(cell, prop.Cell) > rule.travelDistance || !ArenaEventSafety.ClearOfPlayers(Match, cell, Pool.playerClearance))
                        continue;
                    if (!owner.CanChange(new[] { cell }, rule.kind == ArenaEventKind.CourtShuffle ? (Vector2Int? )prop.Cell : null))
                        continue;
                    state.equipment = prop;
                    state.sourceCell = prop.Cell;
                    state.blockCell = cell;
                    state.Center = cell;
                    state.Cells.Add(cell);
                    state.originalPosition = prop.Visual.position;
                    state.originalScale = prop.Visual.localScale;
                    return true;
                }
            }

            return false;
        }

        // Check safety again after the warning, because players may have moved into the area.
        internal bool Activate(ArenaEventState state)
        {
            var rule = state.Rule;
            var potato = Match as HotPotatoController;
            if (rule.kind == ArenaEventKind.PlayerSwap)
                return ArenaEventSafety.Swap(Match, state.first, state.second);
            if (rule.kind == ArenaEventKind.PotatoShuffle)
            {
                if (potato == null || potato.Holder == null)
                    return false;
                state.first = potato.Holder;
                potato.ShuffleHolder(true);
                state.second = potato.Holder;
                state.Center = potato.Holder.Position;
                return true;
            }

            if (rule.kind == ArenaEventKind.ExtraTime)
            {
                if (potato == null)
                    return false;
                potato.AdjustTime(Mathf.Abs(rule.amount));
                return true;
            }

            if (rule.kind == ArenaEventKind.TimeCut)
            {
                if (potato == null || potato.RemainingSeconds <= Pool.minimumPotatoTimeAfterCut)
                    return false;
                state.Direction = new Vector2(Mathf.Min(Mathf.Abs(rule.amount), potato.RemainingSeconds - Pool.minimumPotatoTimeAfterCut), 0);
                potato.AdjustTime(-state.Direction.x);
                return true;
            }

            if (rule.kind == ArenaEventKind.CourtShuffle)
            {
                if (state.equipment == null || state.equipment.Visual == null || !owner.CanChange(new[] { state.blockCell }, state.sourceCell))
                    return false;
                Map.Layout.blocked[state.sourceCell.x, state.sourceCell.y] = false;
                Map.Layout.blocked[state.blockCell.x, state.blockCell.y] = true;
                state.equipment.Cell = state.blockCell;
                state.equipment.Visual.position = Map.World(state.blockCell);
                state.landed = true;
                return true;
            }

            if (rule.IsBlock)
            {
                if (!owner.CanChange(new[] { state.blockCell }))
                    return false;
                if (rule.kind == ArenaEventKind.BleachersMove)
                {
                    CommitBlock(state);
                    Vector2 d = state.blockCell - state.sourceCell;
                    var model = state.equipment.Visual.GetComponent<SurvivalPropVisual>();
                    model.Build(Map.Settings.tileSize, new Vector2(1 + Mathf.Abs(d.x), 1 + Mathf.Abs(d.y)) * .88f, Map.Settings.material, Map.Order(state.sourceCell), true, state.equipment.QuarterTurns);
                    state.equipment.Visual.position = Map.World(((Vector2)state.blockCell + state.sourceCell) * .5f);
                    return true;
                }

                var visual = state.View.SpawnObstacle(rule.obstaclePrefab, state.Center);
                var fall = visual.GetComponent<SurvivalBranchFall>();
                if (fall != null)
                    fall.Configure(() => !state.Ended && TryLand(state), () => state.Ended = true, () => Match.Paused || !Match.Playing);
                else
                    return TryLand(state);
            }

            if (rule.kind == ArenaEventKind.HazardZone && !owner.CanChange(state.Cells))
                return false;
            if (rule.kind == ArenaEventKind.BallBarrage && !ArenaEventSafety.ClearOfPlayers(Match, state.Cells[0], Pool.visuals.ballRadius))
                return false;
            state.View.ActivateGround();
            return true;
        }

        private bool TryLand(ArenaEventState state)
        {
            if (!owner.CanChange(new[] { state.blockCell }))
                return false;
            CommitBlock(state);
            return true;
        }

        private void CommitBlock(ArenaEventState state)
        {
            Map.Layout.blocked[state.blockCell.x, state.blockCell.y] = true;
            state.blocked = true;
            state.landed = true;
        }

        internal string ActivationMessage(ArenaEventState state)
        {
            if (state.Rule.kind == ArenaEventKind.TimeCut)
                return state.Rule.label + ": -" + state.Direction.x.ToString("0.#") + "s";
            if (state.Rule.kind == ArenaEventKind.ExtraTime)
                return state.Rule.label + ": +" + Mathf.Abs(state.Rule.amount).ToString("0.#") + "s";
            if (state.Rule.kind == ArenaEventKind.PotatoShuffle)
                return state.Rule.label + ": " + ((HotPotatoController)Match).Holder.name;
            if (state.Rule.kind == ArenaEventKind.PlayerSwap)
                return state.Rule.label + ": " + state.first.name + " / " + state.second.name;
            return state.Rule.label + " active";
        }

        internal bool Contains(ArenaEventState state, Vector2 point)
        {
            if (state.Rule.kind == ArenaEventKind.MovingStageLights)
                return Vector2.Distance(LightCenter(state), point) <= state.Rule.radius;
            return state.Cells.Contains(Vector2Int.RoundToInt(point));
        }

        internal Vector2 LightCenter(ArenaEventState state)
        {
            if (state.Cells.Count < 2)
                return state.Center;
            float phase = Mathf.PingPong(state.Elapsed / Pool.visuals.movingLightSeconds, 1);
            return Vector2.Lerp(state.Cells[0], state.Cells[state.Cells.Count - 1], phase);
        }

        // Apply an active effect through the existing movement, health and potato hooks.
        internal void Tick(ArenaEventState state, float dt)
        {
            var rule = state.Rule;
            if (rule.kind == ArenaEventKind.BallBarrage)
                Balls(state, dt);
            if (rule.IsWave)
            {
                float radius = Mathf.Clamp01(state.Elapsed / rule.waveSeconds) * rule.radius;
                foreach (var player in Match.Players)
                    if (player.Alive && !state.hit.Contains(player) && Vector2.Distance(player.Position, state.Center) <= radius && ArenaEventSafety.ClearLine(Match, state.Center, player.Position))
                    {
                        state.hit.Add(player);
                        Push(player, player.Position - state.Center, rule.amount);
                    }
            }

            if (rule.kind == ArenaEventKind.HazardZone)
                foreach (var player in Match.Players)
                    if (player.Alive && Contains(state, player.Position) && (!state.nextHit.TryGetValue(player, out float time) || state.Elapsed >= time))
                    {
                        state.nextHit[player] = state.Elapsed + rule.hitInterval;
                        if (rule.damage > 0)
                            player.Damage(rule.damage);
                        Push(player, player.Position - state.Center, rule.amount);
                    }

            if (state.equipment != null && state.equipment.Visual != null)
            {
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(state.Elapsed / Pool.visuals.obstacleAnimationSeconds));
                if (rule.kind == ArenaEventKind.CourtShuffle)
                    state.equipment.Visual.position = Map.World(state.blockCell);
                else if (rule.kind == ArenaEventKind.BleachersMove)
                {
                    state.equipment.Visual.localScale = state.originalScale * Mathf.Lerp(.97f, 1, t);
                }
            }
        }

        private void Push(ArenaParticipant player, Vector2 direction, float distance)
        {
            player.Knockback((direction.sqrMagnitude > .001f ? direction.normalized : Vector2.up) * Mathf.Max(0, distance));
        }

        private void Balls(ArenaEventState state, float dt)
        {
            if (state.ballsSpawned < state.Rule.ballCount && state.Elapsed >= state.nextBall)
            {
                state.ballsSpawned++;
                state.nextBall = state.Elapsed + state.Rule.ballInterval;
                if (ArenaEventSafety.ClearOfPlayers(Match, state.Cells[0], Pool.visuals.ballRadius))
                    state.balls.Add(new ArenaEventBall { position = state.Cells[0], visual = state.View.SpawnBall() });
            }

            for (int i = state.balls.Count - 1; i >= 0; i--)
            {
                var ball = state.balls[i];
                Vector2 old = ball.position;
                float step = state.Rule.ballSpeed * dt;
                Vector2 next = old + state.Direction * step;
                if (ball.travel + step > state.Cells.Count - 1 || !ArenaEventSafety.ClearLine(Match, old, next, Pool.visuals.ballRadius))
                {
                    Object.Destroy(ball.visual);
                    state.balls.RemoveAt(i);
                    continue;
                }

                ball.position = next;
                ball.travel += step;
                foreach (var player in Match.Players)
                    if (player.Alive && !ball.hit.Contains(player))
                    {
                        float distance = Mathf.Clamp(Vector2.Dot(player.Position - old, state.Direction), 0, step);
                        Vector2 closest = old + distance * state.Direction;
                        if (Vector2.Distance(player.Position, closest) > Pool.visuals.ballRadius + Map.Settings.playerRadius)
                            continue;
                        ball.hit.Add(player);
                        Push(player, state.Direction, state.Rule.amount);
                        owner.Play(state.Rule.activationSound);
                    }

                if (ball.visual != null)
                    ball.visual.transform.position = Map.World(ball.position) + Vector3.up * (Mathf.Abs(Mathf.Sin(ball.travel * Pool.visuals.bounceRate)) * Pool.visuals.bounceHeight + .15f);
            }

            if (state.ballsSpawned >= state.Rule.ballCount && state.balls.Count == 0)
                state.Ended = true;
        }

        // Remove temporary blocks and restore expanded bleachers without changing mode rules.
        internal void End(ArenaEventState state)
        {
            if (state.blocked && Map.Layout != null)
                Map.Layout.blocked[state.blockCell.x, state.blockCell.y] = false;
            if (state.Rule.kind == ArenaEventKind.BleachersMove && state.landed && state.equipment != null && state.equipment.Visual != null)
            {
                state.equipment.Visual.GetComponent<SurvivalPropVisual>().Build(Map.Settings.tileSize, Vector2.one * .88f, Map.Settings.material, Map.Order(state.sourceCell), true, state.equipment.QuarterTurns);
                state.equipment.Visual.position = state.originalPosition;
                state.equipment.Visual.localScale = state.originalScale;
            }

            if (state.Rule.kind == ArenaEventKind.CourtShuffle && state.landed && state.equipment?.Visual != null)
                state.equipment.Visual.position = Map.World(state.equipment.Cell);
        }
    }
}
