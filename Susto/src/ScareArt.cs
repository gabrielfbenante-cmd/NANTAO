using System;
using System.Collections.Generic;

namespace SustoNantao
{
    /// <summary>
    /// Desenha por código um rosto assustador ORIGINAL (nenhuma imagem de outro jogo).
    /// Código puro (sem Unity) para poder ser testado/visualizado fora do jogo.
    /// Saída: RGBA, linha 0 = parte de BAIXO da imagem (padrão da Unity).
    /// </summary>
    public static class ScareArt
    {
        /// <param name="mouthOpen">0 = boca quase fechada, 1 = escancarada (quadros da "investida").</param>
        public static byte[] Generate(int size, int seed, bool negative = false, float mouthOpen = 1f)
        {
            var rng = new Random(seed);
            float Jit(float a) => (float)(rng.NextDouble() * 2 - 1) * a;

            // Variações por semente.
            float eyeX = .27f + Jit(.03f), eyeY = .2f + Jit(.03f);
            float eyeRx = .17f + Jit(.02f), eyeRy = .25f + Jit(.04f);
            float eyeSkew = Jit(.06f); // um olho mais baixo que o outro
            float mouthY = -.42f + Jit(.03f), mouthRx = .2f + Jit(.03f), mouthRy = .36f + Jit(.05f);
            float tilt = Jit(.12f);
            int teeth = 9 + rng.Next(5);
            float pupilShift = Jit(.03f);
            int noiseSeed = rng.Next(1, 100000);
            // Algumas criaturas têm olhos extras na testa.
            var extraEyes = new List<float[]>();
            if (rng.NextDouble() < .45)
            {
                int count = 2 + rng.Next(4);
                for (int e = 0; e < count; e++)
                    extraEyes.Add(new[] { Jit(.3f), .5f + (float)rng.NextDouble() * .2f, .045f + (float)rng.NextDouble() * .035f });
            }
            float open = Clamp01(mouthOpen);
            mouthRy *= .3f + .7f * open;
            mouthRx *= .82f + .18f * open;

            float[] buf = new float[size * size * 3];

            // Rachaduras (passeios aleatórios) pré-calculadas como segmentos.
            var cracks = new List<float[]>();
            int crackCount = 6 + rng.Next(5);
            for (int c = 0; c < crackCount; c++)
            {
                float x = Jit(.55f), y = Jit(.75f);
                float ang = (float)(rng.NextDouble() * Math.PI * 2);
                int steps = 8 + rng.Next(14);
                for (int s = 0; s < steps; s++)
                {
                    ang += Jit(.7f);
                    float nx = x + (float)Math.Cos(ang) * .045f, ny = y + (float)Math.Sin(ang) * .045f;
                    cracks.Add(new[] { x, y, nx, ny, .006f + (float)rng.NextDouble() * .006f });
                    x = nx; y = ny;
                }
            }
            // Escorrimentos pretos saindo dos olhos.
            var drips = new List<float[]>();
            for (int side = -1; side <= 1; side += 2)
                for (int d = 0; d < 4; d++)
                    drips.Add(new[] { side * eyeX + Jit(eyeRx * .7f), eyeY - eyeRy * .6f + side * eyeSkew, .012f + (float)rng.NextDouble() * .022f,
                                      .25f + (float)rng.NextDouble() * .65f });

            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                // Coordenadas -1..1 (y pra cima), com leve inclinação do rosto.
                float u0 = (px + .5f) / size * 2 - 1, v0 = (py + .5f) / size * 2 - 1;
                float cs = (float)Math.Cos(tilt), sn = (float)Math.Sin(tilt);
                float u = (u0 * cs - v0 * sn) * .74f, v = (u0 * sn + v0 * cs) * .74f; // rosto ocupa a tela

                float n1 = Fbm(u * 5, v * 5, noiseSeed), n2 = Fbm(u * 14, v * 14, noiseSeed + 7);

                // Fundo: quase preto com vinheta avermelhada.
                float vig = Clamp01(1.3f - (float)Math.Sqrt(u0 * u0 + v0 * v0));
                float r = .03f + .08f * (1 - vig), g = .02f, b = .025f;
                r += n2 * .02f; g += n2 * .015f; b += n2 * .02f;

                // Cabeça (oval irregular).
                float headDist = Ellipse(u, v + .05f, .62f + n1 * .05f, .86f + n1 * .04f);
                if (headDist < 1f)
                {
                    float shade = Clamp01(1.15f - headDist * headDist * .9f) * (.75f + .35f * n1);
                    r = .70f * shade + .05f; g = .72f * shade + .04f; b = .63f * shade + .03f;
                    // manchas
                    float blot = Clamp01((n2 - .55f) * 3f);
                    r -= blot * .25f; g -= blot * .28f; b -= blot * .22f;
                    // bochechas fundas
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float cheek = Ellipse(u - s * .3f, v + .15f, .14f, .22f);
                        if (cheek < 1f) { float k = (1 - cheek) * .35f; r -= k; g -= k; b -= k; }
                    }
                    // contorno escuro
                    float edge = Clamp01((headDist - .82f) * 6f);
                    r *= 1 - edge * .8f; g *= 1 - edge * .85f; b *= 1 - edge * .8f;
                }

                // Órbitas: buracos pretos com borda vermelho-escura e pupila branca minúscula.
                for (int s = -1; s <= 1; s += 2)
                {
                    float ey = eyeY + s * eyeSkew;
                    float e = Ellipse(u - s * eyeX, v - ey, eyeRx * (1 + (n1 - .5f) * .5f), eyeRy * (1 + (n1 - .5f) * .4f));
                    if (e < 1.25f && e >= 1f) { float k = (1.25f - e) * 4f; r = Lerp(r, .35f, k * .6f); g = Lerp(g, .02f, k * .7f); b = Lerp(b, .02f, k * .7f); }
                    if (e < 1f) { r = .015f; g = .01f; b = .012f; }
                    float pd = Ellipse(u - s * (eyeX - .02f) - pupilShift, v - ey + .04f, .022f, .022f);
                    if (pd < 1f) { r = g = b = 1f; }
                    else if (pd < 2.5f && e < 1f) { float glow = (2.5f - pd) / 1.5f * .25f; r += glow; g += glow; b += glow; }
                }

                // Olhos extras.
                foreach (var x in extraEyes)
                {
                    float e = Ellipse(u - x[0], v - x[1], x[2], x[2] * 1.3f);
                    if (e < 1.3f && e >= 1f) { r = Lerp(r, .3f, .5f); g = Lerp(g, .02f, .6f); b = Lerp(b, .02f, .6f); }
                    if (e < 1f) { r = .015f; g = .01f; b = .012f; }
                    if (Ellipse(u - x[0], v - x[1] + x[2] * .2f, x[2] * .22f, x[2] * .22f) < 1f) { r = g = b = 1f; }
                }

                // Escorrimentos.
                foreach (var d in drips)
                {
                    float dx = Math.Abs(u - d[0]);
                    float top = d[1], bottom = d[1] - d[3];
                    if (v < top && v > bottom && dx < d[2] * (1 - (top - v) / d[3] * .6f))
                    { r = Lerp(r, .02f, .9f); g = Lerp(g, .01f, .9f); b = Lerp(b, .01f, .9f); }
                }

                // Boca enorme: interior preto→vermelho e dentes serrilhados.
                float m = Ellipse(u, v - mouthY, mouthRx * (1 + n1 * .15f), mouthRy);
                if (m < 1f)
                {
                    float depth = Clamp01(m);
                    r = .25f * depth + .01f; g = .015f; b = .015f;
                    float ang = (float)Math.Atan2((v - mouthY) / mouthRy, u / mouthRx);
                    float tri = Math.Abs(Frac(ang / (float)(2 * Math.PI) * teeth * 2) * 2 - 1); // 0..1 serra
                    float vertical = Math.Abs((float)Math.Sin(ang));            // só em cima e embaixo
                    float irregular = .6f + .8f * Hash((int)Math.Floor(ang / (2 * Math.PI) * teeth * 2 + 50), 3, noiseSeed);
                    float toothLen = .42f * (1 - tri) * vertical * vertical * irregular;
                    float gum = m > .9f ? (m - .9f) * 10f : 0f;                 // gengiva vermelho-escura na borda
                    r = Lerp(r, .32f, gum); g = Lerp(g, .02f, gum); b = Lerp(b, .03f, gum);
                    if (toothLen > .02f && m > 1f - toothLen)
                    {
                        float t = .78f + n2 * .15f;
                        r = t; g = t * .92f; b = t * .62f;
                        if (m > .97f) { r *= .5f; g *= .5f; b *= .45f; }
                    }
                }

                buf[(py * size + px) * 3 + 0] = r;
                buf[(py * size + px) * 3 + 1] = g;
                buf[(py * size + px) * 3 + 2] = b;
            }

            // Rachaduras por cima de tudo.
            foreach (var seg in cracks) StampLine(buf, size, seg[0], seg[1], seg[2], seg[3], seg[4], tilt);

            // Grão + linhas de TV velha + saída RGBA.
            var outBytes = new byte[size * size * 4];
            var grain = new Random(seed * 31 + 5);
            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                int i = py * size + px;
                float g = (float)(grain.NextDouble() - .5) * .12f;
                float scan = (py % 4 == 0) ? .82f : 1f;
                for (int c = 0; c < 3; c++)
                {
                    float val = Clamp01((buf[i * 3 + c] + g) * scan);
                    if (negative) val = 1f - val;
                    outBytes[i * 4 + c] = (byte)(val * 255);
                }
                outBytes[i * 4 + 3] = 255;
            }
            return outBytes;
        }

        /// <summary>Quadro de chiado de TV (estática com faixas), para o susto "glitch".</summary>
        public static byte[] Static(int size, int seed)
        {
            var rng = new Random(seed);
            var outBytes = new byte[size * size * 4];
            float band = 0;
            for (int y = 0; y < size; y++)
            {
                if (rng.NextDouble() < .06) band = (float)rng.NextDouble() * .5f; // faixa clara/escura
                band *= .93f;
                for (int x = 0; x < size; x++)
                {
                    float v = (float)rng.NextDouble() * .85f + band;
                    if (y % 3 == 0) v *= .7f;
                    byte b = (byte)(Clamp01(v) * 255);
                    int i = (y * size + x) * 4;
                    outBytes[i] = b; outBytes[i + 1] = b; outBytes[i + 2] = b; outBytes[i + 3] = 255;
                }
            }
            return outBytes;
        }

        private static void StampLine(float[] buf, int size, float x0, float y0, float x1, float y1, float w, float tilt)
        {
            float cs = (float)Math.Cos(-tilt), sn = (float)Math.Sin(-tilt);
            // volta pro espaço da imagem (desfaz a inclinação)
            float ax = x0 * cs - y0 * sn, ay = x0 * sn + y0 * cs, bx = x1 * cs - y1 * sn, by = x1 * sn + y1 * cs;
            int steps = 24;
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                float cx = ax + (bx - ax) * t, cy = ay + (by - ay) * t;
                int pcx = (int)((cx + 1) / 2 * size), pcy = (int)((cy + 1) / 2 * size), rad = Math.Max(1, (int)(w * size / 2));
                for (int dy = -rad; dy <= rad; dy++)
                for (int dx = -rad; dx <= rad; dx++)
                {
                    int x = pcx + dx, y = pcy + dy;
                    if (x < 0 || y < 0 || x >= size || y >= size || dx * dx + dy * dy > rad * rad) continue;
                    int i = (y * size + x) * 3;
                    buf[i] *= .25f; buf[i + 1] *= .2f; buf[i + 2] *= .2f;
                }
            }
        }

        private static float Ellipse(float x, float y, float rx, float ry) => (float)Math.Sqrt(x * x / (rx * rx) + y * y / (ry * ry));
        private static float Clamp01(float x) => x < 0 ? 0 : x > 1 ? 1 : x;
        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        private static float Frac(float x) => x - (float)Math.Floor(x);

        /// <summary>Ruído de valor suave (0..1).</summary>
        private static float Noise(float x, float y, int seed)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float xf = x - xi, yf = y - yi;
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            float sx = xf * xf * (3 - 2 * xf), sy = yf * yf * (3 - 2 * yf);
            return Lerp(Lerp(a, b, sx), Lerp(c, d, sx), sy);
        }

        /// <summary>Ruído fractal (várias camadas): manchas orgânicas, sem "quadradinhos".</summary>
        private static float Fbm(float x, float y, int seed)
        {
            float sum = 0, amp = .5f;
            for (int o = 0; o < 4; o++) { sum += Noise(x, y, seed + o * 17) * amp; x *= 2.03f; y *= 2.03f; amp *= .5f; }
            return sum / .9375f;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
