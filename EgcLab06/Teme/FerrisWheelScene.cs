// Tema 06 - Varianta C: Roata panoramica
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

public sealed class FerrisWheelScene : Scene
{
    private const string FasterWheel = "mareste viteza rotii";
    private const string SlowerWheel = "micsoreaza viteza rotii";
    private const string StopWheel = "opreste roata lin";
    private const float Radius = 4.0f;
    private const float CenterY = 5.0f;
    private const float MaxSpeed = 90.0f;
    private const float Response = 2.5f;

    private readonly SceneObject[] _cabinPivots = new SceneObject[8];
    private readonly SceneObject[] _cabins = new SceneObject[8];
    private readonly float[] _swing = new float[8];
    private readonly float[] _swingVelocity = new float[8];
    private SceneObject _wheel = null!;
    private Tween? _dockTween;
    private float _angle;
    private float _speed;
    private float _targetSpeed = 18.0f;
    private int _selectedCabin = -1;

    public override string Name => "tema C - roata panoramica";
    public override string Status => _dockTween != null
        ? $"cabina {_selectedCabin + 1} -> peron | unghi {_angle:F1}°"
        : $"viteza {_speed:F1}°/s | tinta {_targetSpeed:F1}°/s";

    public override IEnumerable<string> ActionNames => new[]
    {
        FasterWheel, SlowerWheel, StopWheel,
        "cabina 1 la peron", "cabina 2 la peron", "cabina 3 la peron", "cabina 4 la peron",
        "cabina 5 la peron", "cabina 6 la peron", "cabina 7 la peron", "cabina 8 la peron"
    };

    public override void Load(Input input)
    {
        CameraPosition = new Vector3(11.0f, 8.0f, 19.0f);
        CameraTarget = new Vector3(0.0f, 4.5f, 0.0f);

        input.Bind(FasterWheel, Keys.Up);
        input.Bind(SlowerWheel, Keys.Down);
        input.Bind(StopWheel, Keys.Space);
        Keys[] numbers = { Keys.D1, Keys.D2, Keys.D3, Keys.D4,
                           Keys.D5, Keys.D6, Keys.D7, Keys.D8 };
        for (int i = 0; i < 8; i++)
            input.Bind($"cabina {i + 1} la peron", numbers[i]);

        Mesh cube = AddMesh(Shapes.ShadedCube());
        Mesh sphere = AddMesh(Shapes.ShadedSphere(1.0f, 20, 12));
        Mesh cylinder = AddMesh(Shapes.ShadedCylinder(0.5f, 1.0f, 24));
        Mesh ground = AddMesh(Shapes.Grid(12, 1.0f, new Color4(0.3f, 0.34f, 0.4f, 1.0f)));
        AddObject("grila", ground);

        var basePlate = Part("fundatie", cube, null, new Vector3(0, 0.12f, 0),
            new Vector3(7.0f, 0.24f, 4.0f), new Color4(0.25f, 0.29f, 0.35f, 1));
        Part("peron", cube, null, new Vector3(0, 0.3f, 2.2f),
            new Vector3(2.2f, 0.25f, 1.5f), new Color4(0.65f, 0.67f, 0.7f, 1));

        foreach (float z in new[] { -0.9f, 0.9f })
        {
            Beam("suport stang", cube, null, new Vector3(-2.2f, 0.25f, z),
                 new Vector3(0, CenterY, z), 0.22f, new Color4(0.65f, 0.7f, 0.76f, 1));
            Beam("suport drept", cube, null, new Vector3(2.2f, 0.25f, z),
                 new Vector3(0, CenterY, z), 0.22f, new Color4(0.65f, 0.7f, 0.76f, 1));
        }

        _wheel = AddObject("pivot roata", null);
        _wheel.Transform.Position = new Vector3(0, CenterY, 0);

        Color4 rimColor = new(0.2f, 0.73f, 0.95f, 1);
        Color4 spokeColor = new(0.92f, 0.92f, 0.95f, 1);
        const int segments = 48;
        for (int j = 0; j < segments; j++)
        {
            float a = 2.0f * MathF.PI * j / segments;
            float b = 2.0f * MathF.PI * (j + 1) / segments;
            foreach (float z in new[] { -0.65f, 0.65f })
            {
                Vector3 p1 = new(Radius * MathF.Sin(a), -Radius * MathF.Cos(a), z);
                Vector3 p2 = new(Radius * MathF.Sin(b), -Radius * MathF.Cos(b), z);
                Beam("janta", cube, _wheel, p1, p2, 0.16f, rimColor);
            }
        }
        for (int i = 0; i < 8; i++)
        {
            float radians = i * MathF.PI / 4.0f;
            float x = Radius * MathF.Sin(radians);
            float y = -Radius * MathF.Cos(radians);
            foreach (float z in new[] { -0.65f, 0.65f })
                Beam("spita", cube, _wheel, new Vector3(0, 0, z),
                     new Vector3(x, y, z), 0.11f, spokeColor);

            SceneObject anchor = AddObject($"pivot cabina {i + 1}", null, _wheel);
            anchor.Transform.Position = new Vector3(x, y, 0);
            _cabinPivots[i] = anchor;

            SceneObject cabin = AddObject($"cabina {i + 1}", null, anchor);
            _cabins[i] = cabin;
            float hue = i / 8.0f;
            Color4 tint = ColorUtil.FromHsv(hue, 0.65f, 1.0f);
            Part("corp cabina", cube, cabin, new Vector3(0, -0.45f, 0),
                 new Vector3(0.95f, 0.7f, 0.85f), tint);
            Part("acoperis", cube, cabin, new Vector3(0, 0.03f, 0),
                 new Vector3(1.1f, 0.12f, 1.0f), new Color4(0.92f, 0.93f, 0.98f, 1));
            Part("geam frontal", cube, cabin, new Vector3(0, -0.35f, 0.435f),
                 new Vector3(0.6f, 0.32f, 0.035f), new Color4(0.55f, 0.85f, 1.0f, 1));
            Part("balustrada", cube, cabin, new Vector3(0, -0.75f, 0.49f),
                 new Vector3(0.85f, 0.07f, 0.08f), new Color4(0.95f, 0.95f, 0.98f, 1));
        }

        SceneObject axle = Part("ax", cylinder, null, new Vector3(0, CenterY, 0),
            new Vector3(0.6f, 2.3f, 0.6f), new Color4(0.9f, 0.72f, 0.25f, 1));
        axle.Transform.Rotation = new Vector3(90, 0, 0);
        Part("butuc", sphere, _wheel, Vector3.Zero, new Vector3(0.48f),
             new Color4(1.0f, 0.8f, 0.2f, 1));
    }

    private SceneObject Part(string name, Mesh mesh, SceneObject? parent,
        Vector3 position, Vector3 scale, Color4 tint)
    {
        SceneObject obj = AddObject(name, mesh, parent);
        obj.Transform.Position = position;
        obj.Transform.Scale = scale;
        obj.Tint = tint;
        return obj;
    }

    private void Beam(string name, Mesh cube, SceneObject? parent,
        Vector3 from, Vector3 to, float thickness, Color4 tint)
    {
        Vector3 delta = to - from;
        SceneObject obj = Part(name, cube, parent, (from + to) * 0.5f,
            new Vector3(thickness, delta.Length, thickness), tint);
        obj.Transform.Rotation = new Vector3(0, 0,
            -MathHelper.RadiansToDegrees(MathF.Atan2(delta.X, delta.Y)));
    }

    public override void Update(float dt, Input input)
    {
        if (input.WasPressed(FasterWheel))
        {
            _dockTween = null;
            _selectedCabin = -1;
            _targetSpeed = Math.Clamp(_targetSpeed + 10.0f, -MaxSpeed, MaxSpeed);
        }
        if (input.WasPressed(SlowerWheel))
        {
            _dockTween = null;
            _selectedCabin = -1;
            _targetSpeed = Math.Clamp(_targetSpeed - 10.0f, -MaxSpeed, MaxSpeed);
        }
        if (input.WasPressed(StopWheel))
        {
            _dockTween = null;
            _selectedCabin = -1;
            _targetSpeed = 0;
        }
        for (int i = 0; i < 8; i++)
        {
            if (input.WasPressed($"cabina {i + 1} la peron"))
            {
                _selectedCabin = i;
                _targetSpeed = 0;
                _speed = 0;
                float desired = -45.0f * i;
                float difference = (desired - _angle) % 360.0f;
                if (difference > 180.0f) difference -= 360.0f;
                if (difference < -180.0f) difference += 360.0f;
                _dockTween = new Tween(new Vector3(_angle, 0, 0),
                    new Vector3(_angle + difference, 0, 0), 1.6f, Easing.EaseInOut);
                break;
            }
        }

        if (dt <= 0.0f) return;

        float previousSpeed = _speed;
        if (_dockTween != null)
        {
            float previousAngle = _angle;
            _dockTween.Update(dt);
            _angle = _dockTween.Value.X;
            _speed = (_angle - previousAngle) / dt;
            if (_dockTween.IsFinished)
            {
                _dockTween = null;
                _speed = 0;
            }
        }
        else
        {
            float alpha = 1.0f - MathF.Exp(-Response * dt);
            float nextSpeed = _speed + (_targetSpeed - _speed) * alpha;
            _angle += _targetSpeed * dt + (_speed - _targetSpeed) * alpha / Response;
            _speed = nextSpeed;
            if (MathF.Abs(_angle) > 3600.0f)
                _angle %= 360.0f;
        }

        _wheel.Transform.Rotation = new Vector3(0, 0, _angle);
        float acceleration = (_speed - previousSpeed) / dt;
        for (int i = 0; i < 8; i++)
        {
            float force = -0.018f * acceleration - 14.0f * _swing[i] - 5.0f * _swingVelocity[i];
            _swingVelocity[i] += force * dt;
            _swing[i] += _swingVelocity[i] * dt;
            _swing[i] = Math.Clamp(_swing[i], -12.0f, 12.0f);
            _cabins[i].Transform.Rotation = new Vector3(0, 0, -_angle + _swing[i]);
        }
    }

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;
        yield return $"Unghi roata     : {_angle:F2} grade";
        yield return $"Viteza reala    : {_speed:F2} grade/s";
        yield return $"Viteza tinta    : {_targetSpeed:F2} grade/s";
        yield return $"Cabina selectata: {(_selectedCabin < 0 ? "niciuna" : (_selectedCabin + 1).ToString())}";
        yield return $"Pozitionare     : {(_dockTween == null ? "inactiva" : $"in curs ({_dockTween.Progress:P0})")}";
        yield return "Bonus           : leganare cu resort amortizat";
    }
}
