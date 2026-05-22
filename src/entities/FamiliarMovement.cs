using Microsoft.Xna.Framework;
using StardewValley;
using StardewModdingAPI;

namespace FAUNA
{
    public partial class FamiliarEntity
    {
        // ─────────────────────────────────────────────────────────
        // Movement fields
        // ─────────────────────────────────────────────────────────

        private int _facingDirection = 2;
        private bool _wasIdling = false;
        private float _animateTimer = 0f;
        private Vector2 _frozenFollowTarget = Vector2.Zero;

        private Stack<Point> _navPath = new();
        private Point _lastPathFromTile = Point.Zero;

        private const float FollowStopDistance = 96f;
        private const float FollowTeleportDistance = 1024f;
        
        private Point _lastPathTargetTile = Point.Zero;

        // ─────────────────────────────────────────────────────────
        // Passive wander fields
        // ─────────────────────────────────────────────────────────

        private int _moveDuration = 0;
        private int _pauseDuration = 0;
        private bool _isMoving = false;

        // ─────────────────────────────────────────────────────────
        // Collision
        // ─────────────────────────────────────────────────────────

public bool IsTileBlocked(GameLocation location, Vector2 pos)
{
    // Check every tile the bounding box overlaps
    int left   = (int)(pos.X + 4)  / 64;
    int right  = (int)(pos.X + 28) / 64;
    int top    = (int)(pos.Y + 8)  / 64;
    int bottom = (int)(pos.Y + 32) / 64;

    int mapW = location.map.Layers[0].LayerSize.Width;
    int mapH = location.map.Layers[0].LayerSize.Height;

    string[] blockingLayers = { "Buildings", "Back" };

    for (int tx = left; tx <= right; tx++)
    for (int ty = top; ty <= bottom; ty++)
    {
        if (tx < 0 || ty < 0 || tx >= mapW || ty >= mapH)
            return true;

        // Check Buildings layer
        var buildingsLayer = location.map.GetLayer("Buildings");
        if (buildingsLayer != null)
        {
            var tile = buildingsLayer.Tiles[tx, ty];
            if (tile != null
                && !tile.TileIndexProperties.ContainsKey("Shadow")
                && !tile.TileIndexProperties.ContainsKey("Passable")
                && !tile.Properties.ContainsKey("Passable"))
                return true;
        }

        // Check Back layer for NoPath/NPCBarrier properties
        var backLayer = location.map.GetLayer("Back");
        if (backLayer != null)
        {
            var tile = backLayer.Tiles[tx, ty];
            if (tile != null)
            {
                if (tile.TileIndexProperties.ContainsKey("NoPath") ||
                    tile.Properties.ContainsKey("NoPath") ||
                    tile.TileIndexProperties.ContainsKey("NPCBarrier") ||
                    tile.Properties.ContainsKey("NPCBarrier"))
                    return true;
            }
        }
    }

    // Also check objects/clumps via isCollidingPosition
    Microsoft.Xna.Framework.Rectangle bounds = new Microsoft.Xna.Framework.Rectangle(
        (int)pos.X + 8,
        (int)pos.Y + 16,
        16,
        16);

    return location.isCollidingPosition(
        bounds,
        Game1.viewport,
        isFarmer: false,
        damagesFarmer: 0,
        glider: false,
        character: this);
}

private bool HasLineOfSight(GameLocation location, Vector2 from, Vector2 to)
{
    Vector2 diff = to - from;
    float dist = diff.Length();
    if (dist < 1f) return true;

    int steps = (int)(dist / 8f) + 1;
    Vector2 step = diff / steps;

    for (int i = 1; i < steps; i++)
    {
        if (IsTileBlocked(location, from + step * i))
            return false;
    }
    return true;
}

        // ─────────────────────────────────────────────────────────
        // Passive state
        // ─────────────────────────────────────────────────────────

        private void UpdatePassive(GameTime time, GameLocation location)
        {
            controller = null;
            xVelocity = 0f;
            yVelocity = 0f;

            bool alwaysAnimate = ModEntry.RegisteredFamiliars.TryGetValue(
                FamiliarId, out var famData)
                && famData.AlwaysAnimate
                && ModEntry.FamiliarManager?.OwnedFamiliars
                    .FirstOrDefault(f => f.InstanceId == InstanceId)
                    ?.CurrentForm == FamiliarForm.Animal;

            if (_isMoving)
            {
                _moveDuration--;

                float spd = speed * 1f;
                float dx = 0f, dy = 0f;

                switch (_facingDirection)
                {
                    case 0: dy = -spd; break;
                    case 1: dx =  spd; break;
                    case 2: dy =  spd; break;
                    case 3: dx = -spd; break;
                }

                AnimateMovement(time);

                Vector2 nextPos = new Vector2(Position.X + dx, Position.Y + dy);

                if (IsTileBlocked(location, nextPos) || _moveDuration <= 0)
                {
                    _isMoving = false;
                    _pauseDuration = Game1.random.Next(60, 180);
                    if (!alwaysAnimate)
                        Sprite.StopAnimation();
                }
                else
                {
                    Position = nextPos;
                }
            }
            else
            {
                _pauseDuration--;

                if (alwaysAnimate)
                    TickAlwaysAnimate(time); // was AnimateMovement(time)

                if (_pauseDuration <= 0)
                {
                    _facingDirection = Game1.random.Next(4);
                    _moveDuration    = Game1.random.Next(60, 240);
                    
                    // Don't start moving if already in a blocked tile
                    if (IsTileBlocked(location, Position))
                    {
                        // Try to nudge to nearest open tile
                        Position = FindOpenTileNear(location, Position);
                    }
                    
                    _isMoving = true;
                    faceDirection(_facingDirection);
                }
            }
        }

        // ─────────────────────────────────────────────────────────
        // Following state
        // ─────────────────────────────────────────────────────────

        private void UpdateFollowing(GameTime time, GameLocation location)
        {
            Farmer player = Game1.player;

            if (player.currentLocation != location)
            {
                SetState(FamiliarState.Passive);
                return;
            }

            var instance = ModEntry.FamiliarManager?.OwnedFamiliars
                .FirstOrDefault(f => f.InstanceId == InstanceId);

            float distanceToPlayer = Vector2.Distance(Position, player.Position);

            if (distanceToPlayer >= FollowTeleportDistance)
            {
                Position = FindOpenTileNear(location, player.Position);
                _navPath.Clear();
                _lastPathFromTile = Point.Zero;
                controller = null;
                xVelocity = 0f;
                yVelocity = 0f;
                return;
            }

            speed = (int)Math.Ceiling(player.getMovementSpeed());
            float spd = player.getMovementSpeed();

            _abilityHandler?.UpdateFollowing(time, location);

            if (AbilityState != FamiliarAbilityState.None)
            {
                UpdateAbilityMovement(time, location, player);
                _abilityHandler?.TickCooldowns(time);
                return;
            }

            bool playerIsMoving = player.isMoving();

            // Freeze follow target when player stops so familiars
            // don't scramble when player turns in place
            Vector2 followTarget;
            
            if (playerIsMoving)
            {
                followTarget = GetFollowTarget(player);
                _frozenFollowTarget = followTarget;
            }
            else
            {
                followTarget = _frozenFollowTarget == Vector2.Zero
                    ? GetFollowTarget(player)
                    : _frozenFollowTarget;
            }

            float distanceToTarget = Vector2.Distance(Position, followTarget);
            float distanceToStop   = Math.Max(0f, distanceToTarget - 32f);
            float moveAmount       = distanceToStop < 64f
                ? spd * (distanceToStop / 64f)
                : Math.Min(spd, distanceToStop);
            
            // Idle — close enough and player stopped
            if (moveAmount < 0.5f && !playerIsMoving)
            {
                _wasIdling = true;
                xVelocity  = 0f;
                yVelocity  = 0f;
                controller = null;
                faceGeneralDirection(player.Position);
                speed = 2;

                if (ModEntry.RegisteredFamiliars.TryGetValue(FamiliarId, out var famData)
                    && famData.AlwaysAnimate
                    && instance?.CurrentForm == FamiliarForm.Animal)
                    TickAlwaysAnimate(time); // was AnimateMovement(time)
                else
                    Sprite.currentFrame = 16;
                return;
            }

            if (playerIsMoving)
                _wasIdling = false;

            // Hysteresis — don't start moving again until clearly outside idle zone
            if (_wasIdling && distanceToTarget < 40f)
            {
                if (ModEntry.RegisteredFamiliars.TryGetValue(FamiliarId, out var famData)
                    && famData.AlwaysAnimate
                    && instance?.CurrentForm == FamiliarForm.Animal)
                    TickAlwaysAnimate(time); // was AnimateMovement(time)
                else
                    Sprite.currentFrame = 16;
                return;
            }

            _wasIdling = false;
            MoveToward(time, location, followTarget, moveAmount);
        }

        // ─────────────────────────────────────────────────────────
        // Core movement — pathfind + direct position write
        // ─────────────────────────────────────────────────────────

        private void MoveToward(GameTime time, GameLocation location, Vector2 target, float moveAmount)
        {
            
            Vector2 diff = target - Position;
            float dist   = diff.Length();
            

            if (dist < 0.5f || moveAmount < 0.1f)
            {
                xVelocity = 0f;
                yVelocity = 0f;
                return;
            }

            Point currentTile = new Point((int)(Position.X / 64f), (int)(Position.Y / 64f));
            Point targetTile  = new Point((int)(target.X / 64f),   (int)(target.Y / 64f));

            bool los = HasLineOfSight(location, Position, target);

            if (los)
            {
                // Clear stale path — we have direct sight
                _navPath.Clear();
                _lastPathFromTile   = Point.Zero;
                _lastPathTargetTile = Point.Zero;
            }
            else if (_navPath.Count == 0 || currentTile != _lastPathFromTile)
            {
                _navPath.Clear();

                Point pathTarget = targetTile;
                Vector2 targetTilePos = new Vector2(targetTile.X * 64f, targetTile.Y * 64f);
                if (IsTileBlocked(location, targetTilePos))
                {
                    Vector2 openTile = FindOpenTileNear(location, target);
                    pathTarget = new Point((int)(openTile.X / 64f), (int)(openTile.Y / 64f));
                }

                _lastPathFromTile   = currentTile;
                _lastPathTargetTile = pathTarget;
                try { FindPath(currentTile, pathTarget, location, _navPath); }
                catch { _navPath.Clear(); }

                if (_navPath.Count > 0 && _navPath.Peek() == currentTile)
                    _navPath.Pop();
            }

            // Default: head straight for target
            Vector2 dir = diff / dist;

            if (_navPath.Count > 0)
            {
                var tiles = _navPath.ToArray();
                string pathStr = string.Join(" -> ", tiles.Select(t => $"({t.X},{t.Y})"));
            }

            // Follow path if we have one
            while (_navPath.Count > 0)
            {
                Point   nextTile  = _navPath.Peek();
                Vector2 waypoint = new Vector2(nextTile.X * 64f + 16f, nextTile.Y * 64f + 16f);
                Vector2 center    = new Vector2(Position.X + 32f, Position.Y + 32f);

                if (Vector2.Distance(center, waypoint) < 20f)
                {
                    _navPath.Pop(); // reached this waypoint, move to next
                }
                else
                {
                    Vector2 toWaypoint = waypoint - Position;
                    float   wpDist     = toWaypoint.Length();
                    dir = wpDist > 0.5f ? toWaypoint / wpDist : diff / dist;
                    break;
                }
            }

            // Per-axis collision sliding — prevents clipping through walls
            Vector2 move = new Vector2(dir.X * moveAmount, dir.Y * moveAmount);
            Vector2 newPos = Position;

            if (!IsTileBlocked(location, newPos + move))
            {
                newPos += move;
            }
            else
            {
                Vector2 moveX = new Vector2(move.X, 0f);
                Vector2 moveY = new Vector2(0f,     move.Y);

                if (!IsTileBlocked(location, newPos + moveX)) newPos += moveX;
                if (!IsTileBlocked(location, newPos + moveY)) newPos += moveY;
            }

            if (_navPath.Count > 0 && newPos == Position)
            {
                Point nextTile = _navPath.Peek();
                bool tileBlocked  = IsTileBlocked(location, new Vector2(nextTile.X * 64f, nextTile.Y * 64f));
                bool moveBlocked  = IsTileBlocked(location, Position + move);
                bool xOnlyBlocked = IsTileBlocked(location, new Vector2(Position.X + move.X, Position.Y));
                bool yOnlyBlocked = IsTileBlocked(location, new Vector2(Position.X, Position.Y + move.Y));
            }


            Vector2 before = position.Value;
            position.Value = newPos;
            Vector2 after = position.Value;

            xVelocity = 0f;
            yVelocity = 0f;

            UpdateFacing(dir.X, dir.Y);
            AnimateMovement(time);
        }

        // ─────────────────────────────────────────────────────────
        // BFS pathfinder
        // ─────────────────────────────────────────────────────────

        private void FindPath(Point start, Point end, GameLocation location, Stack<Point> result)
        {
            var queue   = new Queue<Point>();
            var visited = new Dictionary<Point, Point>();
            queue.Enqueue(start);
            visited[start] = start;

            int[] dx = { -1, 1, 0, 0 };
            int[] dy = {  0, 0, -1, 1 };
            int mapW = location.map.Layers[0].LayerSize.Width;
            int mapH = location.map.Layers[0].LayerSize.Height;
            bool found    = false;
            int  maxNodes = 200;
            int  processed = 0;

            while (queue.Count > 0 && processed < maxNodes)
            {
                processed++;
                Point current = queue.Dequeue();

                if (current == end) { found = true; break; }

                for (int i = 0; i < 4; i++)
                {
                    Point next = new Point(current.X + dx[i], current.Y + dy[i]);
                    if (next.X < 0 || next.Y < 0 || next.X >= mapW || next.Y >= mapH) continue;
                    if (visited.ContainsKey(next)) continue;

                    Vector2 nextPos = new Vector2(next.X * 64f, next.Y * 64f);
                    if (IsTileBlocked(location, nextPos)) continue;

                    visited[next] = current;
                    queue.Enqueue(next);
                }
            }

            if (found)
            {
                Point current = end;
                while (current != start)
                {
                    result.Push(current);
                    current = visited[current];
                }
            }
        }

        // ─────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────

        private Vector2 GetFollowTarget(Farmer player)
        {
            int     spacing = 64;
            Vector2 target  = player.Position;

            switch (player.FacingDirection)
            {
                case 0: target.Y += spacing * (FollowIndex + 1); break;
                case 1: target.X -= spacing * (FollowIndex + 1); break;
                case 2: target.Y -= spacing * (FollowIndex + 1); break;
                case 3: target.X += spacing * (FollowIndex + 1); break;
            }

            return target;
        }

        public Vector2 FindOpenTileNear(GameLocation location, Vector2 center)
        {
            int cx = (int)(center.X / 64f);
            int cy = (int)(center.Y / 64f);

            for (int radius = 1; radius <= 5; radius++)
                for (int dx = -radius; dx <= radius; dx++)
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;

                        int checkX = cx + dx;
                        int checkY = cy + dy;

                        if (checkX < 0 || checkY < 0 ||
                            checkX >= location.map.Layers[0].LayerSize.Width ||
                            checkY >= location.map.Layers[0].LayerSize.Height) continue;

                        Vector2 checkPos = new Vector2(checkX * 64f, checkY * 64f);
                        if (!IsTileBlocked(location, checkPos))
                            return checkPos;
                    }

            return center + new Vector2(64f, 0f);
        }

        public void ClearPath()
        {
            _navPath.Clear();
            _lastPathFromTile  = Point.Zero;
            _lastPathTargetTile = Point.Zero;
            _frozenFollowTarget = Vector2.Zero;
        }

        private void UpdateFacing(float dx, float dy)
        {
            if (Math.Abs(dx) > Math.Abs(dy))
                _facingDirection = dx > 0 ? 1 : 3;
            else if (dy != 0)
                _facingDirection = dy > 0 ? 2 : 0;
            faceDirection(_facingDirection);
        }

        private void AnimateMovement(GameTime time)
        {
            switch (_facingDirection)
            {
                case 0: Sprite.Animate(time,  8, 4, 100f); break;
                case 1: Sprite.Animate(time,  4, 4, 100f); break;
                case 2: Sprite.Animate(time,  0, 4, 100f); break;
                case 3: Sprite.Animate(time, 12, 4, 100f); break;
            }
        }

        private void TickAlwaysAnimate(GameTime time)
        {
            _animateTimer += (float)time.ElapsedGameTime.TotalMilliseconds;
            
            if (!ModEntry.RegisteredFamiliars.TryGetValue(FamiliarId, out var data))
                return;
                
            if (_animateTimer >= data.AnimateInterval)
            {
                _animateTimer = 0f;
                AnimateMovement(time);
            }
        }
    }
}