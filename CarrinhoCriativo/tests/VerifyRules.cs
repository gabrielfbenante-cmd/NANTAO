using System;
using CarrinhoCriativo;

public static class VerifyRules
{
    private static int checks;
    private static void Check(bool condition, string text)
    { checks++; if (!condition) throw new Exception(text); }
    public static int Main()
    {
        try
        {
            Check(CartRules.ValidSnapshot(new CartSnapshot(), 8), "An absent, empty cart is a valid state.");
            CartSnapshot state = new CartSnapshot { exists = true, x = 10, y = .2f, z = -30, yaw = 180, speed = 2,
                driver = 0, passenger = 1, cargoIds = new string[] { "123", "124" } };
            Check(CartRules.ValidSnapshot(state, 8), "Normal two-player cart with cargo was rejected.");
            state.passenger = 0;
            Check(!CartRules.ValidSnapshot(state, 8), "A player cannot drive and ride simultaneously.");
            state.passenger = 1;
            state.cargoIds = new string[] { "123", "123" };
            Check(!CartRules.ValidSnapshot(state, 8), "Duplicate cargo references must be rejected.");
            state.cargoIds = new string[] { "garbage" };
            Check(!CartRules.ValidSnapshot(state, 8), "Invalid network object id was accepted.");
            state.cargoIds = new string[0];
            foreach (float invalid in new float[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 100001, -100001 })
            { state.x = invalid; Check(!CartRules.ValidSnapshot(state, 8), "Invalid position was accepted."); }
            state.x = 0;
            state.exists = false;
            Check(!CartRules.ValidSnapshot(state, 8), "An absent cart cannot contain players.");
            state.exists = true;
            state.cargoIds = new string[] { "1", "2", "3" };
            Check(!CartRules.ValidSnapshot(state, 2), "Cargo over capacity was accepted.");
            state.cargoIds = null;
            Check(!CartRules.ValidSnapshot(state, 8), "Null cargo array was accepted.");
            Check(!CartRules.ValidSnapshot(null, 8), "Null state was accepted.");
            state.cargoIds = new string[0]; state.version = 99;
            Check(!CartRules.ValidSnapshot(state, 8), "An unknown protocol version was accepted.");
            for (long client = 0; client < 24; client++)
            {
                Check(CartRules.CanTakeRole(client, -1, -1), "Empty role should be available.");
                Check(CartRules.CanTakeRole(client, client, -1), "The current occupant should be able to release the role.");
                Check(!CartRules.CanTakeRole(client, client + 1, -1), "An occupied role cannot be stolen.");
                Check(!CartRules.CanTakeRole(client, -1, client), "A player cannot take the other role too.");
            }
            Check(!CartRules.CanTakeRole(-1, -1, -1), "Invalid player id was accepted.");
            foreach (float empty in new float[] { 2, 3, 4, 5, 6 })
            {
                float previous = empty;
                for (int count = 0; count <= 12; count++)
                {
                    float loaded = CartRules.LoadedSpeed(empty, count);
                    Check(loaded <= previous && loaded >= 1.8f, "Cargo speed must decrease within bounds.");
                    previous = loaded;
                }
            }
            // 1.1.0: tamanho vindo do host
            CartSnapshot sized = new CartSnapshot { exists = true, size = 1.5f };
            Check(CartRules.ValidSnapshot(sized, 20), "A 1.5x cart is valid.");
            foreach (float bad in new float[] { 0f, .2f, 5f, float.NaN, float.PositiveInfinity })
            { sized.size = bad; Check(!CartRules.ValidSnapshot(sized, 20), "Invalid cart size was accepted."); }
            sized.size = 1.5f;
            sized.cargoIds = new string[20];
            for (int i = 0; i < 20; i++) sized.cargoIds[i] = (1000 + i).ToString();
            Check(CartRules.ValidSnapshot(sized, 20), "A full 20-item basket is valid.");
            Check(!CartRules.ValidSnapshot(sized, 19), "Basket over capacity was accepted.");

            // 1.1.0: ladeira
            float ax, az;
            CartRules.SlopeAcceleration(0f, 1f, 0f, -9.81f, 3f, out ax, out az);
            Check(ax == 0 && az == 0, "Flat floor must not push the cart.");
            float tilt = (float)Math.Sin(2 * Math.PI / 180), up = (float)Math.Cos(2 * Math.PI / 180);
            CartRules.SlopeAcceleration(tilt, up, 0f, -9.81f, 3f, out ax, out az);
            Check(ax == 0 && az == 0, "A 2 degree floor counts as flat (cart stays parked).");
            tilt = (float)Math.Sin(15 * Math.PI / 180); up = (float)Math.Cos(15 * Math.PI / 180);
            CartRules.SlopeAcceleration(tilt, up, 0f, -9.81f, 3f, out ax, out az);
            Check(ax > 2f && ax < 3f && Math.Abs(az) < 1e-4, "A 15 degree slope pushes downhill (+x) by about g*sin*cos.");
            CartRules.SlopeAcceleration(0f, up, -tilt, -9.81f, 3f, out ax, out az);
            Check(az < -2f && Math.Abs(ax) < 1e-4, "Downhill direction follows the floor normal (-z).");
            float steepX, steepZ, gentleX, gentleZ;
            CartRules.SlopeAcceleration((float)Math.Sin(30 * Math.PI / 180), (float)Math.Cos(30 * Math.PI / 180), 0f, -9.81f, 3f, out steepX, out steepZ);
            CartRules.SlopeAcceleration((float)Math.Sin(8 * Math.PI / 180), (float)Math.Cos(8 * Math.PI / 180), 0f, -9.81f, 3f, out gentleX, out gentleZ);
            Check(steepX > gentleX && gentleX > 0, "Steeper slopes accelerate more.");
            CartRules.SlopeAcceleration(0f, 0f, 0f, -9.81f, 3f, out ax, out az);
            Check(ax == 0 && az == 0, "Degenerate normal is ignored.");
            Check(CartRules.SlopeSpeedLimit(4f, 0f) == 4f, "Flat push keeps the normal limit.");
            Check(CartRules.SlopeSpeedLimit(4f, 1f) == 8f, "Downhill doubles the push limit.");
            Check(CartRules.SlopeSpeedLimit(4f, 9f) == 10f, "Downhill bonus is capped.");
            Check(CartRules.SlopeSpeedLimit(4f, -1f) == 2f, "Uphill halves the push limit.");
            Check(CartRules.SlopeSpeedLimit(4f, float.NaN) == 4f, "Bad slope value is ignored.");

            Console.WriteLine("PASS: " + checks + " checks against the compiled cart DLL: roles, capacity, size, slopes, state validation and loaded speed. Unity physics and multiplayer require runtime checks.");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
