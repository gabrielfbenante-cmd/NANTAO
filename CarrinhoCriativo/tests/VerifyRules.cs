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
            Console.WriteLine("PASS: " + checks + " checks against the compiled cart DLL: roles, capacity, state validation and loaded speed. Unity physics and multiplayer require runtime checks.");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
