using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using KingsLastCastle.Rendering;

namespace KingsLastCastle;

public sealed class MilestoneGame : Game
{
    // Team-Aufteilung:
    // Aziz: Spielablauf, Game-State, Wellenstart, Gold/HP, HUD, Start/Restart sowie Sieg-/Niederlage-Logik.
    // Jihed: Gegner-System, Spawn-Logik, Pfadbewegung, Gegnerabstand, Schloss-Schaden und prozedurale Spielwelt.
    // Rayan: Tower-System, Bauplätze, Tower-Kampf, Balancing, Schuss-Effekte sowie Kamera-/3D-Modell-Darstellung.
    // Integration, Testing und Feinanpassungen wurden gemeinsam durchgeführt.

    //  Diese Enums halten die wichtigsten Spielkategorien klein und gut vergleichbar
    private enum EnemyType { Standard, Heavy, Flying }
    private enum TowerType { Arrow, Cannon, Magic }
    private enum GamePhase { Preparation, WaveActive, Victory, Defeat }

    //  Ein Enemy speichert nur die Werte, die für Bewegung, Darstellung und Kampf gebraucht werden
    private sealed class Enemy
    {
        public EnemyType Type;
        public Vector3 Position;
        public float RotationY;
        public float Progress;
        public float TimeAlive;
        public float Speed;
        public float Scale;
        public int Health;
        public int Reward;
        public int CastleDamage;
    }

    //  Ein Tower kennt seinen Bauplatz, seine Kampfwerte und seinen Cooldown-Timer.
    private sealed class Tower
    {
        public TowerType Type;
        public int SpotIndex;
        public Vector3 Position;
        public float Range;
        public float Cooldown;
        public float Timer;
        public int Damage;
    }

    //  Shots sind kurze visuelle Effekte für Pfeile, Kanonen-Schüsse und Laser.
    private sealed class Shot
    {
        public Vector3 Start;
        public Vector3 End;
        public Vector3 Position;
        public float Age;
        public Color Color;
        public bool IsLaser;
    }

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private SpriteFont? _font;
    private Texture2D? _pixel;
    private PrimitiveRenderer? _renderer;
    private Model? _castleModel;
    private Model? _arrowTowerModel;
    private Model? _cannonTowerModel;
    private Model? _magicTowerModel;
    private Model? _standardEnemyModel;
    private Model? _heavyEnemyModel;
    private Model? _flyingEnemyModel;

    private Matrix _view;
    private Matrix _projection;
    private readonly List<Enemy> _enemies = new();
    private readonly List<Tower> _towers = new();
    private readonly List<Shot> _shots = new();

    private const float SpawnDelay = 1.20f;
    private const float EnemySpacing = 1.00f;
    private static readonly Vector3 CastlePosition = new(10.45f, 0f, 3.65f);

    //  Die Wave-Liste bestimmt Reihenfolge und Mischung der Gegner im aktuellen Meilenstein.
    private readonly EnemyType[][] _waves =
    {
        new[]
        {
            EnemyType.Standard, EnemyType.Standard, EnemyType.Flying, EnemyType.Standard,
            EnemyType.Heavy, EnemyType.Standard, EnemyType.Flying, EnemyType.Standard,
            EnemyType.Heavy, EnemyType.Flying, EnemyType.Standard, EnemyType.Heavy,
            EnemyType.Standard, EnemyType.Flying, EnemyType.Heavy, EnemyType.Heavy,
            EnemyType.Flying, EnemyType.Standard, EnemyType.Heavy, EnemyType.Flying,
            EnemyType.Standard, EnemyType.Heavy, EnemyType.Flying, EnemyType.Standard,
            EnemyType.Flying, EnemyType.Heavy
        }
    };

    //  Der Pfad besteht aus Waypoints; Gegner bewegen sich Segment für Segment darüber.
    private readonly List<Vector3> _path = new()
    {
        new(-10.65f, 0f, -6.40f),
        new(-8.55f, 0f, -6.15f),
        new(-6.75f, 0f, -4.45f),
        new(-7.05f, 0f, -2.15f),
        new(-4.75f, 0f, -0.55f),
        new(-2.20f, 0f, -1.75f),
        new(0.35f, 0f, -3.05f),
        new(2.95f, 0f, -2.05f),
        new(3.75f, 0f, 0.55f),
        new(5.45f, 0f, 2.20f),
        new(7.70f, 0f, 2.85f),
        new(9.05f, 0f, 3.18f)
    };

    //  Tower dürfen nur auf diesen festen Bauplätzen gesetzt werden.
    private readonly List<Vector3> _towerSpots = new()
    {
        new(-9.35f, 0f, -4.75f),
        new(-6.15f, 0f, -6.60f),
        new(-5.85f, 0f, 0.95f),
        new(-3.45f, 0f, -3.35f),
        new(-0.75f, 0f, -0.65f),
        new(1.60f, 0f, -4.30f),
        new(2.40f, 0f, 0.40f),
        new(4.95f, 0f, -0.55f),
        new(5.95f, 0f, 3.75f),
        new(8.05f, 0f, 1.15f)
    };

    //  Die Bäume sind reine Deko-Positionen für die schwebende Insel.
    private readonly Vector3[] _trees =
    {
        new(-10.8f, 0f, 0.0f), new(-10.2f, 0f, 3.1f), new(-8.4f, 0f, 6.4f),
        new(-5.4f, 0f, 8.9f), new(-2.0f, 0f, 10.1f), new(1.8f, 0f, 10.2f),
        new(5.4f, 0f, 8.8f), new(8.4f, 0f, 6.2f), new(10.2f, 0f, 2.3f),
        new(10.4f, 0f, -1.7f), new(8.7f, 0f, -5.4f), new(5.5f, 0f, -8.1f),
        new(1.5f, 0f, -10.0f), new(-2.8f, 0f, -9.9f)
    };

    private int _gold = 195;
    private int _castleHp = 18;
    private int _currentWave;
    private int _nextEnemy;
    private int _selectedSpot;
    private float _spawnTimer;
    private TowerType _selectedTower = TowerType.Arrow;
    private GamePhase _phase = GamePhase.Preparation;
    private KeyboardState _oldKeyboard;
    private MouseState _oldMouse;
    private string _message = "Build a mixed defense, then start the long wave.";

    public MilestoneGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            GraphicsProfile = GraphicsProfile.HiDef,
            PreferMultiSampling = true,
            SynchronizeWithVerticalRetrace = true
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "King's Last Castle - Meilenstein 1";
        Window.AllowUserResizing = true;
    }

    protected override void LoadContent()
    {
        // Aziz, Jihed und Rayan: Hier werden alle gemeinsam benötigten 3D-Modelle und Hilfsressourcen für die Spielszene geladen.
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("Default");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _renderer = new PrimitiveRenderer(GraphicsDevice);

        _castleModel = Content.Load<Model>("Models/kings_castle");
        _arrowTowerModel = Content.Load<Model>("Models/arrow_Tower");
        _cannonTowerModel = Content.Load<Model>("Models/cannon_Tower");
        _magicTowerModel = Content.Load<Model>("Models/magic_Tower");
        _standardEnemyModel = Content.Load<Model>("Models/standard_enemy");
        _heavyEnemyModel = Content.Load<Model>("Models/heavy_enemy");
        _flyingEnemyModel = Content.Load<Model>("Models/flying_enemy");
    }

    protected override void Update(GameTime gameTime)
    {
        // Aziz: Hier wird die zentrale Spielschleife für Wellenphase, Sieg und Niederlage gesteuert.
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();

        //  In jedem Frame werden zuerst Kamera und Spieler-Eingabe aktualisiert.
        UpdateCamera();
        HandleInput(keyboard, mouse);

        //  Gegner und Tower werden nur während einer aktiven Welle simuliert.
        if (_phase == GamePhase.WaveActive)
        {
            UpdateEnemies(gameTime);
            UpdateTowers(gameTime);
            UpdateShots(gameTime);

            if (_castleHp <= 0)
            {
                //  Wenn die Schlossleben auf 0 fallen, endet die Runde als Niederlage.
                _phase = GamePhase.Defeat;
                _message = "The castle has fallen.";
            }
            else if (_nextEnemy >= _waves[_currentWave].Length && _enemies.Count == 0)
            {
                //  Die Welle ist geschafft, sobald kein Gegner mehr gespawnt wird und keiner mehr lebt.
                if (_currentWave == _waves.Length - 1)
                {
                    _phase = GamePhase.Victory;
                    _message = "All waves defeated!";
                }
                else
                {
                    _currentWave++;
                    _nextEnemy = 0;
                    _spawnTimer = 0f;
                    _phase = GamePhase.Preparation;
                    _message = "Wave cleared. Build more towers.";
                }
            }
        }

        _oldKeyboard = keyboard;
        _oldMouse = mouse;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // Aziz, Jihed und Rayan: Hier wird die komplette Szene aus Welt, Gegnern, Türmen und HUD zusammengesetzt.
        GraphicsDevice.Clear(new Color(124, 195, 232));
        DrawSky();

        //  Für die 3D-Szene werden Depth-Buffer und undurchsichtige Darstellung aktiviert.
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.BlendState = BlendState.Opaque;

        //  Die Draw-Reihenfolge geht von Umgebung über Gameplay-Objekte bis zum HUD.
        _renderer!.Begin(_view, _projection);
        DrawIsland();
        _renderer.DrawPath(_path, 1.15f, new Color(96, 72, 48), 0.05f);
        _renderer.DrawPath(_path, 0.82f, new Color(190, 151, 95), 0.08f);
        DrawCastleBase();
        DrawTowerPads();
        DrawEnemies();
        DrawTowers();
        DrawShots();
        DrawModel(_castleModel, CastlePosition, 0.105f, 0f, MathHelper.ToRadians(250f), 0f, true);
        DrawHud();

        base.Draw(gameTime);
    }

    private void UpdateCamera()
    {
        // Rayan: Hier werden die feste 3D-Kamera und Perspektive für eine klare Spielübersicht festgelegt.
        var viewport = GraphicsDevice.Viewport;
        _view = Matrix.CreateLookAt(new Vector3(0.6f, 13.8f, 26.4f), new Vector3(1.0f, -0.75f, -0.45f), Vector3.Up);
        _projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(39f), Math.Max(0.1f, viewport.AspectRatio), 0.1f, 100f);
    }

    private void UpdateEnemies(GameTime gameTime)
    {
        // Jihed: Hier werden Gegner-Spawns, Pfadbewegung, Abstand auf dem Weg und Schaden am Schloss gesteuert.
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _spawnTimer -= dt;

        //  Der Spawn-Timer sorgt dafür, dass Gegner nacheinander und nicht alle gleichzeitig kommen.
        if (_nextEnemy < _waves[_currentWave].Length && _spawnTimer <= 0f)
        {
            SpawnEnemy(_waves[_currentWave][_nextEnemy++]);
            _spawnTimer = SpawnDelay;
        }

        float pathLength = GetPathLength();
        for (int i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            enemy.TimeAlive += dt;
            float progress = enemy.Progress + enemy.Speed * dt;
            var blocker = FindEnemyAheadOnSameLane(i);
            //  Gegner bleiben auf Abstand, damit sie nicht komplett ineinander laufen.
            if (blocker != null) progress = Math.Min(progress, blocker.Progress - EnemySpacing);
            enemy.Progress = Math.Max(0f, progress);
            enemy.Position = GetPositionOnPath(enemy.Progress, out enemy.RotationY);
        }

        //  Erreicht ein Gegner das Ende des Pfads, verliert das Schloss Lebenspunkte.
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            if (_enemies[i].Progress < pathLength) continue;
            _castleHp = Math.Max(0, _castleHp - _enemies[i].CastleDamage);
            _enemies.RemoveAt(i);
        }
    }

    private void SpawnEnemy(EnemyType type)
    {
        // Jihed: Hier werden Werte der Gegnertypen wie Geschwindigkeit, Lebenspunkte, Belohnung und Schaden definiert.
        //  Heavy ist langsam und robust, Flying ist schnell, Standard ist der Basisgegner.
        var data = type switch
        {
            EnemyType.Heavy => (Speed: 0.92f, Scale: 0.0075f, Health: 125, Reward: 32, Damage: 4),
            EnemyType.Flying => (Speed: 1.65f, Scale: 0.0060f, Health: 48, Reward: 24, Damage: 2),
            _ => (Speed: 1.15f, Scale: 0.0060f, Health: 62, Reward: 18, Damage: 1)
        };

        _enemies.Add(new Enemy
        {
            Type = type,
            Position = _path[0],
            Speed = data.Speed,
            Scale = data.Scale,
            Health = data.Health,
            Reward = data.Reward,
            CastleDamage = data.Damage
        });
    }

    private Enemy? FindEnemyAheadOnSameLane(int enemyIndex)
    {
        // Jihed: Hier wird sichtbares Durchlaufen von Gegnern auf derselben Bewegungs-Ebene verhindert.
        var enemy = _enemies[enemyIndex];
        for (int i = enemyIndex - 1; i >= 0; i--)
        {
            //  Fluggegner vergleichen sich nur mit Fluggegnern, Bodengegner nur mit Bodengegnern.
            bool sameLane = enemy.Type == EnemyType.Flying
                ? _enemies[i].Type == EnemyType.Flying
                : _enemies[i].Type != EnemyType.Flying;

            if (sameLane) return _enemies[i];
        }

        return null;
    }

    private float GetPathLength()
    {
        float length = 0f;
        for (int i = 0; i < _path.Count - 1; i++) length += Vector3.Distance(_path[i], _path[i + 1]);
        return length;
    }

    private Vector3 GetPositionOnPath(float progress, out float rotationY)
    {
        // Jihed: Diese Methode wandelt die Strecke auf dem Pfad in eine echte 3D-Position um.
        for (int i = 0; i < _path.Count - 1; i++)
        {
            var start = _path[i];
            var segment = _path[i + 1] - start;
            float length = segment.Length();
            if (progress > length)
            {
                progress -= length;
                continue;
            }

            var direction = Vector3.Normalize(segment);
            //  RotationY dreht das Modell in Bewegungsrichtung des aktuellen Pfadsegments.
            rotationY = MathF.Atan2(direction.X, direction.Z);
            return start + direction * progress;
        }

        var finalDirection = Vector3.Normalize(_path[^1] - _path[^2]);
        rotationY = MathF.Atan2(finalDirection.X, finalDirection.Z);
        return _path[^1];
    }

    private void HandleInput(KeyboardState keyboard, MouseState mouse)
    {
        // Aziz: Hier werden Tastatur und Maus für Turmauswahl, Bauplätze, Wellenstart und Neustart ausgewertet.
        //  Die Zahlentasten wechseln den Tower-Typ.
        if (Pressed(keyboard, Keys.D1)) _selectedTower = TowerType.Arrow;
        if (Pressed(keyboard, Keys.D2)) _selectedTower = TowerType.Cannon;
        if (Pressed(keyboard, Keys.D3)) _selectedTower = TowerType.Magic;

        //  Während der Vorbereitung kann gebaut oder die Welle gestartet werden.
        if (_phase == GamePhase.Preparation)
        {
            int spot = SpotUnderMouse(mouse.X, mouse.Y);
            if (spot >= 0) _selectedSpot = spot;

            if (LeftClicked(mouse))
            {
                if (StartButton().Contains(mouse.X, mouse.Y)) StartWave();
                else if (spot >= 0) BuildTower();
            }
        }

        if ((_phase == GamePhase.Victory || _phase == GamePhase.Defeat) && Pressed(keyboard, Keys.R)) Restart();
    }

    private bool Pressed(KeyboardState keyboard, Keys key) => keyboard.IsKeyDown(key) && !_oldKeyboard.IsKeyDown(key);
    private bool LeftClicked(MouseState mouse) => mouse.LeftButton == ButtonState.Pressed && _oldMouse.LeftButton == ButtonState.Released;

    private void StartWave()
    {
        // Aziz: Hier wird die Kampfphase erst gestartet, wenn der Spieler mindestens einen Tower gebaut hat.
        if (_towers.Count == 0)
        {
            _message = "Build at least one tower first.";
            return;
        }

        _phase = GamePhase.WaveActive;
        _message = "The long enemy wave has started.";
    }

    private int SpotUnderMouse(int x, int y)
    {
        //  Die 3D-Bauplätze werden auf den Bildschirm projiziert, damit die Maus sie anklicken kann.
        int best = -1;
        float bestDistance = 34f;
        var mousePosition = new Vector2(x, y);

        for (int i = 0; i < _towerSpots.Count; i++)
        {
            if (SpotOccupied(i)) continue;

            var screen = GraphicsDevice.Viewport.Project(_towerSpots[i] + new Vector3(0f, 0.2f, 0f), _projection, _view, Matrix.Identity);
            if (screen.Z < 0f || screen.Z > 1f) continue;

            float distance = Vector2.Distance(mousePosition, new Vector2(screen.X, screen.Y));
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void BuildTower()
    {
        // Rayan: Hier werden Bauplatz und Gold geprüft, der ausgewählte Tower erstellt und die Auswahl aktualisiert.
        //  Erst wird geprüft, ob der Platz frei ist und ob genug Gold vorhanden ist.
        if (SpotOccupied(_selectedSpot))
        {
            _message = "This pad is already occupied.";
            return;
        }

        var stats = TowerStats(_selectedTower);
        if (_gold < stats.Cost)
        {
            _message = $"Not enough gold. Need {stats.Cost}.";
            return;
        }

        _gold -= stats.Cost;
        //  Der Tower übernimmt Werte aus TowerStats und wird dann in die aktive Tower-Liste eingefügt.
        _towers.Add(new Tower
        {
            Type = _selectedTower,
            SpotIndex = _selectedSpot,
            Position = _towerSpots[_selectedSpot],
            Range = stats.Range,
            Cooldown = stats.Cooldown,
            Damage = stats.Damage
        });
        _message = $"{_selectedTower} tower built.";
        SelectNextSpot();
    }

    private void SelectNextSpot()
    {
        //  Nach dem Bauen springt die Auswahl automatisch zum nächsten freien Bauplatz.
        for (int i = 0; i < _towerSpots.Count; i++)
        {
            _selectedSpot = (_selectedSpot + 1) % _towerSpots.Count;
            if (!SpotOccupied(_selectedSpot)) return;
        }
    }

    private bool SpotOccupied(int index)
    {
        //  Ein Bauplatz ist belegt, sobald ein Tower denselben SpotIndex besitzt.
        foreach (var tower in _towers)
        {
            if (tower.SpotIndex == index) return true;
        }
        return false;
    }

    private void Restart()
    {
        // Aziz: Hier wird das Spiel nach Sieg oder Niederlage wieder auf den Startzustand zurückgesetzt.
        _enemies.Clear();
        _towers.Clear();
        _shots.Clear();
        _gold = 195;
        _castleHp = 18;
        _currentWave = 0;
        _nextEnemy = 0;
        _selectedSpot = 0;
        _spawnTimer = 0f;
        _phase = GamePhase.Preparation;
        _message = "Build a mixed defense, then start the long wave.";
    }

    // Aziz und Rayan: Hier werden die Balancing-Werte der Tower-Typen für Reichweite, Feuerrate, Schaden und Kosten festgelegt.
    private (float Range, float Cooldown, int Damage, int Cost) TowerStats(TowerType type) => type switch
    {
        TowerType.Arrow => (4.4f, 0.72f, 12, 50),
        TowerType.Cannon => (3.8f, 1.45f, 32, 75),
        TowerType.Magic => (5.0f, 1.05f, 24, 70),
        _ => (4f, 1f, 10, 50)
    };

    private void UpdateTowers(GameTime gameTime)
    {
        // Rayan: Hier suchen die Türme Ziele, verursachen Schaden und lösen Schüsse aus.
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        foreach (var tower in _towers)
        {
            //  Der Timer verhindert, dass ein Tower in jedem Frame schießt.
            tower.Timer -= dt;
            if (tower.Timer > 0f) continue;

            var target = FindTarget(tower);
            if (target == null) continue;

            //  Erst wird Schaden berechnet, danach wird nur der sichtbare Schuss-Effekt erzeugt.
            HitEnemy(tower, target);
            tower.Timer = tower.Cooldown;
            AddShot(tower, target);
            RemoveDeadEnemies();
        }
    }

    private void HitEnemy(Tower tower, Enemy target)
    {
        // Rayan: Hier werden Einzelschaden, Flächenschaden und die Sonderregel gegen Fluggegner behandelt.
        if (tower.Type == TowerType.Cannon)
        {
            //  Der Cannon Tower trifft mehrere Bodengegner im Radius, aber keine Fluggegner.
            foreach (var enemy in _enemies)
            {
                if (enemy.Type != EnemyType.Flying && Vector3.Distance(enemy.Position, target.Position) <= 1.45f) enemy.Health -= tower.Damage;
            }
            return;
        }

        target.Health -= tower.Damage;
    }

    private void RemoveDeadEnemies()
    {
        //  Aziz: Tote Gegner werden entfernt und geben dem Spieler Gold als Belohnung.
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            if (_enemies[i].Health > 0) continue;
            _gold += _enemies[i].Reward;
            _enemies.RemoveAt(i);
        }
    }

    private Enemy? FindTarget(Tower tower)
    {
        // Rayan: Hier wird innerhalb der Reichweite der Gegner ausgewählt, der dem Schloss am nächsten ist.
        Enemy? best = null;
        foreach (var enemy in _enemies)
        {
            //  Cannon ignoriert Fluggegner, weil dieser Tower nur Bodenziele trifft.
            if (enemy.Type == EnemyType.Flying && tower.Type == TowerType.Cannon) continue;
            if (Vector3.Distance(tower.Position, enemy.Position) > tower.Range) continue;
            //  Höherer Progress bedeutet, dass der Gegner weiter vorne auf dem Pfad ist.
            if (best == null || enemy.Progress > best.Progress) best = enemy;
        }
        return best;
    }

    private void AddShot(Tower tower, Enemy target)
    {
        // Rayan: Hier werden sichtbare Projektile oder Laser passend zum jeweiligen Tower erzeugt.
        //  Die Farbe des Effekts macht schnell sichtbar, welcher Tower geschossen hat.
        var color = tower.Type switch
        {
            TowerType.Arrow => new Color(235, 210, 120),
            TowerType.Cannon => new Color(80, 80, 80),
            _ => new Color(110, 220, 255)
        };

        _shots.Add(new Shot
        {
            Start = tower.Position + new Vector3(0f, 1.1f, 0f),
            End = target.Position + new Vector3(0f, target.Type == EnemyType.Flying ? 1.5f : 0.7f, 0f),
            Position = tower.Position + new Vector3(0f, 1.1f, 0f),
            Color = color,
            IsLaser = tower.Type == TowerType.Magic
        });
    }

    private void UpdateShots(GameTime gameTime)
    {
        //  Schüsse leben nur sehr kurz und bewegen sich per Lerp vom Tower zum Ziel.
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        for (int i = _shots.Count - 1; i >= 0; i--)
        {
            _shots[i].Age += dt;
            float t = _shots[i].Age / 0.18f;
            if (t >= 1f) _shots.RemoveAt(i);
            else _shots[i].Position = Vector3.Lerp(_shots[i].Start, _shots[i].End, t);
        }
    }

    private void DrawSky()
    {
        // Jihed: Der Himmel wird als einfacher vertikaler Farbverlauf mit SpriteBatch gezeichnet.
        if (_spriteBatch == null || _pixel == null) return;

        var viewport = GraphicsDevice.Viewport;
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        for (int i = 0; i < 12; i++)
        {
            float t = i / 11f;
            var color = Color.Lerp(new Color(82, 165, 224), new Color(213, 238, 244), t);
            _spriteBatch.Draw(_pixel, new Rectangle(0, i * viewport.Height / 12, viewport.Width, viewport.Height / 12 + 2), color);
        }
        _spriteBatch.End();
    }

    private void DrawIsland()
    {
        // Jihed: Hier werden Inselbasis, Wolken, Steine und die Umrandung gezeichnet.
        //  Die Umgebung besteht aus einfachen Formen aus PrimitiveRenderer und nicht aus einem großen Modell.
        DrawCloud(new Vector3(-8.5f, -5.4f, 5.8f), 1.2f);
        DrawCloud(new Vector3(7.2f, -5.1f, 4.6f), 1.0f);
        DrawCloud(new Vector3(0.8f, -6.0f, -7.5f), 1.4f);
        DrawFloatingStone(new Vector3(-7.8f, -2.5f, 4.3f), 0.7f);
        DrawFloatingStone(new Vector3(6.8f, -3.1f, -5.0f), 0.55f);
        DrawFloatingStone(new Vector3(1.0f, -4.0f, -8.0f), 0.45f);

        _renderer!.DrawCone(new Vector3(0f, -0.95f, 0f), 10.2f, new Vector3(0f, -7.3f, 0.8f), 64, new Color(73, 60, 53));
        _renderer.DrawCone(new Vector3(-3.4f, -0.85f, 2.3f), 4.0f, new Vector3(-3.8f, -6.3f, 1.5f), 32, new Color(88, 71, 58));
        _renderer.DrawCone(new Vector3(4.6f, -0.90f, -2.8f), 3.6f, new Vector3(4.2f, -5.9f, -1.8f), 32, new Color(87, 70, 58));
        _renderer.DrawCylinder(new Vector3(0f, -0.63f, 0f), 11.2f, 0.55f, 64, new Color(118, 91, 67));
        _renderer.DrawCylinder(new Vector3(0f, -0.23f, 0f), 11.6f, 0.34f, 64, new Color(90, 169, 99));
        _renderer.DrawCylinder(new Vector3(0f, -0.02f, 0f), 10.9f, 0.08f, 64, new Color(118, 195, 116));
        DrawWaterfall(new Vector3(-9.8f, -1.8f, 3.0f));

        foreach (var tree in _trees) DrawTree(tree, 0.7f);
    }

    private void DrawCloud(Vector3 p, float s)
    {
        var color = new Color(215, 236, 242);
        _renderer!.DrawCylinder(p, 0.95f * s, 0.12f * s, 18, color);
        _renderer.DrawCylinder(p + new Vector3(0.75f * s, 0.05f, 0.15f * s), 0.65f * s, 0.10f * s, 18, color);
        _renderer.DrawCylinder(p + new Vector3(-0.65f * s, 0.03f, -0.05f * s), 0.70f * s, 0.10f * s, 18, color);
    }

    private void DrawWaterfall(Vector3 p)
    {
        _renderer!.DrawCylinder(p, 0.16f, 3.2f, 12, new Color(138, 213, 230));
        _renderer.DrawCylinder(p + new Vector3(0.05f, -2.0f, 0.05f), 0.42f, 0.10f, 18, new Color(210, 236, 240));
    }

    private void DrawTree(Vector3 p, float s)
    {
        _renderer!.DrawCylinder(p + new Vector3(0f, 0.25f * s, 0f), 0.10f * s, 0.50f * s, 8, new Color(101, 70, 45));
        _renderer.DrawCone(p + new Vector3(0f, 0.48f * s, 0f), 0.50f * s, p + new Vector3(0f, 1.45f * s, 0f), 10, new Color(55, 137, 89));
        _renderer.DrawCone(p + new Vector3(0f, 0.88f * s, 0f), 0.38f * s, p + new Vector3(0f, 1.75f * s, 0f), 10, new Color(85, 166, 100));
    }

    private void DrawFloatingStone(Vector3 p, float s)
    {
        _renderer!.DrawCylinder(p, 0.55f * s, 0.20f * s, 7, new Color(106, 96, 87));
        _renderer.DrawCone(p, 0.62f * s, p + new Vector3(0f, -0.95f * s, 0f), 7, new Color(80, 71, 65));
    }

    private void DrawCastleBase()
    {
        _renderer!.DrawCylinder(CastlePosition, 2.05f, 0.16f, 36, new Color(124, 132, 126));
        _renderer.DrawCylinder(_path[^1] + new Vector3(0f, 0.08f, 0f), 0.62f, 0.08f, 20, new Color(190, 151, 95));
    }

    private void DrawTowerPads()
    {
        // Freie Bauplätze werden als Pads angezeigt; der ausgewählte Platz ist heller/größer.
        for (int i = 0; i < _towerSpots.Count; i++)
        {
            if (SpotOccupied(i)) continue;
            bool selected = i == _selectedSpot && _phase == GamePhase.Preparation;
            var color = selected ? new Color(100, 180, 200) : new Color(80, 112, 125);
            _renderer!.DrawCylinder(_towerSpots[i] + new Vector3(0f, 0.07f, 0f), selected ? 0.50f : 0.42f, 0.12f, 18, color);
        }
    }

    private void DrawEnemies()
    {
        // Jihed: Hier werden Gegner auf dem Pfad gezeichnet; Fluggegner werden höher über dem Boden dargestellt.
        foreach (var enemy in _enemies)
        {
            var p = enemy.Position + new Vector3(0f, enemy.Type == EnemyType.Flying ? 1.15f : 0.05f, 0f);
            float z = enemy.Type == EnemyType.Flying ? MathF.Sin(enemy.TimeAlive * 7f) * MathHelper.ToRadians(8f) : 0f;
            DrawModel(GetEnemyModel(enemy.Type), p, enemy.Scale, MathHelper.ToRadians(90f), enemy.RotationY, z, false);
        }
    }

    private void DrawTowers()
    {
        // Rayan: Hier werden die Türme als Modelle und Grundkörper in die Szene gesetzt.
        foreach (var tower in _towers)
        {
            _renderer!.DrawCylinder(tower.Position + new Vector3(0f, 0.07f, 0f), 0.56f, 0.14f, 20, new Color(86, 94, 101));
            DrawModel(GetTowerModel(tower.Type), tower.Position + new Vector3(0f, 0.08f, 0f), 0.00020f, MathHelper.ToRadians(90f), MathHelper.ToRadians(90f), 0f, false);
        }
    }

    private void DrawShots()
    {
        // Rayan: Laser werden als Strahl gezeichnet, andere Schüsse als kleine Zylinder-Projektile.
        foreach (var shot in _shots)
        {
            if (shot.IsLaser) _renderer!.DrawBeam(shot.Start, shot.End, 0.055f, shot.Color);
            else _renderer!.DrawCylinder(shot.Position, 0.07f, 0.12f, 10, shot.Color);
        }
    }

    private void DrawHud()
    {
        // Aziz: Hier werden im HUD Spielstatus, Gold, Schlossleben, Welle und aktuelle Meldungen angezeigt.
        //  Das HUD wird zuletzt ohne Depth-Buffer gezeichnet, damit es immer vor der 3D-Szene liegt.
        if (_spriteBatch == null || _font == null || _pixel == null) return;

        GraphicsDevice.DepthStencilState = DepthStencilState.None;
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        Panel(new Rectangle(18, 18, 270, 142));
        Text("King's Last Castle", 34, 30, Color.White, 1.1f);
        Text($"Castle HP: {_castleHp}", 34, 66, new Color(205, 240, 205));
        Text($"Gold: {_gold}", 34, 94, new Color(245, 210, 100));
        Text($"Wave: {_currentWave + 1}/{_waves.Length}", 34, 122, new Color(180, 225, 240));

        Panel(new Rectangle(18, GraphicsDevice.Viewport.Height - 78, 900, 54));
        Text(_message, 34, GraphicsDevice.Viewport.Height - 66, new Color(255, 230, 140));
        Text($"1/2/3 tower: Arrow anti-air, Cannon splash, Magic laser  |  left click pad to build  |  Selected: {_selectedTower}", 34, GraphicsDevice.Viewport.Height - 42, Color.White, 0.75f);
        if (_phase == GamePhase.Preparation) DrawStartButton();

        if (_phase == GamePhase.Defeat) DrawCenteredOverlayText("DEFEAT - Press R", Color.Red, 1.4f, -24f);
        if (_phase == GamePhase.Victory) DrawCenteredOverlayText("VICTORY - Press R", Color.Yellow, 1.4f, -24f);
        _spriteBatch.End();
    }

    private void DrawCenteredOverlayText(string text, Color color, float scale, float yOffset)
    {
        var size = _font!.MeasureString(text) * scale;
        var viewport = GraphicsDevice.Viewport;
        var x = (viewport.Width - size.X) * 0.5f;
        var y = (viewport.Height - size.Y) * 0.5f + yOffset;
        Text(text, (int)x, (int)y, color, scale);
    }

    private Rectangle StartButton() => new(GraphicsDevice.Viewport.Width - 220, GraphicsDevice.Viewport.Height - 78, 190, 54);

    private void DrawStartButton()
    {
        // Aziz: Der Startbutton wird nur in der Vorbereitungsphase angezeigt und per Maus geprüft.
        var r = StartButton();
        var color = _towers.Count == 0 ? new Color(85, 100, 104, 220) : new Color(70, 145, 94, 230);
        Rect(r, color);
        Rect(new Rectangle(r.X, r.Y, r.Width, 2), new Color(225, 220, 150));
        Rect(new Rectangle(r.X, r.Bottom - 2, r.Width, 2), new Color(30, 70, 50));
        Rect(new Rectangle(r.X, r.Y, 2, r.Height), new Color(225, 220, 150));
        Rect(new Rectangle(r.Right - 2, r.Y, 2, r.Height), new Color(30, 70, 50));
        Text("START WAVE", r.X + 28, r.Y + 16, Color.White, 1f);
    }

    private void Panel(Rectangle r)
    {
        // Aziz: Panels sind dunkle HUD-Flächen mit einfachem Rahmen für bessere Lesbarkeit.
        Rect(r, new Color(20, 32, 38, 205));
        Rect(new Rectangle(r.X, r.Y, r.Width, 2), new Color(225, 200, 130));
        Rect(new Rectangle(r.X, r.Bottom - 2, r.Width, 2), new Color(90, 120, 130));
        Rect(new Rectangle(r.X, r.Y, 2, r.Height), new Color(225, 200, 130));
        Rect(new Rectangle(r.Right - 2, r.Y, 2, r.Height), new Color(90, 120, 130));
    }

    private void Rect(Rectangle r, Color color) => _spriteBatch!.Draw(_pixel!, r, color);
    private void Text(string text, int x, int y, Color color, float scale = 1f) => _spriteBatch!.DrawString(_font!, text, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

    private Model? GetEnemyModel(EnemyType type) => type switch
    {
        EnemyType.Heavy => _heavyEnemyModel,
        EnemyType.Flying => _flyingEnemyModel,
        _ => _standardEnemyModel
    };

    private Model? GetTowerModel(TowerType type) => type switch
    {
        TowerType.Cannon => _cannonTowerModel,
        TowerType.Magic => _magicTowerModel,
        _ => _arrowTowerModel
    };

    private void DrawModel(Model? model, Vector3 position, float scale, float rotationX, float rotationY, float rotationZ, bool lit)
    {
        // Aziz, Jihed und Rayan: Hier wird die gemeinsame Render-Hilfsroutine für Skalierung, Rotation, Position und Licht der 3D-Modelle genutzt.
        //  Alle FBX-Modelle gehen durch dieselbe Methode, damit Skalierung und Rotation zentral kontrolliert werden.
        if (model == null) return;

        var bones = new Matrix[model.Bones.Count];
        model.CopyAbsoluteBoneTransformsTo(bones);
        var world = Matrix.CreateScale(scale)
            * Matrix.CreateRotationX(rotationX)
            * Matrix.CreateRotationY(rotationY)
            * Matrix.CreateRotationZ(rotationZ)
            * Matrix.CreateTranslation(position);

        foreach (var mesh in model.Meshes)
        {
            foreach (var effect in mesh.Effects)
            {
                //  BasicEffect bekommt hier World/View/Projection, damit das Modell korrekt im 3D-Raum erscheint.
                if (effect is not BasicEffect basic) continue;
                basic.World = bones[mesh.ParentBone.Index] * world;
                basic.View = _view;
                basic.Projection = _projection;
                basic.LightingEnabled = lit;
                if (lit) basic.EnableDefaultLighting();
            }
            mesh.Draw();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _renderer?.Dispose();
            _spriteBatch?.Dispose();
            _pixel?.Dispose();
        }
        base.Dispose(disposing);
    }
}
