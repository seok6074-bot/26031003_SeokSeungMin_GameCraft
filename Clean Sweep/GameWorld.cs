using System.Numerics;

// Game rules use world coordinates and have no rendering dependencies.
internal sealed class GameWorld
{
    public const int Width = 1600, Height = 1200;
    public enum Phase { Title, Playing, Paused, Won, Lost }
    public sealed class Trash(Vector2 p, int kind) { public Vector2 Position = p; public int Kind = kind; }
    public sealed class Robot(Vector2 p) { public Vector2 Position = p; public int Health = 3; public float Drop = 10, Flash, Arrival; }
    public sealed class Bullet(Vector2 p, Vector2 direction) { public Vector2 Position = p, Direction = direction; public float Distance; }
    public sealed class Effect(Vector2 p, string kind, float life = .35f) { public Vector2 Position = p; public string Kind = kind; public float Life = life, Duration = life; }
    public readonly record struct Box(float X, float Y, float W, float H);
    public readonly List<Trash> Litter = [];
    public readonly List<Robot> Robots = [];
    public readonly List<Bullet> Bullets = [];
    public readonly List<Effect> Effects = [];
    public readonly List<Box> Obstacles = [new(400, 340, 96, 96), new(1080, 300, 96, 96), new(780, 760, 96, 96), new(300, 880, 64, 64), new(1220, 920, 64, 64)];
    public Phase State = Phase.Title;
    public Vector2 Player, Velocity, Facing = Vector2.UnitY;
    public int Health, Ammo, Destroyed;
    public float TimeLeft, Invincible, ShootCooldown, SuckCooldown, EmptyFlash, Walk;
    public bool Suction;
    public const int MaxRobots = 6;
    public float SpawnCountdown = 12;
    public float Clean => Math.Clamp((50 - Litter.Count - Bullets.Count) * 2, 0, 100);
    public event Action<string>? Sound;
    private Random random = new(731);
    public GameWorld() { Reset(); State = Phase.Title; }
    public void Reset()
    {
        random = new(731); Player = new(210, 210); Velocity = Vector2.Zero; Facing = Vector2.UnitY;
        Health = 5; Ammo = Destroyed = 0; TimeLeft = 180; SpawnCountdown = 12;
        Invincible = ShootCooldown = SuckCooldown = EmptyFlash = Walk = 0; Suction = false;
        Litter.Clear(); Robots.Clear(); Bullets.Clear(); Effects.Clear();
        foreach (var p in new Vector2[] { new(210, 270), new(245, 290), new(180, 310), new(280, 330), new(315, 270), new(220, 370) }) Litter.Add(new(p, random.Next(6)));
        while (Litter.Count < 50)
        {
            var p = new Vector2(random.Next(65, Width - 65), random.Next(65, Height - 65));
            if (Free(p, 24) && Litter.All(t => Vector2.Distance(t.Position, p) > 35)) Litter.Add(new(p, random.Next(6)));
        }
        Robots.AddRange(new[] { new Robot(new(1250, 180)), new Robot(new(1380, 1030)), new Robot(new(620, 1080)), new Robot(new(1100, 650)) });
        State = Phase.Playing;
    }
    public bool Free(Vector2 p, float r) => p.X >= 32 + r && p.X <= Width - 32 - r && p.Y >= 32 + r && p.Y <= Height - 32 - r && !Obstacles.Any(b => p.X + r > b.X && p.X - r < b.X + b.W && p.Y + r > b.Y && p.Y - r < b.Y + b.H);
    public bool ClearLine(Vector2 a, Vector2 b)
    {
        int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(a, b) / 6));
        for (int i = 0; i <= steps; i++) if (!Free(Vector2.Lerp(a, b, (float)i / steps), 2)) return false;
        return true;
    }
    Vector2 Move(Vector2 p, Vector2 d, float r)
    {
        if (Free(p + new Vector2(d.X, 0), r)) p.X += d.X;
        if (Free(p + new Vector2(0, d.Y), r)) p.Y += d.Y;
        return p;
    }
    public bool InSuction(Vector2 p)
    {
        var d = p - Player; float f = Vector2.Dot(d, Facing);
        return f >= -8 && f <= 112 && MathF.Abs(d.X * Facing.Y - d.Y * Facing.X) < 43 && ClearLine(Player, p);
    }
    public void Tick(float dt, Vector2 movement, bool suck, bool fire)
    {
        if (State != Phase.Playing) return;
        dt = Math.Clamp(dt, 0, .1f);
        int steps = Math.Max(1, (int)MathF.Ceiling(dt * 120));
        for (int s = 0; s < steps && State == Phase.Playing; s++) Step(dt / steps, movement, suck, fire);
    }
    void Step(float dt, Vector2 movement, bool suck, bool fire)
    {
        TimeLeft = Math.Max(0, TimeLeft - dt); Invincible = Math.Max(0, Invincible - dt);
        ShootCooldown -= dt; SuckCooldown -= dt; EmptyFlash = Math.Max(0, EmptyFlash - dt);
        SpawnCountdown -= dt;
        if (SpawnCountdown <= 0)
        {
            SpawnCountdown = 12;
            if (Robots.Count < MaxRobots) SpawnRobot();
        }
        if (movement.LengthSquared() > 1) movement = Vector2.Normalize(movement);
        Velocity = Vector2.Lerp(Velocity, movement * 220, 1 - MathF.Exp(-20 * dt));
        Player = Move(Player, Velocity * dt, 17); Walk += Velocity.Length() * dt / 15;
        Suction = suck && !fire;
        if (fire && ShootCooldown <= 0)
        {
            ShootCooldown = .3f;
            if (Ammo > 0) { Ammo--; Bullets.Add(new(Player, Facing)); Sound?.Invoke("shoot"); }
            else { EmptyFlash = .3f; Sound?.Invoke("warning"); }
        }
        if (Suction)
        {
            foreach (var t in Litter.Where(t => InSuction(t.Position))) t.Position = Vector2.Lerp(t.Position, Player + Facing * 19, 1 - MathF.Exp(-8 * dt));
            var nearest = Litter.Where(t => InSuction(t.Position)).OrderBy(t => Vector2.DistanceSquared(t.Position, Player)).FirstOrDefault();
            if (SuckCooldown <= 0 && nearest != null && Vector2.Distance(nearest.Position, Player) < 48)
            {
                Effects.Add(new(nearest.Position, "pickup")); Litter.Remove(nearest); Ammo++; SuckCooldown = .15f; Sound?.Invoke("pickup");
            }
        }
        foreach (var b in Bullets.ToArray())
        {
            var next = b.Position + b.Direction * 500 * dt; b.Distance += 500 * dt;
            if (!Free(next, 9) || b.Distance >= 450) { Litter.Add(new(b.Position, random.Next(6))); Bullets.Remove(b); continue; }
            b.Position = next;
            var hit = Robots.FirstOrDefault(r => Vector2.Distance(r.Position, b.Position) < 31);
            if (hit != null)
            {
                Bullets.Remove(b); hit.Health--; hit.Flash = .2f; Effects.Add(new(hit.Position, "hit"));
                if (hit.Health == 0) { Robots.Remove(hit); Destroyed++; Effects.Add(new(hit.Position, "broken", .65f)); Sound?.Invoke("robot_break"); }
                else Sound?.Invoke("robot_hit");
            }
        }
        foreach (var r in Robots)
        {
            if (r.Arrival > 0) { r.Arrival = Math.Max(0, r.Arrival - dt); continue; }
            r.Flash = Math.Max(0, r.Flash - dt); var d = Player - r.Position;
            if (d.LengthSquared() > 1) r.Position = Move(r.Position, Vector2.Normalize(d) * 90 * dt, 20);
            r.Drop -= dt;
            if (r.Drop <= 0)
            {
                r.Drop = 10;
                if (Litter.Count < 80) for (int i = 0; i < 12; i++)
                {
                    var p = r.Position + new Vector2(random.Next(-48, 49), random.Next(-48, 49));
                    if (Free(p, 12)) { Litter.Add(new(p, random.Next(6))); break; }
                }
            }
            if (Invincible <= 0 && Vector2.Distance(Player, r.Position) < 37) { Health--; Invincible = 1; Effects.Add(new(Player, "hit")); Sound?.Invoke("robot_hit"); }
        }
        foreach (var e in Effects.ToArray()) { e.Life -= dt; if (e.Life <= 0) Effects.Remove(e); }
        if (Health <= 0) Finish(Phase.Lost);
        else if (Clean >= 80) Finish(Phase.Won);
        else if (TimeLeft <= 0) Finish(Phase.Lost);
    }
    void SpawnRobot()
    {
        // Perimeter entrances keep new enemies away from the player and crates.
        for (int i = 0; i < 40; i++)
        {
            var p = random.Next(4) switch
            {
                0 => new Vector2(80, random.Next(80, Height - 80)),
                1 => new Vector2(Width - 80, random.Next(80, Height - 80)),
                2 => new Vector2(random.Next(80, Width - 80), 80),
                _ => new Vector2(random.Next(80, Width - 80), Height - 80)
            };
            if (!Free(p, 30) || Vector2.Distance(p, Player) < 420 || Robots.Any(r => Vector2.Distance(r.Position, p) < 70)) continue;
            Robots.Add(new(p) { Arrival = .8f });
            Effects.Add(new(p, "arrival", .8f));
            Sound?.Invoke("warning");
            return;
        }
    }
    void Finish(Phase phase) { State = phase; Suction = false; Sound?.Invoke(phase == Phase.Won ? "success" : "failure"); }
}
