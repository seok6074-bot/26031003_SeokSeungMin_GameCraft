
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

class GameMain : G2AppBase
{
    public override System.Drawing.Size ScreenSize => GameGlobal.ScreenSize;
    public override string GameName => GameGlobal.GameName;
    readonly GameWorld world = new();
    readonly Dictionary<string, G2Texture> textures = [];
    readonly Dictionary<string, System.Drawing.Size> sizes = [];
    readonly Dictionary<string, G2AudioSound> sounds = [];
    G2Font small = null!, normal = null!, title = null!, large = null!;
    ID2D1SolidColorBrush brush = null!;
    Vortice.DirectWrite.IDWriteTextFormat buttonFormat = null!;
    Vector2 camera;
    float progress, uiTime;
    bool muted, vacuum;
    bool musicPaused;
    float musicVolume;
    static readonly string[] TrashNames = ["bottle", "can", "dust", "paper", "peel", "wrapper"];
    static readonly Color4 White = new(.91f, .96f, 1, 1), Mint = new(.34f, .94f, .77f, 1), Gold = new(1, .79f, .35f, 1);
    protected override void Initialize()
    {
        ClearColor = new(.035f, .065f, .09f, 1);
        var root = Path.Combine(AppContext.BaseDirectory, "resource");
        foreach (string file in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
        {
            string key = Path.GetFileNameWithoutExtension(file); textures[key] = new G2Texture(file);
            using var img = System.Drawing.Image.FromFile(file); sizes[key] = img.Size;
        }
        foreach (string file in Directory.GetFiles(root, "*.wav", SearchOption.AllDirectories)) sounds[Path.GetFileNameWithoutExtension(file)] = new G2AudioSound(file);
        sounds["bgm_clean_sweep"].SetVolume(0);
        sounds["bgm_clean_sweep"].Play(true);
        world.Sound += Play;
        brush = RenderTarget.CreateSolidColorBrush(White);
        small = new("Malgun Gothic", 14); normal = new("Malgun Gothic", 18);
        large = new("Malgun Gothic", 30); title = new("Malgun Gothic", 50);
        buttonFormat = DWriteFactory.CreateTextFormat("Malgun Gothic", 18);
        buttonFormat.TextAlignment = Vortice.DirectWrite.TextAlignment.Center;
        buttonFormat.ParagraphAlignment = Vortice.DirectWrite.ParagraphAlignment.Center;
        camera = CameraTarget();
    }
    void Play(string key) { if (!muted && sounds.TryGetValue(key, out var sound)) sound.Play(); }
    bool Held(Keys key) => Input.IsKeyDown(key) || Input.IsKeyPress(key);
    bool Click(float x, float y, float w, float h) => Input.IsButtonDown(MouseButtons.Left) && Hover(x, y, w, h);
    bool Hover(float x, float y, float w, float h) { var p = Input.MousePosition; return p.X >= x && p.X <= x + w && p.Y >= y && p.Y <= y + h; }
    void Start() { world.Reset(); camera = CameraTarget(); progress = 0; Play("button_click"); }
    Vector2 CameraTarget() => new(Math.Clamp(world.Player.X - 480, 0, GameWorld.Width - 960), Math.Clamp(world.Player.Y - 258, 0, GameWorld.Height - 516));
    protected override void Update()
    {
        float dt = Math.Clamp((float)DeltaTime, 0, .1f); uiTime += dt;
        if (!IsActive)
        {
            if (world.State == GameWorld.Phase.Playing) { world.State = GameWorld.Phase.Paused; world.Suction = false; }
            if (vacuum) { sounds["vacuum_loop"].Stop(); vacuum = false; }
            UpdateMusic(dt, false);
            return;
        }
        if (Input.IsKeyDown(Keys.M)) { muted = !muted; if (muted) foreach (var s in sounds.Where(s => s.Key != "bgm_clean_sweep").Select(s => s.Value)) s.Stop(); }
        if (world.State == GameWorld.Phase.Title)
        {
            if (Input.IsKeyDown(Keys.Enter) || Click(340, 432, 280, 56)) Start();
        }
        else if (world.State == GameWorld.Phase.Playing)
        {
            if (Input.IsKeyDown(Keys.Escape) || Click(884, 20, 48, 48)) { world.State = GameWorld.Phase.Paused; world.Suction = false; }
            else
            {
                Vector2 move = new((Held(Keys.D) ? 1 : 0) - (Held(Keys.A) ? 1 : 0), (Held(Keys.S) ? 1 : 0) - (Held(Keys.W) ? 1 : 0));
                foreach (var (key, dir) in new[] { (Keys.W, -Vector2.UnitY), (Keys.A, -Vector2.UnitX), (Keys.S, Vector2.UnitY), (Keys.D, Vector2.UnitX) }) if (Input.IsKeyDown(key)) world.Facing = dir;
                world.Tick(dt, move, Held(Keys.J), Held(Keys.K));
                camera = Vector2.Lerp(camera, CameraTarget(), 1 - MathF.Exp(-8 * dt));
                progress += (world.Clean - progress) * (1 - MathF.Exp(-7 * dt));
            }
        }
        else if (world.State == GameWorld.Phase.Paused)
        {
            if (Input.IsKeyDown(Keys.Escape) || Input.IsKeyDown(Keys.Enter) || Click(340, 342, 280, 50)) { world.State = GameWorld.Phase.Playing; Play("button_click"); }
            else if (Input.IsKeyDown(Keys.R) || Click(340, 410, 280, 50)) Start();
        }
        else if (Input.IsKeyDown(Keys.Enter) || Input.IsKeyDown(Keys.R) || Click(340, 432, 280, 56)) Start();
        bool activeVacuum = world.State == GameWorld.Phase.Playing && world.Suction && !muted;
        if (activeVacuum != vacuum) { if (activeVacuum) sounds["vacuum_loop"].Play(true); else sounds["vacuum_loop"].Stop(); vacuum = activeVacuum; }
        UpdateMusic(dt, !muted && world.State != GameWorld.Phase.Paused);
    }
    void UpdateMusic(float dt, bool enabled)
    {
        var music = sounds["bgm_clean_sweep"];
        float target = enabled ? (world.State == GameWorld.Phase.Playing ? .28f : .14f) : 0;
        if (enabled && musicPaused) { music.Resume(); musicPaused = false; }
        musicVolume += (target - musicVolume) * (1 - MathF.Exp(-10 * dt));
        if (!enabled && musicVolume < .001f)
        {
            musicVolume = 0;
            if (!musicPaused) { music.Pause(); musicPaused = true; }
        }
        music.SetVolume(musicVolume);
    }
    void Fill(float x, float y, float w, float h, Color4 c) { brush.Color = c; RenderTarget.FillRectangle(new Rect(x, y, w, h), brush); }
    void Text(string text, float x, float y, float w, G2Font? font = null, Color4? color = null) => (font ?? normal).DrawText(text, new Rect(x, y, w, 100), color ?? White);
    void Sprite(string key, float x, float y, float w, float h, float alpha = 1)
    {
        var size = sizes[key]; textures[key].Draw(new Rect(x, y, w, h), new Rect(0, 0, size.Width, size.Height), alpha, BitmapInterpolationMode.NearestNeighbor);
    }
    Vector2 Screen(Vector2 p) => p - camera + new Vector2(0, 90);
    void WorldSprite(string key, Vector2 p, float size, float alpha = 1) { var s = Screen(p); Sprite(key, s.X - size / 2, s.Y - size / 2, size, size, alpha); }
    void Shadow(Vector2 p, float w) { var s = Screen(p); Fill(s.X - w / 2, s.Y + 14, w, 8, new(0, 0, 0, .24f)); }
    protected override void Render()
    {
        DrawMap(); DrawHud();
        if (world.State == GameWorld.Phase.Title) DrawTitle();
        else if (world.State == GameWorld.Phase.Paused) DrawPause();
        else if (world.State is GameWorld.Phase.Won or GameWorld.Phase.Lost) DrawResult();
    }
    void DrawMap()
    {
        for (int y = Math.Max(0, (int)camera.Y / 64); y < Math.Min(19, (int)camera.Y / 64 + 10); y++)
        for (int x = Math.Max(0, (int)camera.X / 64); x < Math.Min(25, (int)camera.X / 64 + 16); x++)
        {
            string tile = ((x * 7 + y * 11) % 23) switch { 0 => "floor_crack", 1 => "floor_grate", 2 => "floor_speckle", _ => "floor_plain" };
            Sprite(tile, x * 64 - camera.X, y * 64 - camera.Y + 90, 64, 64);
        }
        var o = Screen(Vector2.Zero);
        Fill(o.X, o.Y, 1600, 28, new(.12f, .19f, .24f, 1)); Fill(o.X, o.Y + 1172, 1600, 28, new(.12f, .19f, .24f, 1));
        Fill(o.X, o.Y, 28, 1200, new(.12f, .19f, .24f, 1)); Fill(o.X + 1572, o.Y, 28, 1200, new(.12f, .19f, .24f, 1));
        foreach (var t in world.Litter) WorldSprite("trash_" + TrashNames[t.Kind], t.Position, 30);
        if (world.Suction)
        {
            var side = new Vector2(-world.Facing.Y, world.Facing.X);
            for (int i = 0; i < 13; i++)
            {
                float c = (uiTime * 2.6f + i * .173f) % 1;
                var s = Screen(world.Player + world.Facing * (110 - c * 85) + side * (MathF.Sin(i * 5.7f) * 34 * (1 - c)));
                Fill(s.X, s.Y, 4 + c * 3, 3, new(.45f, 1, .9f, .25f + c * .6f));
            }
            string direction = world.Facing.X < 0 ? "left_0" : world.Facing.X > 0 ? "right_1" : world.Facing.Y < 0 ? "up_0" : "down_0";
            WorldSprite("suction_" + direction, world.Player + world.Facing * 67, 90, .25f + .12f * MathF.Sin(uiTime * 23));
        }
        var actors = new List<(float Y, Action Draw)>();
        foreach (var b in world.Obstacles) { var box = b; actors.Add((b.Y + b.H, () => { var s = Screen(new(box.X, box.Y)); Fill(s.X + 6, s.Y + box.H - 4, box.W, 12, new(0, 0, 0, .2f)); Sprite(box.W == 64 ? "barrel" : "crate_hazard", s.X, s.Y, box.W, box.H); })); }
        foreach (var r in world.Robots) { var robot = r; actors.Add((r.Position.Y + 22, () => { Shadow(robot.Position, 42); WorldSprite(robot.Flash > 0 || robot.Health == 1 ? "robot_damaged" : "robot_normal", robot.Position + new Vector2(0, MathF.Sin(uiTime * 7 + robot.Position.X) * 1.3f), 58); var s = Screen(robot.Position); Fill(s.X - 18, s.Y - 35, 36, 4, new(.12f, .18f, .23f, 1)); Fill(s.X - 18, s.Y - 35, 12 * robot.Health, 4, Gold); })); }
        actors.Add((world.Player.Y + 20, DrawPlayer)); foreach (var actor in actors.OrderBy(a => a.Y)) actor.Draw();
        foreach (var b in world.Bullets) WorldSprite("trash_bullet_" + ((int)(uiTime * 15) % 3), b.Position, 25);
        foreach (var e in world.Effects)
        {
            if (e.Kind == "arrival")
            {
                var p = Screen(e.Position); float a = .35f + .3f * MathF.Sin(uiTime * 22);
                Fill(p.X - 35, p.Y + 25, 70, 5, new(1, .79f, .35f, a));
                Text("로봇 등장", p.X - 35, p.Y - 57, 110, small, Gold);
            }
            else if (e.Kind == "broken") WorldSprite("robot_broken", e.Position, 64, Math.Min(1, e.Life * 3));
            else WorldSprite(e.Kind + "_" + Math.Clamp((int)((1 - e.Life / e.Duration) * 4), 0, 3), e.Position, 65, Math.Min(1, e.Life * 6));
        }
        DrawMinimap(); Fill(0, 606, 960, 34, new(.035f, .065f, .09f, .98f));
        if (world.State == GameWorld.Phase.Playing) Text($"로봇 {world.Robots.Count} / {GameWorld.MaxRobots}   ·   다음 등장 {MathF.Ceiling(world.SpawnCountdown):0}초", 22, 105, 450, small, Gold);
        Text("W A S D  이동     J  흡입 (유지)     K  발사     ESC  일시정지", 22, 613, 720, small);
        Text(muted ? "M  소리 꺼짐" : "M  소리 켜짐", 816, 613, 140, small, Mint);
        if (world.State == GameWorld.Phase.Playing && world.Ammo == 0 && world.TimeLeft > 170) Text("J를 누른 채 쓰레기 쪽을 바라보세요", 280, 550, 500, normal, Gold);
    }
    void DrawPlayer()
    {
        Shadow(world.Player, 38);
        float bob = MathF.Sin(world.Walk) * Math.Min(3, world.Velocity.Length() / 70);
        float alpha = world.Invincible > 0 && (int)(world.Invincible * 15) % 2 == 0 ? .35f : 1;
        WorldSprite("player_down_suction", world.Player + new Vector2(0, bob), 62, alpha);
        var nozzle = Screen(world.Player + world.Facing * 25); Fill(nozzle.X - 6, nozzle.Y - 6, 12, 12, Mint);
        var tip = Screen(world.Player + world.Facing * 39); Fill(tip.X - 3, tip.Y - 3, 6, 6, Gold);
    }
    void DrawHud()
    {
        Fill(0, 0, 960, 90, new(.045f, .08f, .11f, 1)); Sprite("hud_panel", 12, 10, 936, 70);
        Text("CLEAN SWEEP", 28, 16, 190, small, Mint);
        for (int i = 0; i < 5; i++) Sprite(i < world.Health ? "icon_heart_full" : "icon_heart_empty", 28 + i * 28, 43, 24, 24);
        Text($"청소율  {world.Clean:0}% / 80%", 215, 18, 280, normal, world.Clean >= 70 ? Gold : White);
        Fill(216, 51, 278, 14, new(.055f, .1f, .13f, 1)); Fill(216, 51, 278 * progress / 100, 14, world.Clean >= 70 ? Gold : Mint);
        Fill(216 + 278 * .8f, 48, 2, 20, White); Sprite("icon_timer", 526, 28, 28, 28);
        int seconds = (int)MathF.Ceiling(world.TimeLeft);
        Text($"{seconds / 60:00}:{seconds % 60:00}", 566, 27, 110, large, seconds <= 30 ? Gold : White);
        Sprite("icon_ammo", 706, 28, 28, 28); Text($"{world.Ammo}", 746, 27, 115, large, world.EmptyFlash > 0 ? Gold : Mint);
        Button("Ⅱ", 884, 20, 48, 48);
    }
    void DrawMinimap()
    {
        const float x = 786, y = 456, scale = .09f;
        Fill(x - 6, y - 6, 156, 120, new(.04f, .07f, .1f, .88f));
        foreach (var b in world.Obstacles) Fill(x + b.X * scale, y + b.Y * scale, b.W * scale, b.H * scale, new(.35f, .45f, .5f, 1));
        foreach (var t in world.Litter) Fill(x + t.Position.X * scale, y + t.Position.Y * scale, 2, 2, Gold);
        foreach (var r in world.Robots) Fill(x + r.Position.X * scale - 2, y + r.Position.Y * scale - 2, 4, 4, new(1, .4f, .5f, 1));
        Fill(x + world.Player.X * scale - 3, y + world.Player.Y * scale - 3, 6, 6, Mint); Text("창고 지도", x, y - 29, 140, small, Mint);
    }
    void Panel()
    {
        Fill(0, 90, 960, 516, new(.015f, .035f, .055f, .82f)); Fill(180, 146, 600, 396, new(.055f, .1f, .135f, .98f)); Fill(180, 146, 600, 4, Mint);
    }
    void Button(string label, float x, float y, float w, float h)
    {
        bool hot = Hover(x, y, w, h); Fill(x, y + 4, w, h, new(.02f, .04f, .06f, 1)); Sprite("button_blank", x, y, w, h, hot ? 1 : .8f);
        if (hot) Fill(x + 3, y + 3, w - 6, h - 6, new(.3f, 1, .8f, .12f));
        brush.Color = hot ? Mint : White; RenderTarget.DrawText(label, buttonFormat, new Rect(x, y, w, h), brush);
    }
    void DrawTitle()
    {
        Panel(); Text("WAREHOUSE  /  01", 248, 175, 480, small, Mint); Text("청소기 대소동", 247, 213, 520, title);
        Text("쓰레기를 흡입하고, 고장 난 로봇을 멈추세요.", 249, 290, 490); Text("3분 안에 청소율 80% 달성", 249, 328, 490, large, Mint);
        Text("WASD 이동   ·   J 흡입   ·   K 발사\n흡입한 쓰레기가 탄약이 됩니다.", 249, 380, 490, small); Button("청소 시작    ENTER", 340, 432, 280, 56);
    }
    void DrawPause()
    {
        Panel(); Text("잠시 쉬어가기", 287, 211, 460, large); Text("계속하면 멈춘 지점에서 이어집니다.", 287, 274, 450);
        Button("계속하기    ESC", 340, 342, 280, 50); Button("처음부터    R", 340, 410, 280, 50);
    }
    void DrawResult()
    {
        Panel(); bool win = world.State == GameWorld.Phase.Won; Text(win ? "MISSION COMPLETE" : "TRY AGAIN", 249, 178, 500, small, Mint);
        Text(win ? "창고 청소 완료!" : "다시 도전해 볼까요?", 249, 222, 520, large, win ? Mint : Gold);
        Text(win ? "덕분에 창고가 다시 깨끗해졌어요." : world.Health <= 0 ? "체력이 모두 소진되었습니다." : "제한 시간이 끝났습니다.", 249, 278, 500);
        Text($"청소율       {world.Clean:0}%\n플레이 시간   {180 - world.TimeLeft:0}초\n정지한 로봇   {world.Destroyed}대", 290, 326, 420); Button("다시 시작    ENTER", 340, 432, 280, 56);
    }
    public override void Dispose()
    {
        world.Sound -= Play; foreach (var s in sounds.Values) s.Dispose(); foreach (var t in textures.Values) t.Dispose();
        small?.Dispose(); normal?.Dispose(); large?.Dispose(); title?.Dispose(); brush?.Dispose(); buttonFormat?.Dispose(); base.Dispose();
    }
}
