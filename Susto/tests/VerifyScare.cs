using System;
using System.IO;
using SustoNantao;

public static class VerifyScare
{
    private static int checks;
    private static void Check(bool ok, string what) { checks++; if (!ok) throw new Exception(what); }

    private static PlayerState Inside() => new PlayerState { Alive = true, Controlled = true, InsideFacility = true, Landed = true };

    private static byte[] Wav(short channels, int rate, short bits, short format, byte[] data)
    {
        var ms = new MemoryStream(); var w = new BinaryWriter(ms);
        w.Write("RIFF".ToCharArray()); w.Write(36 + data.Length); w.Write("WAVE".ToCharArray());
        w.Write("fmt ".ToCharArray()); w.Write(16); w.Write(format); w.Write(channels); w.Write(rate);
        w.Write(rate * channels * bits / 8); w.Write((short)(channels * bits / 8)); w.Write(bits);
        w.Write("data".ToCharArray()); w.Write(data.Length); w.Write(data);
        return ms.ToArray();
    }

    public static int Main()
    {
        try
        {
            // Quando pode assustar
            Check(ScareRules.CanScare(Inside(), true), "inside facility can scare");
            var s = Inside(); s.InsideFacility = false;
            Check(!ScareRules.CanScare(s, true), "outside cannot scare when only-inside");
            Check(ScareRules.CanScare(s, false), "outside can scare when allowed");
            s = Inside(); s.Alive = false; Check(!ScareRules.CanScare(s, true), "dead players are not scared");
            s = Inside(); s.InTerminal = true; Check(!ScareRules.CanScare(s, true), "terminal is safe");
            s = Inside(); s.Typing = true; Check(!ScareRules.CanScare(s, true), "chat is safe");
            s = Inside(); s.MenuOpen = true; Check(!ScareRules.CanScare(s, true), "pause menu is safe");
            s = Inside(); s.InShip = true; Check(!ScareRules.CanScare(s, false), "ship is safe");
            s = Inside(); s.Landed = false; Check(!ScareRules.CanScare(s, false), "orbit is safe");

            // Chance
            Check(ScareRules.ChanceInInterval(0, 60) == 0, "0% never");
            Check(Math.Abs(ScareRules.ChanceInInterval(6, 60) - .06) < 1e-9, "6%/min over one minute");
            Check(ScareRules.ChanceInInterval(100, 1) == 1, "100% always");
            Check(ScareRules.ChanceInInterval(float.NaN, 1) == 0, "bad chance");
            double perSecond = ScareRules.ChanceInInterval(6, 1);
            Check(Math.Abs((1 - Math.Pow(1 - perSecond, 60)) - .06) < 1e-9, "60 one-second rolls equal one minute");
            var rng = new Random(9); int hits = 0;
            for (int minute = 0; minute < 20000; minute++)
            {
                bool any = false;
                for (int sec = 0; sec < 60; sec++) if (ScareRules.Roll(rng.NextDouble(), 6, 1)) any = true;
                if (any) hits++;
            }
            Check(hits > 20000 * .05 && hits < 20000 * .07, "about 6% of minutes have a scare");
            Check(!ScareRules.Ready(100, 0, 240, 999, 90), "minimum interval respected");
            Check(ScareRules.Ready(300, 0, 240, 999, 90), "ready after the interval");
            Check(!ScareRules.Ready(300, 0, 240, 30, 90), "first-scare delay respected");

            // Desenho (determinístico, tamanho certo, rosto variado)
            var a = ScareArt.Generate(64, 97); var b = ScareArt.Generate(64, 97); var c = ScareArt.Generate(64, 194);
            Check(a.Length == 64 * 64 * 4, "RGBA size");
            Check(Convert.ToBase64String(a) == Convert.ToBase64String(b), "same seed, same face");
            Check(Convert.ToBase64String(a) != Convert.ToBase64String(c), "different seeds, different faces");
            var closed = ScareArt.Generate(64, 97, false, .1f);
            Check(Convert.ToBase64String(a) != Convert.ToBase64String(closed), "closed-mouth frame differs");
            var neg = ScareArt.Generate(64, 97, true);
            Check(neg[0] + a[0] >= 250 && neg[0] + a[0] <= 260, "negative frame inverts colors");
            Check(ScareArt.Static(32, 1).Length == 32 * 32 * 4, "static frame size");
            bool hasBright = false, hasDark = false;
            for (int i = 0; i < a.Length; i += 4) { if (a[i] > 240) hasBright = true; if (a[i] < 20) hasDark = true; }
            Check(hasBright && hasDark, "face has white pupils and black sockets");

            // Grito
            var scream = ScareSound.Scream(22050, 666);
            Check(scream.Length == (int)(22050 * 1.5f), "scream length");
            float peak = 0; bool inRange = true;
            foreach (var v in scream) { if (float.IsNaN(v) || v < -1 || v > 1) inRange = false; peak = Math.Max(peak, Math.Abs(v)); }
            Check(inRange, "scream samples in range");
            Check(peak > .8f, "scream is loud");

            // WAV
            var pcm16 = Wav(1, 22050, 16, 1, new byte[] { 0, 0, 255, 127, 0, 128 });
            var w16 = ScareRules.ParseWav(pcm16, out int ch, out int rate);
            Check(w16 != null && ch == 1 && rate == 22050 && w16.Length == 3, "16-bit WAV");
            Check(w16[0] == 0 && w16[1] > .99f && w16[2] == -1f, "16-bit values");
            var pcm8 = ScareRules.ParseWav(Wav(2, 8000, 8, 1, new byte[] { 128, 255 }), out ch, out rate);
            Check(pcm8 != null && ch == 2 && pcm8[0] == 0 && pcm8[1] > .98f, "8-bit stereo WAV");
            var f32 = ScareRules.ParseWav(Wav(1, 48000, 32, 3, BitConverter.GetBytes(.5f)), out ch, out rate);
            Check(f32 != null && f32[0] == .5f, "float WAV");
            Check(ScareRules.ParseWav(new byte[10], out ch, out rate) == null, "garbage rejected");
            Check(ScareRules.ParseWav(null, out ch, out rate) == null, "null rejected");
            var mp3ish = Wav(1, 22050, 16, 85, new byte[4]);
            Check(ScareRules.ParseWav(mp3ish, out ch, out rate) == null, "compressed WAV rejected");

            // Suspense: tensão
            float tension = 0;
            for (int i = 0; i < 6 * 60; i++) tension = SuspenseRules.UpdateTension(tension, 1, true, true, false, 6);
            Check(tension > .6f && tension < .8f, "6 min alone with light: tension rises but not to the max");
            tension = 0;
            for (int i = 0; i < 6 * 60; i++) tension = SuspenseRules.UpdateTension(tension, 1, true, true, true, 6);
            Check(tension == 1f, "6 min alone in the dark: max tension");
            float withFriends = SuspenseRules.UpdateTension(.5f, 10, true, false, true, 6);
            Check(withFriends < .5f, "near friends tension drops");
            float outside = SuspenseRules.UpdateTension(.5f, 10, false, true, true, 6);
            Check(outside < withFriends, "outside drops faster");
            Check(SuspenseRules.UpdateTension(0, 100, false, true, true, 6) == 0, "never below zero");
            Check(SuspenseRules.UpdateTension(float.NaN, 1, true, true, true, 6) >= 0, "bad value recovers");

            // Suspense: frequência e susto
            Check(SuspenseRules.EventsPerMinute(0, 4) == 0, "calm: no events");
            Check(SuspenseRules.EventsPerMinute(1, 4) == 4, "max tension: max events");
            Check(SuspenseRules.EventsPerMinute(.5f, 4) < 2, "events grow faster at the top");
            Check(SuspenseRules.EventChance(1, 4, 60) > .98, "at max, almost surely an event per minute");
            Check(SuspenseRules.ScareMultiplier(.3f) == 1, "low tension: normal scare chance");
            Check(Math.Abs(SuspenseRules.ScareMultiplier(1) - 4) < 1e-4, "max tension: 4x scare chance");

            // Suspense: escolha de eventos
            Check(SuspenseRules.PickEvent(0, .5, -1) == -1, "nothing unlocked at zero");
            Check(SuspenseRules.PickEvent(.1f, .5, -1) == (int)SuspenseEvent.Creak, "only creaks at low tension");
            Check(SuspenseRules.PickEvent(.1f, .5, (int)SuspenseEvent.Creak) == (int)SuspenseEvent.Creak, "single option may repeat");
            var seen = new bool[SuspenseRules.Unlock.Length];
            var r2 = new Random(3); int prev = -1; bool repeated = false;
            for (int i = 0; i < 3000; i++)
            {
                int e = SuspenseRules.PickEvent(1f, r2.NextDouble(), prev);
                Check(e >= 0 && e < SuspenseRules.Unlock.Length, "valid event");
                if (e == prev) repeated = true;
                seen[e] = true; prev = e;
            }
            Check(!repeated, "never the same event twice in a row");
            Check(Array.TrueForAll(seen, x => x), "all eight events happen at max tension");
            Check(SuspenseRules.PickEvent(.3f, .99, -1) != (int)SuspenseEvent.Eyes, "eyes locked at low-mid tension");
            for (int i = 0; i < 200; i++)
                Check(SuspenseRules.PickEvent(.3f, r2.NextDouble(), -1) <= (int)SuspenseEvent.Footsteps, "giggles/whispers locked at mid tension");

            // Sons de suspense
            foreach (var (name, clip) in new[] {
                ("knock", ScareSound.Knock(22050, 1)), ("step", ScareSound.Step(22050, 1)), ("creak", ScareSound.Creak(22050, 1)),
                ("whisper", ScareSound.Whisper(22050, 1)), ("giggle", ScareSound.Giggle(22050, 1)) })
            {
                float pk = 0; bool ok = clip.Length > 1000;
                foreach (var v in clip) { if (float.IsNaN(v) || v < -1 || v > 1) ok = false; pk = Math.Max(pk, Math.Abs(v)); }
                Check(ok && pk > .7f && pk <= .9f, name + " sound is valid and normalized");
            }

            Console.WriteLine("PASS: " + checks + " checks (rules, chance, art, scream, wav, suspense). Screen and audio playback need in-game checks.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
