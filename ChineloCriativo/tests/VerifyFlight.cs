using System;
using ChineloArremessavel;

internal static class VerifyFlight
{
    private static int checks;
    private static void Check(bool result, string name)
    { checks++; if (!result) throw new Exception(name); }
    private static FlightMessage Launch()
    { return new FlightMessage { action = "launch", item = "42", shot = 1, dz = 1, range = 12, speed = 14 }; }
    public static void Main()
    {
        Check(FlightRules.Distance(0, 14, 12) == 0, "starts at camera");
        Check(FlightRules.Distance(.5f, 14, 12) == 7, "seven metres after half second");
        Check(FlightRules.Distance(12f / 14f, 14, 12) == 12, "exact endpoint");
        Check(FlightRules.Distance(1, 14, 12) == 12, "long frame cannot overshoot");
        Check(FlightRules.Distance(60, 14, 12) == 12, "delayed client stays at endpoint");
        Check(FlightRules.Distance(-1, 14, 12) == 0, "negative elapsed");
        foreach (float bad in new float[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Check(FlightRules.Distance(bad, 14, 12) == 0, "bad time");
            Check(FlightRules.Distance(1, bad, 12) == 0, "bad speed");
            Check(FlightRules.Distance(1, 14, bad) == 0, "bad range");
            FlightMessage invalid = Launch(); invalid.x = bad;
            Check(!FlightRules.Valid(invalid, true), "nonfinite origin");
            invalid = Launch(); invalid.dz = bad;
            Check(!FlightRules.Valid(invalid, true), "nonfinite direction");
        }
        Check(FlightRules.Valid(Launch(), true), "valid host launch");
        FlightMessage message = Launch(); message.action = "request"; message.range = 0; message.speed = 0; message.shot = 0;
        Check(FlightRules.Valid(message, true), "client cannot choose host speed or range");
        message = Launch(); message.dy = 1; message.dz = 0;
        Check(FlightRules.Valid(message, true), "vertical shot");
        message.dy = -.6f; message.dz = .8f;
        Check(FlightRules.Valid(message, true), "angled shot");
        message = Launch(); message.dz = 0;
        Check(!FlightRules.Valid(message, true), "zero direction");
        message.dz = 3;
        Check(!FlightRules.Valid(message, true), "unnormalized direction");
        message = Launch(); message.range = 26;
        Check(!FlightRules.Valid(message, true), "range bound");
        message = Launch(); message.speed = 0;
        Check(!FlightRules.Valid(message, true), "zero speed");
        message = Launch(); message.version = 2;
        Check(!FlightRules.Valid(message, true), "protocol mismatch");
        message = Launch(); message.item = "-1";
        Check(!FlightRules.Valid(message, true), "invalid object id");
        message = Launch(); message.action = "explode";
        Check(FlightRules.Valid(message, false), "host explosion");
        Check(!FlightRules.Valid(message, true), "explosion is not a launch");
        message.shot = 0;
        Check(!FlightRules.Valid(message, false), "unidentified explosion");
        Check(!FlightRules.Valid(null, true), "missing message");
        Console.WriteLine("PASS: " + checks + " checks against compiled flight rules. Unity collisions, damage and multiplayer need runtime checks.");
    }
}
