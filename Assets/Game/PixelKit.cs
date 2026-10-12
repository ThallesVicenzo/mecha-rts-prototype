using UnityEngine;

namespace MechaRts.Game
{
    static class PixelKit
    {
        public static Sprite Mecha;
        public static Sprite Rifle;
        public static Sprite Heavy;
        public static Sprite Disc;
        public static Sprite Square;
        public static Sprite Ground;
        public static Sprite Block;

        static readonly string[] MechaRows =
        {
            "..............######................",
            ".............########...............",
            "............##++++++##..............",
            "...........###++++++###.............",
            "..........###############...........",
            ".........#################..........",
            "........####+++####+++####..........",
            ".......###++++####++++###...........",
            "......###+++++####+++++###..........",
            ".....########################.......",
            "....#######...####...###########....",
            "...######....####....############...",
            "...#####.....####.....###########...",
            "...######....####....############...",
            "....#######...####...###########....",
            ".....########################.......",
            "......######################........",
            ".......#####++++++++++#####.........",
            "......######++++++++++######........",
            ".....######++++++++++++######.......",
            "....######++++++++++++++######......",
            "....#####++++++++++++++++#####......",
            ".....####++++++++++++++++####.......",
            "......###++++++++++++++++###........",
            ".......##++++++++++++++++##.........",
            "........##++++++++++++++##..........",
            ".........#####++++++#####...........",
            ".........####++++++++####...........",
            "..........###++++++++###............",
            "..........##++++++++++##............",
            "...........#++++++++++#.............",
            "...........#++++++++++#............."
        };

        static readonly string[] RifleRows =
        {
            "............",
            "...######...",
            "..##++++##..",
            ".###++++###.",
            ".##########.",
            "....####....",
            "...##..##...",
            "...##..##...",
            "....#..#....",
            "....#..#....",
            "............",
            "............"
        };

        static readonly string[] HeavyRows =
        {
            "................",
            "....########....",
            "...##++++++##...",
            "..###++++++###..",
            ".##############.",
            ".#####....#####.",
            "######....######",
            ".#####....#####.",
            "..##############",
            "...############.",
            "....###....###..",
            "....##......##..",
            "....#........#..",
            "................"
        };

        public static void Ensure()
        {
            if (Mecha != null)
                return;
            Mecha = MakeSprite(MechaRows, 12f);
            Rifle = MakeSprite(RifleRows, 14f);
            Heavy = MakeSprite(HeavyRows, 13f);
            Disc = MakeDisc(16, 16f);
            Square = MakeSolid(4, 4f);
            Block = MakeBlock();
            Ground = MakeGround();
        }

        static Sprite MakeSprite(string[] rows, float pixelsPerUnit)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                string row = rows[height - 1 - y];
                for (int x = 0; x < width; x++)
                {
                    char c = x < row.Length ? row[x] : '.';
                    pixels[y * width + x] = Ink(c);
                }
            }

            var source = (Color32[])pixels.Clone();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (source[y * width + x].a != 0)
                        continue;
                    if (HasInkNeighbor(source, width, height, x, y))
                        pixels[y * width + x] = new Color32(10, 12, 18, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        static bool HasInkNeighbor(Color32[] pixels, int width, int height, int x, int y)
        {
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0)
                        continue;
                    int nx = x + ox;
                    int ny = y + oy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        continue;
                    if (pixels[ny * width + nx].a != 0)
                        return true;
                }
            }
            return false;
        }

        static Color32 Ink(char c)
        {
            switch (c)
            {
                case '#': return new Color32(196, 202, 210, 255);
                case '+': return new Color32(255, 255, 255, 255);
                case '=': return new Color32(70, 76, 88, 255);
                default: return new Color32(0, 0, 0, 0);
            }
        }

        static Sprite MakeDisc(int size, float pixelsPerUnit)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            float r = (size - 1) * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r;
                    float dy = y - r;
                    pixels[y * size + x] = dx * dx + dy * dy <= r * r
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        static Sprite MakeSolid(int size, float pixelsPerUnit)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        static Sprite MakeBlock()
        {
            const int size = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    bool top = y >= size - 2 && !edge;
                    pixels[y * size + x] = edge
                        ? new Color32(28, 32, 40, 255)
                        : top
                            ? new Color32(150, 158, 170, 255)
                            : new Color32(92, 98, 110, 255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeGround()
        {
            const int width = 160;
            const int height = 108;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            var floor = new Color32(36, 40, 48, 255);
            var line = new Color32(48, 54, 64, 255);
            var border = new Color32(86, 96, 112, 255);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool edge = x < 2 || y < 2 || x >= width - 2 || y >= height - 2;
                    bool grid = x % 4 == 0 || y % 4 == 0;
                    pixels[y * width + x] = edge ? border : grid ? line : floor;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 4f);
        }
    }
}
