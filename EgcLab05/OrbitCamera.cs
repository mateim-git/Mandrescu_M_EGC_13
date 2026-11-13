
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

public class OrbitCamera : Camera
{
    private static readonly Vector3 WorldUp = Vector3.UnitY;

    public Vector3 Target { get; private set; }
    public float Distance { get; private set; }
    public float Azimuth { get; private set; }
    public float Elevation { get; private set; }

    public const float MinDistance = 1.0f;
    public const float MaxDistance = 50.0f;
    public const float MaxElevation = 89.0f;

    public float MouseSensitivity = 0.2f;
    public float RotationSpeed = 90.0f;
    public float ZoomStep = 0.8f;
    public float PanSpeed = 3.0f;

    public override string Name => "Camera orbita";

    public override Vector3 Position
    {
        get
        {
            float az = MathHelper.DegreesToRadians(Azimuth);
            float el = MathHelper.DegreesToRadians(Elevation);

            return Target + Distance * new Vector3(
                MathF.Cos(el) * MathF.Cos(az),
                MathF.Sin(el),
                MathF.Cos(el) * MathF.Sin(az)
            );
        }
    }

    public override Vector3 Front =>
        Vector3.Normalize(Target - Position);

    public override Matrix4 ViewMatrix =>
        Matrix4.LookAt(Position, Target, WorldUp);

    public OrbitCamera()
    {
        Reset();
    }

    public override void Update(
        KeyboardState keyboard,
        MouseState mouse,
        Vector2 mouseDelta,
        float dt)
    {
        // Rotire cu mouse-ul, doar cu click stanga tinut
        if (mouse.IsButtonDown(MouseButton.Left))
        {
            Azimuth -= mouseDelta.X * MouseSensitivity;
            Elevation += mouseDelta.Y * MouseSensitivity;
        }

        // Rotire cu sagetile
        if (keyboard.IsKeyDown(Keys.Left))
            Azimuth -= RotationSpeed * dt;

        if (keyboard.IsKeyDown(Keys.Right))
            Azimuth += RotationSpeed * dt;

        if (keyboard.IsKeyDown(Keys.Up))
            Elevation += RotationSpeed * dt;

        if (keyboard.IsKeyDown(Keys.Down))
            Elevation -= RotationSpeed * dt;

        Elevation = Math.Clamp(
            Elevation, -MaxElevation, MaxElevation);

        // Zoom orbital: modificam distanta, nu FOV-ul
        Distance -= mouse.ScrollDelta.Y * ZoomStep;
        Distance = Math.Clamp(
            Distance, MinDistance, MaxDistance);

        // Bonus: W A S D deplaseaza tinta pe planul XZ
        Vector3 forward = new Vector3(Front.X, 0, Front.Z);

        if (forward.LengthSquared > 0.000001f)
            forward = Vector3.Normalize(forward);

        Vector3 right = Vector3.Normalize(
            Vector3.Cross(forward, WorldUp));

        Vector3 move = Vector3.Zero;

        if (keyboard.IsKeyDown(Keys.W)) move += forward;
        if (keyboard.IsKeyDown(Keys.S)) move -= forward;
        if (keyboard.IsKeyDown(Keys.D)) move += right;
        if (keyboard.IsKeyDown(Keys.A)) move -= right;

        if (move.LengthSquared > 0.000001f)
            Target += Vector3.Normalize(move) * PanSpeed * dt;
    }

    // Bonus: preluam pozitia si orientarea camerei FPS
    public void SetFromFps(Vector3 position, Vector3 front)
    {
        Distance = 5.0f;
        Target = position + Vector3.Normalize(front) * Distance;

        Vector3 offset = Vector3.Normalize(position - Target);

        Elevation = MathHelper.RadiansToDegrees(
            MathF.Asin(Math.Clamp(offset.Y, -1.0f, 1.0f)));

        Azimuth = MathHelper.RadiansToDegrees(
            MathF.Atan2(offset.Z, offset.X));
    }

    public override void Reset()
    {
        Target = Vector3.Zero;
        Distance = 8.0f;
        Azimuth = 45.0f;
        Elevation = 20.0f;
        FieldOfView = 60.0f;
    }

    public override IEnumerable<string> Describe()
    {
        yield return $"Tinta          : ({Target.X:F2}, {Target.Y:F2}, {Target.Z:F2})";
        yield return $"Distanta       : {Distance:F2}";
        yield return $"Azimut         : {Azimuth:F1}°";
        yield return $"Elevatie       : {Elevation:F1}°";
        yield return $"Pozitie        : ({Position.X:F2}, {Position.Y:F2}, {Position.Z:F2})";
    }
}
