using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Diagnostics.Tracing;

namespace Egc;

/// <summary>
/// Fereastra aplicației și bucla principală (main game loop).
/// Față de laboratorul 3: o scenă cu mai multe obiecte, fiecare cu matricea lui model,
/// o cameră cu matricele view și projection, tastatura și mouse-ul.
/// </summary>
public class Game : GameWindow
{
    // Obiecte care NU depind de OpenGL pot fi create imediat.
    private readonly Camera _camera = new();

    // Obiectele care folosesc OpenGL se creează în OnLoad (abia atunci există contextul OpenGL).
    private Shader _shader = null!;
    private readonly List<SceneObject> _scene = new();
    private readonly List<Transform2D> _initialTransforms = new();   // pentru tasta R

    // Starea aplicației (se modifică în OnUpdateFrame, se folosește în OnRenderFrame).
    private int _selected;            // indexul obiectului selectat în _scene (tasta Tab)
    private bool _wireframe;          // contur (true) sau plin (false)

    private Vector2 _pivot = Vector2.Zero; //T4: Pivotul este un punct fix in coordonate de lume.

    // T5: Satelitul are propria geometrie si un unghi orbital in grade.
    private Mesh _satelliteMesh = null!;
    private float _satelliteAngle;

    // Contoare afișate în titlul ferestrei.
    private readonly string _baseTitle;
    private double _secondTimer;
    private int _frames, _lastFrames;
    private bool _titleDirty = true;

    public Game(int width, int height, string title)
        : base(GameWindowSettings.Default,
               new NativeWindowSettings
               {
                   ClientSize = new Vector2i(width, height),
                   Title = title,
                   APIVersion = new Version(3, 3),          // cerem OpenGL 3.3 ...
                   Profile = ContextProfile.Core,           // ... profil Core (fără modul imediat)
                   Flags = ContextFlags.ForwardCompatible
               })
    {
        _baseTitle = title;
        VSync = VSyncMode.On;   // swap-ul bufferelor așteaptă reîmprospătarea monitorului
    }

    /// <summary>Apelată O SINGURĂ DATĂ, după crearea ferestrei și a contextului OpenGL.</summary>
    protected override void OnLoad()
    {
        base.OnLoad();

        Console.WriteLine($"OpenGL  : {GL.GetString(StringName.Version)}");
        Console.WriteLine($"Renderer: {GL.GetString(StringName.Renderer)}");

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        _camera.Resize(ClientSize.X, ClientSize.Y);

        _shader = Shader.FromFiles("Shaders/shader.vert", "Shaders/shader.frag");

        // Scena: aceleași forme din laboratorul 3, acum plasate în lume prin Transform2D.
        // Depth (z) contează doar în perspectivă (tasta O, secțiunea 7.2).
        AddObject("pătrat", Shapes.Quad(),
                  new Transform2D { Position = new Vector2(-1.8f, -1.0f), Depth = -3.0f });
        AddObject("triunghi", Shapes.Triangle(),
                  new Transform2D { Position = new Vector2(-1.2f, 1.0f) });
        AddObject("hexagon", Shapes.RegularPolygon(6, 0.5f),
                  new Transform2D { Position = new Vector2(2.0f, 1.0f) });
        AddObject("cerc", Shapes.RegularPolygon(64, 0.5f),
                  new Transform2D { Position = new Vector2(0.8f, -0.6f), Depth = 2.0f });

        _satelliteMesh = Shapes.Triangle();
    }

    /// <summary>Adaugă un obiect în scenă și reține transformarea lui inițială (pentru tasta R).</summary>
    private void AddObject(string name, Mesh mesh, Transform2D transform)
    {
        _scene.Add(new SceneObject(name, mesh, transform));
        _initialTransforms.Add(transform.Clone());
    }

    /// <summary>Apelată când se schimbă dimensiunea ferestrei.</summary>
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        GL.Viewport(0, 0, e.Width, e.Height);   // zona ferestrei în care se desenează
        _camera.Resize(e.Width, e.Height);      // proiecția se adaptează la noul raport de aspect
    }

    /// <summary>Apelată la fiecare iterație a buclei: LOGICA aplicației (intrări, mișcare, timp).</summary>
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
        }

        // --- Obiectul selectat: tastatura (secțiunea 8) ------------------------------

        float dt = (float)args.Time;
        
        _satelliteAngle = (_satelliteAngle + 90.0f * dt) % 360.0f;

        SceneObject sel = _scene[_selected];

        // T2: Shift accelerează doar miscarile obiectului.
        // dt ramane nemodificat pentru camera si celelalte calcule.

        float objectDt = dt * (KeyboardState.IsKeyDown(Keys.LeftShift) ? 3.0f : 1.0f);

        const float moveSpeed = 2.0f;

        if (KeyboardState.IsKeyDown(Keys.Left))
            sel.Transform.Position.X -= moveSpeed * objectDt;

        if (KeyboardState.IsKeyDown(Keys.Right))
            sel.Transform.Position.X += moveSpeed * objectDt;

        if (KeyboardState.IsKeyDown(Keys.Down))
            sel.Transform.Position.Y -= moveSpeed * objectDt;

        if (KeyboardState.IsKeyDown(Keys.Up))
            sel.Transform.Position.Y += moveSpeed * objectDt;

        // Q: sens trigonometric; E: sens orar.

        float rotationDirection = (KeyboardState.IsKeyDown(Keys.Q) ? 1.0f : 0.0f) - (KeyboardState.IsKeyDown(Keys.E) ? 1.0f : 0.0f);

        sel.Transform.Rotation += rotationDirection * 90.0f * objectDt;

        float scaleDirection = (KeyboardState.IsKeyDown(Keys.X) ? 1.0f : 0.0f) - (KeyboardState.IsKeyDown(Keys.Z) ? 1.0f : 0.0f);

        // T2: Inmultirea modifica proportional dimensiunile si pastreaza raportul dintre axe inainte de atingerea limitelor.
        // MathF.Pow face scalarea dependenta de timpul trecut, nu de numarul de cadre.

        float scaleFactor = MathF.Pow(1.5f, scaleDirection * objectDt);

        sel.Transform.Scale.X = Math.Clamp(sel.Transform.Scale.X * scaleFactor, 0.1f, 10.0f);
        sel.Transform.Scale.Y = Math.Clamp(sel.Transform.Scale.Y * scaleFactor, 0.1f, 10.0f);

        if (KeyboardState.IsKeyPressed(Keys.Tab)) _selected = (_selected + 1) % _scene.Count;
        if (KeyboardState.IsKeyPressed(Keys.O))   _camera.IsPerspective = !_camera.IsPerspective;
        if (KeyboardState.IsKeyPressed(Keys.F))   _wireframe = !_wireframe;
        if (KeyboardState.IsKeyPressed(Keys.R))   ResetScene();

        if (KeyboardState.IsKeyPressed(Keys.Tab) || KeyboardState.IsKeyPressed(Keys.O) ||
            KeyboardState.IsKeyPressed(Keys.R))
        {
            _titleDirty = true;
        }

        // --- Obiectul selectat: mouse-ul (secțiunea 9) -------------------------------

        // T3: Conversia cerută este pentru proiecția ortografică.
        // Actualizăm selecția deoarece Tab poate fi apăsat în acest cadru.

        if (!_camera.IsPerspective && ClientSize.X > 0 && ClientSize.Y > 0 && MouseState.IsButtonPressed(MouseButton.Left))
        {
            _scene[_selected].Transform.Position =
                ScreenToWorld(MouseState.Position);
        }

        // T4: Clic dreapta stabileste pivotul.
        // ScreenToWorld este implementata pentru proiectia ortografica.

        if (!_camera.IsPerspective && ClientSize.X > 0 && ClientSize.Y > 0 && MouseState.IsButtonPressed(MouseButton.Right))
        {
            _pivot = ScreenToWorld(MouseState.Position);
        }

        if (KeyboardState.IsKeyDown(Keys.P))
        {
            Transform2D transform = _scene[_selected].Transform;

            float angleDegrees = 90.0f * dt;
            float angleRadians = MathHelper.DegreesToRadians(angleDegrees);

            float cos = MathF.Cos(angleRadians);
            float sin = MathF.Sin(angleRadians);

            Vector2 offset = transform.Position - _pivot;

            Vector2 rotatedOffset = new Vector2(
                offset.X * cos - offset.Y * sin,
                offset.X * sin + offset.Y * cos
            );

            transform.Position = _pivot + rotatedOffset;

            transform.Rotation += angleDegrees;
        }

        // --- Camera: tastatura și rotița (secțiunea 6.2) -----------------------------

        float camSpeed = 3.0f / _camera.Zoom;                    // unități de lume pe secundă
        if (KeyboardState.IsKeyDown(Keys.A)) _camera.Position.X -= camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.D)) _camera.Position.X += camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.S)) _camera.Position.Y -= camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.W)) _camera.Position.Y += camSpeed * dt;

        _camera.Zoom *= MathF.Pow(1.1f, MouseState.ScrollDelta.Y);   // un pas al rotiței = ±10 %
        _camera.Zoom = Math.Clamp(_camera.Zoom, 0.1f, 10.0f);

        // --- Titlul ferestrei: obiectul selectat, proiecția, cadre pe secundă --------

        _secondTimer += args.Time;
        if (_secondTimer >= 1.0)
        {
            _lastFrames = _frames;
            _frames = 0;
            _secondTimer -= 1.0;
            _titleDirty = true;
        }

        if (_titleDirty)
        {
            string projection = _camera.IsPerspective ? "perspectivă" : "ortografică";
            Title = $"{_baseTitle} | selectat: {_scene[_selected].Name} | proiecție {projection} | FPS: {_lastFrames}";
            _titleDirty = false;
        }
    }

    /// <summary>Apelată la fiecare iterație a buclei: DESENAREA unui cadru.</summary>
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        _frames++;

        // 1. Ștergem imaginea veche din back buffer.
        GL.ClearColor(0.12f, 0.14f, 0.18f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        // 2. Starea de rasterizare și programul shader.
        SetWireframe(_wireframe);
        _shader.Use();

        // 3. View și projection sunt aceleași pentru toate obiectele: le trimitem o singură dată.
        _shader.SetMatrix4("uView", _camera.ViewMatrix);
        _shader.SetMatrix4("uProjection", _camera.ProjectionMatrix);

        // 4. Fiecare obiect: matricea lui model, nuanța, apoi desenarea.
        foreach (SceneObject obj in _scene)
        {
            _shader.SetMatrix4("uModel", obj.Transform.ModelMatrix);
            _shader.SetColor("uTint", obj.Tint);
            obj.Mesh.Draw();
        }

        Matrix4 satelliteLocal = Matrix4.CreateScale(0.3f, 0.3f, 1.0f) * Matrix4.CreateTranslation(1.2f, 0.0f, 0.0f) *
        Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(_satelliteAngle));

        // T5: Scalarea uniforma a parintelui modifica raza orbitei si
        // dimensiunea satelitului. O scalare neuniforma transforma
        // orbita circulara intr-o elipsa, deoarece scaleaza diferit axele.
        Matrix4 satelliteModel = satelliteLocal * _scene[_selected].Transform.ModelMatrix;

        _shader.SetMatrix4("uModel", satelliteModel);
        _shader.SetColor("uTint", Color4.White);
        _satelliteMesh.Draw();

        // 5. Afișăm cadrul terminat: back buffer <-> front buffer.
        SwapBuffers();
    }

    /// <summary>Apelată O SINGURĂ DATĂ, la închiderea ferestrei: eliberăm resursele GPU.</summary>
    protected override void OnUnload()
    {
        foreach (SceneObject obj in _scene)
        {
            obj.Dispose();
        }
        _satelliteMesh.Dispose();
        _shader.Dispose();

        base.OnUnload();
    }

    /// <summary>Transformă poziția mouse-ului (pixeli, origine stânga-sus) în coordonate de lume.</summary>
    private Vector2 ScreenToWorld(Vector2 mousePixels)
    {
        float xNdc = 2.0f * mousePixels.X / ClientSize.X - 1.0f;
        float yNdc = 1.0f - 2.0f * mousePixels.Y / ClientSize.Y;

        // T3: La mouse, y creste in jos, iar in NDC creste in sus.
        // Apare in formula 1 - deoarece se inverseaza sensul axei verticale.

        Vector4 ndc = new Vector4(xNdc, yNdc, 0.0f, 1.0f);

        Matrix4 inverseViewProjection = (_camera.ViewMatrix * _camera.ProjectionMatrix).Inverted();
        Vector4 world = Vector4.TransformRow(ndc, inverseViewProjection);

        return new Vector2(world.X / world.W, world.Y / world.W);
    }

    /// <summary>Tasta R: obiectele revin la transformările inițiale, camera la starea inițială.</summary>
    private void ResetScene()
    {
        for (int i = 0; i < _scene.Count; i++)
        {
            _scene[i].Transform = _initialTransforms[i].Clone();
        }
        _camera.Reset();
        // T4 si T5: Revenim si cu pivotul si orbita la starea initiala.
        _pivot = Vector2.Zero;
        _satelliteAngle = 0.0f;
    }

    /// <summary>
    /// Contur / plin. Dacă Visual Studio semnalează o eroare la primul parametru,
    /// folosiți aici tipul acceptat de versiunea voastră (vezi laboratorul 2, Problema 3):
    /// TriangleFace.FrontAndBack sau MaterialFace.FrontAndBack.
    /// </summary>
    private static void SetWireframe(bool wireframe)
    {
        GL.PolygonMode(TriangleFace.FrontAndBack, wireframe ? PolygonMode.Line : PolygonMode.Fill);
    }
}
