namespace Engine.Rendering.Base;

/// <summary>
/// 8x16 bitmap font covering all 256 CP437 codepoints (IBM VGA ROM font).
/// Glyph data is defined as ASCII art for readability and easy debugging.
/// </summary>
public static class MiniBitmapFont
{
    public const int GlyphWidth = 8;
    public const int GlyphHeight = 16;

    // ── CP437 Glyph Index Constants ────────────────────────────────────

    public const int Null = 0x00;
    public const int SmileyFace = 0x01;
    public const int SmileyFaceInv = 0x02;
    public const int Heart = 0x03;
    public const int Diamond = 0x04;
    public const int Club = 0x05;
    public const int Spade = 0x06;
    public const int Bullet = 0x07;
    public const int BulletInv = 0x08;
    public const int Circle = 0x09;
    public const int CircleInv = 0x0A;
    public const int Male = 0x0B;
    public const int Female = 0x0C;
    public const int EighthNote = 0x0D;
    public const int BeamedNotes = 0x0E;
    public const int Sun = 0x0F;
    public const int RightPointer = 0x10;
    public const int LeftPointer = 0x11;
    public const int UpDownArrow = 0x12;
    public const int DoubleExclaim = 0x13;
    public const int Pilcrow = 0x14;
    public const int Section = 0x15;
    public const int ThickUnderline = 0x16;
    public const int UpDownArrowUL = 0x17;
    public const int UpArrow = 0x18;
    public const int DownArrow = 0x19;
    public const int RightArrow = 0x1A;
    public const int LeftArrow = 0x1B;
    public const int RightAngle = 0x1C;
    public const int LeftRightArrow = 0x1D;
    public const int UpTriangle = 0x1E;
    public const int DownTriangle = 0x1F;
    public const int Space = 0x20;
    public const int Exclamation = 0x21;
    public const int DoubleQuote = 0x22;
    public const int Hash = 0x23;
    public const int Dollar = 0x24;
    public const int Percent = 0x25;
    public const int Ampersand = 0x26;
    public const int Apostrophe = 0x27;
    public const int LeftParen = 0x28;
    public const int RightParen = 0x29;
    public const int Asterisk = 0x2A;
    public const int Plus = 0x2B;
    public const int Comma = 0x2C;
    public const int Hyphen = 0x2D;
    public const int Period = 0x2E;
    public const int Slash = 0x2F;
    public const int Digit0 = 0x30;
    public const int Digit1 = 0x31;
    public const int Digit2 = 0x32;
    public const int Digit3 = 0x33;
    public const int Digit4 = 0x34;
    public const int Digit5 = 0x35;
    public const int Digit6 = 0x36;
    public const int Digit7 = 0x37;
    public const int Digit8 = 0x38;
    public const int Digit9 = 0x39;
    public const int Colon = 0x3A;
    public const int Semicolon = 0x3B;
    public const int LessThan = 0x3C;
    public const int EqualsSign = 0x3D;
    public const int GreaterThan = 0x3E;
    public const int Question = 0x3F;
    public const int At = 0x40;
    public const int UpperA = 0x41;
    public const int UpperB = 0x42;
    public const int UpperC = 0x43;
    public const int UpperD = 0x44;
    public const int UpperE = 0x45;
    public const int UpperF = 0x46;
    public const int UpperG = 0x47;
    public const int UpperH = 0x48;
    public const int UpperI = 0x49;
    public const int UpperJ = 0x4A;
    public const int UpperK = 0x4B;
    public const int UpperL = 0x4C;
    public const int UpperM = 0x4D;
    public const int UpperN = 0x4E;
    public const int UpperO = 0x4F;
    public const int UpperP = 0x50;
    public const int UpperQ = 0x51;
    public const int UpperR = 0x52;
    public const int UpperS = 0x53;
    public const int UpperT = 0x54;
    public const int UpperU = 0x55;
    public const int UpperV = 0x56;
    public const int UpperW = 0x57;
    public const int UpperX = 0x58;
    public const int UpperY = 0x59;
    public const int UpperZ = 0x5A;
    public const int LeftBracket = 0x5B;
    public const int Backslash = 0x5C;
    public const int RightBracket = 0x5D;
    public const int Caret = 0x5E;
    public const int Underscore = 0x5F;
    public const int Backtick = 0x60;
    public const int LowerA = 0x61;
    public const int LowerB = 0x62;
    public const int LowerC = 0x63;
    public const int LowerD = 0x64;
    public const int LowerE = 0x65;
    public const int LowerF = 0x66;
    public const int LowerG = 0x67;
    public const int LowerH = 0x68;
    public const int LowerI = 0x69;
    public const int LowerJ = 0x6A;
    public const int LowerK = 0x6B;
    public const int LowerL = 0x6C;
    public const int LowerM = 0x6D;
    public const int LowerN = 0x6E;
    public const int LowerO = 0x6F;
    public const int LowerP = 0x70;
    public const int LowerQ = 0x71;
    public const int LowerR = 0x72;
    public const int LowerS = 0x73;
    public const int LowerT = 0x74;
    public const int LowerU = 0x75;
    public const int LowerV = 0x76;
    public const int LowerW = 0x77;
    public const int LowerX = 0x78;
    public const int LowerY = 0x79;
    public const int LowerZ = 0x7A;
    public const int LeftBrace = 0x7B;
    public const int Pipe = 0x7C;
    public const int RightBrace = 0x7D;
    public const int Tilde = 0x7E;
    public const int House = 0x7F;
    public const int CedillaC = 0x80;
    public const int UmlautLowerU = 0x81;
    public const int AcuteLowerE = 0x82;
    public const int CircumLowerA = 0x83;
    public const int UmlautLowerA = 0x84;
    public const int GraveLowerA = 0x85;
    public const int RingLowerA = 0x86;
    public const int CedillaLowerC = 0x87;
    public const int CircumLowerE = 0x88;
    public const int UmlautLowerE = 0x89;
    public const int GraveLowerE = 0x8A;
    public const int UmlautLowerI = 0x8B;
    public const int CircumLowerI = 0x8C;
    public const int GraveLowerI = 0x8D;
    public const int UmlautUpperA = 0x8E;
    public const int RingUpperA = 0x8F;
    public const int AcuteUpperE = 0x90;
    public const int LowerAE = 0x91;
    public const int UpperAE = 0x92;
    public const int CircumLowerO = 0x93;
    public const int UmlautLowerO = 0x94;
    public const int GraveLowerO = 0x95;
    public const int CircumLowerU = 0x96;
    public const int GraveLowerU = 0x97;
    public const int UmlautLowerY = 0x98;
    public const int UmlautUpperO = 0x99;
    public const int UmlautUpperU = 0x9A;
    public const int Cent = 0x9B;
    public const int Pound = 0x9C;
    public const int Yen = 0x9D;
    public const int Peseta = 0x9E;
    public const int FlorinSign = 0x9F;
    public const int AcuteLowerA = 0xA0;
    public const int AcuteLowerI = 0xA1;
    public const int AcuteLowerO = 0xA2;
    public const int AcuteLowerU = 0xA3;
    public const int TildeLowerN = 0xA4;
    public const int TildeUpperN = 0xA5;
    public const int FemOrdinal = 0xA6;
    public const int MascOrdinal = 0xA7;
    public const int InvQuestion = 0xA8;
    public const int ReversedNot = 0xA9;
    public const int Not = 0xAA;
    public const int Half = 0xAB;
    public const int Quarter = 0xAC;
    public const int InvExclamation = 0xAD;
    public const int LeftAngleQuote = 0xAE;
    public const int RightAngleQuote = 0xAF;
    public const int LightShade = 0xB0;
    public const int MediumShade = 0xB1;
    public const int DarkShade = 0xB2;
    public const int BoxVert = 0xB3;
    public const int BoxVertLeft = 0xB4;
    public const int BoxVertLeftDbl = 0xB5;
    public const int BoxDblVertLeft = 0xB6;
    public const int BoxDblDownLeft = 0xB7;
    public const int BoxDownLeftDbl = 0xB8;
    public const int BoxDblVertLeftDbl = 0xB9;
    public const int BoxDblVert = 0xBA;
    public const int BoxDblDownLeftDbl = 0xBB;
    public const int BoxDblUpLeft = 0xBC;
    public const int BoxDblUpLeftDbl = 0xBD;
    public const int BoxUpLeftDbl = 0xBE;
    public const int BoxDownLeft = 0xBF;
    public const int BoxUpRight = 0xC0;
    public const int BoxUpHoriz = 0xC1;
    public const int BoxDownHoriz = 0xC2;
    public const int BoxVertRight = 0xC3;
    public const int BoxHoriz = 0xC4;
    public const int BoxCross = 0xC5;
    public const int BoxVertRightDbl = 0xC6;
    public const int BoxDblVertRight = 0xC7;
    public const int BoxDblUpRight = 0xC8;
    public const int BoxDblDownRight = 0xC9;
    public const int BoxDblUpHoriz = 0xCA;
    public const int BoxDblDownHoriz = 0xCB;
    public const int BoxDblVertRightDbl = 0xCC;
    public const int BoxDblHoriz = 0xCD;
    public const int BoxDblCross = 0xCE;
    public const int BoxUpHorizDbl = 0xCF;
    public const int BoxDblUpHorizDbl = 0xD0;
    public const int BoxDownHorizDbl = 0xD1;
    public const int BoxDblDownHorizDbl = 0xD2;
    public const int BoxDblUpRightDbl = 0xD3;
    public const int BoxUpRightDbl = 0xD4;
    public const int BoxDownRightDbl = 0xD5;
    public const int BoxDblDownRightDbl = 0xD6;
    public const int BoxDblVertHoriz = 0xD7;
    public const int BoxVertHorizDbl = 0xD8;
    public const int BoxUpLeft = 0xD9;
    public const int BoxDownRight = 0xDA;
    public const int FullBlock = 0xDB;
    public const int LowerHalfBlock = 0xDC;
    public const int LeftHalfBlock = 0xDD;
    public const int RightHalfBlock = 0xDE;
    public const int UpperHalfBlock = 0xDF;
    public const int Alpha = 0xE0;
    public const int Beta = 0xE1;
    public const int Gamma = 0xE2;
    public const int Pi = 0xE3;
    public const int UpperSigma = 0xE4;
    public const int LowerSigma = 0xE5;
    public const int Mu = 0xE6;
    public const int Tau = 0xE7;
    public const int UpperPhi = 0xE8;
    public const int Theta = 0xE9;
    public const int Omega = 0xEA;
    public const int Delta = 0xEB;
    public const int Infinity = 0xEC;
    public const int LowerPhi = 0xED;
    public const int Epsilon = 0xEE;
    public const int Intersection = 0xEF;
    public const int Identical = 0xF0;
    public const int PlusMinus = 0xF1;
    public const int GreaterEqual = 0xF2;
    public const int LessEqual = 0xF3;
    public const int UpperIntegral = 0xF4;
    public const int LowerIntegral = 0xF5;
    public const int Division = 0xF6;
    public const int ApproxEqual = 0xF7;
    public const int Degree = 0xF8;
    public const int BulletOp = 0xF9;
    public const int MiddleDot = 0xFA;
    public const int SquareRoot = 0xFB;
    public const int SuperscriptN = 0xFC;
    public const int Superscript2 = 0xFD;
    public const int FilledSquare = 0xFE;
    public const int NBSP = 0xFF;

    /// <summary>Maps CP437 byte index (0-255) to its Unicode character.</summary>
    public static readonly char[] Cp437ToUnicode = CreateCp437Map();

    private static char[] CreateCp437Map()
    {
        var map = new char[256];

        // Control / graphic characters (0x00-0x1F)
        map[Null] = '\0';
        map[SmileyFace] = '\u263A';
        map[SmileyFaceInv] = '\u263B';
        map[Heart] = '\u2665';
        map[Diamond] = '\u2666';
        map[Club] = '\u2663';
        map[Spade] = '\u2660';
        map[Bullet] = '\u2022';
        map[BulletInv] = '\u25D8';
        map[Circle] = '\u25CB';
        map[CircleInv] = '\u25D9';
        map[Male] = '\u2642';
        map[Female] = '\u2640';
        map[EighthNote] = '\u266A';
        map[BeamedNotes] = '\u266B';
        map[Sun] = '\u263C';
        map[RightPointer] = '\u25BA';
        map[LeftPointer] = '\u25C4';
        map[UpDownArrow] = '\u2195';
        map[DoubleExclaim] = '\u203C';
        map[Pilcrow] = '\u00B6';
        map[Section] = '\u00A7';
        map[ThickUnderline] = '\u25AC';
        map[UpDownArrowUL] = '\u21A8';
        map[UpArrow] = '\u2191';
        map[DownArrow] = '\u2193';
        map[RightArrow] = '\u2192';
        map[LeftArrow] = '\u2190';
        map[RightAngle] = '\u221F';
        map[LeftRightArrow] = '\u2194';
        map[UpTriangle] = '\u25B2';
        map[DownTriangle] = '\u25BC';

        // Standard ASCII (0x20-0x7E) — identity mapping
        for (int i = 0x20; i <= 0x7E; i++)
            map[i] = (char)i;

        // House (0x7F)
        map[House] = '\u2302';

        // Extended characters (0x80-0xFF)
        map[CedillaC] = '\u00C7';
        map[UmlautLowerU] = '\u00FC';
        map[AcuteLowerE] = '\u00E9';
        map[CircumLowerA] = '\u00E2';
        map[UmlautLowerA] = '\u00E4';
        map[GraveLowerA] = '\u00E0';
        map[RingLowerA] = '\u00E5';
        map[CedillaLowerC] = '\u00E7';
        map[CircumLowerE] = '\u00EA';
        map[UmlautLowerE] = '\u00EB';
        map[GraveLowerE] = '\u00E8';
        map[UmlautLowerI] = '\u00EF';
        map[CircumLowerI] = '\u00EE';
        map[GraveLowerI] = '\u00EC';
        map[UmlautUpperA] = '\u00C4';
        map[RingUpperA] = '\u00C5';
        map[AcuteUpperE] = '\u00C9';
        map[LowerAE] = '\u00E6';
        map[UpperAE] = '\u00C6';
        map[CircumLowerO] = '\u00F4';
        map[UmlautLowerO] = '\u00F6';
        map[GraveLowerO] = '\u00F2';
        map[CircumLowerU] = '\u00FB';
        map[GraveLowerU] = '\u00F9';
        map[UmlautLowerY] = '\u00FF';
        map[UmlautUpperO] = '\u00D6';
        map[UmlautUpperU] = '\u00DC';
        map[Cent] = '\u00A2';
        map[Pound] = '\u00A3';
        map[Yen] = '\u00A5';
        map[Peseta] = '\u20A7';
        map[FlorinSign] = '\u0192';
        map[AcuteLowerA] = '\u00E1';
        map[AcuteLowerI] = '\u00ED';
        map[AcuteLowerO] = '\u00F3';
        map[AcuteLowerU] = '\u00FA';
        map[TildeLowerN] = '\u00F1';
        map[TildeUpperN] = '\u00D1';
        map[FemOrdinal] = '\u00AA';
        map[MascOrdinal] = '\u00BA';
        map[InvQuestion] = '\u00BF';
        map[ReversedNot] = '\u2310';
        map[Not] = '\u00AC';
        map[Half] = '\u00BD';
        map[Quarter] = '\u00BC';
        map[InvExclamation] = '\u00A1';
        map[LeftAngleQuote] = '\u00AB';
        map[RightAngleQuote] = '\u00BB';
        map[LightShade] = '\u2591';
        map[MediumShade] = '\u2592';
        map[DarkShade] = '\u2593';
        map[BoxVert] = '\u2502';
        map[BoxVertLeft] = '\u2524';
        map[BoxVertLeftDbl] = '\u2561';
        map[BoxDblVertLeft] = '\u2562';
        map[BoxDblDownLeft] = '\u2556';
        map[BoxDownLeftDbl] = '\u2555';
        map[BoxDblVertLeftDbl] = '\u2563';
        map[BoxDblVert] = '\u2551';
        map[BoxDblDownLeftDbl] = '\u2557';
        map[BoxDblUpLeft] = '\u255D';
        map[BoxDblUpLeftDbl] = '\u255C';
        map[BoxUpLeftDbl] = '\u255B';
        map[BoxDownLeft] = '\u2510';
        map[BoxUpRight] = '\u2514';
        map[BoxUpHoriz] = '\u2534';
        map[BoxDownHoriz] = '\u252C';
        map[BoxVertRight] = '\u251C';
        map[BoxHoriz] = '\u2500';
        map[BoxCross] = '\u253C';
        map[BoxVertRightDbl] = '\u255E';
        map[BoxDblVertRight] = '\u255F';
        map[BoxDblUpRight] = '\u255A';
        map[BoxDblDownRight] = '\u2554';
        map[BoxDblUpHoriz] = '\u2569';
        map[BoxDblDownHoriz] = '\u2566';
        map[BoxDblVertRightDbl] = '\u2560';
        map[BoxDblHoriz] = '\u2550';
        map[BoxDblCross] = '\u256C';
        map[BoxUpHorizDbl] = '\u2567';
        map[BoxDblUpHorizDbl] = '\u2568';
        map[BoxDownHorizDbl] = '\u2564';
        map[BoxDblDownHorizDbl] = '\u2565';
        map[BoxDblUpRightDbl] = '\u2559';
        map[BoxUpRightDbl] = '\u2558';
        map[BoxDownRightDbl] = '\u2552';
        map[BoxDblDownRightDbl] = '\u2553';
        map[BoxDblVertHoriz] = '\u256B';
        map[BoxVertHorizDbl] = '\u256A';
        map[BoxUpLeft] = '\u2518';
        map[BoxDownRight] = '\u250C';
        map[FullBlock] = '\u2588';
        map[LowerHalfBlock] = '\u2584';
        map[LeftHalfBlock] = '\u258C';
        map[RightHalfBlock] = '\u2590';
        map[UpperHalfBlock] = '\u2580';
        map[Alpha] = '\u03B1';
        map[Beta] = '\u00DF';
        map[Gamma] = '\u0393';
        map[Pi] = '\u03C0';
        map[UpperSigma] = '\u03A3';
        map[LowerSigma] = '\u03C3';
        map[Mu] = '\u00B5';
        map[Tau] = '\u03C4';
        map[UpperPhi] = '\u03A6';
        map[Theta] = '\u0398';
        map[Omega] = '\u03A9';
        map[Delta] = '\u03B4';
        map[Infinity] = '\u221E';
        map[LowerPhi] = '\u03C6';
        map[Epsilon] = '\u03B5';
        map[Intersection] = '\u2229';
        map[Identical] = '\u2261';
        map[PlusMinus] = '\u00B1';
        map[GreaterEqual] = '\u2265';
        map[LessEqual] = '\u2264';
        map[UpperIntegral] = '\u2320';
        map[LowerIntegral] = '\u2321';
        map[Division] = '\u00F7';
        map[ApproxEqual] = '\u2248';
        map[Degree] = '\u00B0';
        map[BulletOp] = '\u2219';
        map[MiddleDot] = '\u00B7';
        map[SquareRoot] = '\u221A';
        map[SuperscriptN] = '\u207F';
        map[Superscript2] = '\u00B2';
        map[FilledSquare] = '\u25A0';
        map[NBSP] = '\u00A0';

        return map;
    }

    // ── Font Bitmap Data (ASCII Art) ───────────────────────────────────
    // Each glyph: 16 rows of 8 pixels. '#' = pixel on, '.' = pixel off.
    // Bit 7 (MSB) = leftmost pixel.

    private static readonly byte[] FontData = BuildFontData();

    private static byte[] BuildFontData()
    {
        var data = new byte[256 * GlyphHeight];
        int offset = 0;

        void G(
            string r0,  string r1,  string r2,  string r3,
            string r4,  string r5,  string r6,  string r7,
            string r8,  string r9,  string rA,  string rB,
            string rC,  string rD,  string rE,  string rF)
        {
            ReadOnlySpan<string> rows = [r0, r1, r2, r3, r4, r5, r6, r7,
                                         r8, r9, rA, rB, rC, rD, rE, rF];
            foreach (var row in rows)
            {
                byte b = 0;
                for (int c = 0; c < GlyphWidth; c++)
                    if (row[c] == '#') b |= (byte)(0x80 >> c);
                data[offset++] = b;
            }
        }

        // 0x00 - Null
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x01 - SmileyFace
        G("........",
          "........",
          ".######.",
          "#......#",
          "#.#..#.#",
          "#......#",
          "#......#",
          "#.####.#",
          "#..##..#",
          "#......#",
          "#......#",
          ".######.",
          "........",
          "........",
          "........",
          "........");

        // 0x02 - SmileyFaceInv
        G("........",
          "........",
          ".######.",
          "########",
          "##.##.##",
          "########",
          "########",
          "##....##",
          "###..###",
          "########",
          "########",
          ".######.",
          "........",
          "........",
          "........",
          "........");

        // 0x03 - Heart
        G("........",
          "........",
          "........",
          "........",
          ".##.##..",
          "#######.",
          "#######.",
          "#######.",
          "#######.",
          ".#####..",
          "..###...",
          "...#....",
          "........",
          "........",
          "........",
          "........");

        // 0x04 - Diamond
        G("........",
          "........",
          "........",
          "........",
          "...#....",
          "..###...",
          ".#####..",
          "#######.",
          ".#####..",
          "..###...",
          "...#....",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x05 - Club
        G("........",
          "........",
          "........",
          "...##...",
          "..####..",
          "..####..",
          "###..###",
          "###..###",
          "###..###",
          "...##...",
          "...##...",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0x06 - Spade
        G("........",
          "........",
          "........",
          "...##...",
          "..####..",
          ".######.",
          "########",
          "########",
          ".######.",
          "...##...",
          "...##...",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0x07 - Bullet
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "...##...",
          "..####..",
          "..####..",
          "...##...",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x08 - BulletInv
        G("########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "###..###",
          "##....##",
          "##....##",
          "###..###",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########");

        // 0x09 - Circle
        G("........",
          "........",
          "........",
          "........",
          "........",
          "..####..",
          ".##..##.",
          ".#....#.",
          ".#....#.",
          ".##..##.",
          "..####..",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x0A - CircleInv
        G("########",
          "########",
          "########",
          "########",
          "########",
          "##....##",
          "#..##..#",
          "#.####.#",
          "#.####.#",
          "#..##..#",
          "##....##",
          "########",
          "########",
          "########",
          "########",
          "########");

        // 0x0B - Male
        G("........",
          "........",
          "...####.",
          "....###.",
          "...##.#.",
          "..##..#.",
          ".####...",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".####...",
          "........",
          "........",
          "........",
          "........");

        // 0x0C - Female
        G("........",
          "........",
          "..####..",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          "..####..",
          "...##...",
          ".######.",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "........");

        // 0x0D - EighthNote
        G("........",
          "........",
          "..######",
          "..##..##",
          "..######",
          "..##....",
          "..##....",
          "..##....",
          "..##....",
          ".###....",
          "####....",
          "###.....",
          "........",
          "........",
          "........",
          "........");

        // 0x0E - BeamedNotes
        G("........",
          "........",
          ".#######",
          ".##...##",
          ".#######",
          ".##...##",
          ".##...##",
          ".##...##",
          ".##...##",
          ".##..###",
          "###..###",
          "###..##.",
          "##......",
          "........",
          "........",
          "........");

        // 0x0F - Sun
        G("........",
          "........",
          "........",
          "...##...",
          "...##...",
          "##.##.##",
          "..####..",
          "###..###",
          "..####..",
          "##.##.##",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "........");

        // 0x10 - RightPointer
        G("........",
          "#.......",
          "##......",
          "###.....",
          "####....",
          "#####...",
          "#######.",
          "#####...",
          "####....",
          "###.....",
          "##......",
          "#.......",
          "........",
          "........",
          "........",
          "........");

        // 0x11 - LeftPointer
        G("........",
          "......#.",
          ".....##.",
          "....###.",
          "...####.",
          "..#####.",
          "#######.",
          "..#####.",
          "...####.",
          "....###.",
          ".....##.",
          "......#.",
          "........",
          "........",
          "........",
          "........");

        // 0x12 - UpDownArrow
        G("........",
          "........",
          "...##...",
          "..####..",
          ".######.",
          "...##...",
          "...##...",
          "...##...",
          ".######.",
          "..####..",
          "...##...",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x13 - DoubleExclaim
        G("........",
          "........",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          "........",
          ".##..##.",
          ".##..##.",
          "........",
          "........",
          "........",
          "........");

        // 0x14 - Pilcrow
        G("........",
          "........",
          ".#######",
          "##.##.##",
          "##.##.##",
          "##.##.##",
          ".####.##",
          "...##.##",
          "...##.##",
          "...##.##",
          "...##.##",
          "...##.##",
          "........",
          "........",
          "........",
          "........");

        // 0x15 - Section
        G("........",
          ".#####..",
          "##...##.",
          ".##.....",
          "..###...",
          ".##.##..",
          "##...##.",
          "##...##.",
          ".##.##..",
          "..###...",
          "....##..",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........");

        // 0x16 - ThickUnderline
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "#######.",
          "#######.",
          "#######.",
          "#######.",
          "........",
          "........",
          "........",
          "........");

        // 0x17 - UpDownArrowUL
        G("........",
          "........",
          "...##...",
          "..####..",
          ".######.",
          "...##...",
          "...##...",
          "...##...",
          ".######.",
          "..####..",
          "...##...",
          ".######.",
          "........",
          "........",
          "........",
          "........");

        // 0x18 - UpArrow
        G("........",
          "........",
          "...##...",
          "..####..",
          ".######.",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "........");

        // 0x19 - DownArrow
        G("........",
          "........",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          ".######.",
          "..####..",
          "...##...",
          "........",
          "........",
          "........",
          "........");

        // 0x1A - RightArrow
        G("........",
          "........",
          "........",
          "........",
          "........",
          "...##...",
          "....##..",
          "#######.",
          "....##..",
          "...##...",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x1B - LeftArrow
        G("........",
          "........",
          "........",
          "........",
          "........",
          "..##....",
          ".##.....",
          "#######.",
          ".##.....",
          "..##....",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x1C - RightAngle
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "##......",
          "##......",
          "##......",
          "#######.",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x1D - LeftRightArrow
        G("........",
          "........",
          "........",
          "........",
          "........",
          "..#..#..",
          ".##..##.",
          "########",
          ".##..##.",
          "..#..#..",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x1E - UpTriangle
        G("........",
          "........",
          "........",
          "........",
          "...#....",
          "..###...",
          "..###...",
          ".#####..",
          ".#####..",
          "#######.",
          "#######.",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x1F - DownTriangle
        G("........",
          "........",
          "........",
          "........",
          "#######.",
          "#######.",
          ".#####..",
          ".#####..",
          "..###...",
          "..###...",
          "...#....",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x20 - Space
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x21 - Exclamation
        G("........",
          "........",
          "...##...",
          "..####..",
          "..####..",
          "..####..",
          "...##...",
          "...##...",
          "...##...",
          "........",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "........");

        // 0x22 - DoubleQuote
        G("........",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          "..#..#..",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x23 - Hash
        G("........",
          "........",
          "........",
          ".##.##..",
          ".##.##..",
          "#######.",
          ".##.##..",
          ".##.##..",
          ".##.##..",
          "#######.",
          ".##.##..",
          ".##.##..",
          "........",
          "........",
          "........",
          "........");

        // 0x24 - Dollar
        G("...##...",
          "...##...",
          ".#####..",
          "##...##.",
          "##....#.",
          "##......",
          ".#####..",
          ".....##.",
          ".....##.",
          "#....##.",
          "##...##.",
          ".#####..",
          "...##...",
          "...##...",
          "........",
          "........");

        // 0x25 - Percent
        G("........",
          "........",
          "........",
          "........",
          "##....#.",
          "##...##.",
          "....##..",
          "...##...",
          "..##....",
          ".##.....",
          "##...##.",
          "#....##.",
          "........",
          "........",
          "........",
          "........");

        // 0x26 - Ampersand
        G("........",
          "........",
          "..###...",
          ".##.##..",
          ".##.##..",
          "..###...",
          ".###.##.",
          "##.###..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".###.##.",
          "........",
          "........",
          "........",
          "........");

        // 0x27 - Apostrophe
        G("........",
          "..##....",
          "..##....",
          "..##....",
          ".##.....",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x28 - LeftParen
        G("........",
          "........",
          "....##..",
          "...##...",
          "..##....",
          "..##....",
          "..##....",
          "..##....",
          "..##....",
          "..##....",
          "...##...",
          "....##..",
          "........",
          "........",
          "........",
          "........");

        // 0x29 - RightParen
        G("........",
          "........",
          "..##....",
          "...##...",
          "....##..",
          "....##..",
          "....##..",
          "....##..",
          "....##..",
          "....##..",
          "...##...",
          "..##....",
          "........",
          "........",
          "........",
          "........");

        // 0x2A - Asterisk
        G("........",
          "........",
          "........",
          "........",
          "........",
          ".##..##.",
          "..####..",
          "########",
          "..####..",
          ".##..##.",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x2B - Plus
        G("........",
          "........",
          "........",
          "........",
          "........",
          "...##...",
          "...##...",
          ".######.",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x2C - Comma
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "...##...",
          "...##...",
          "...##...",
          "..##....",
          "........",
          "........",
          "........");

        // 0x2D - Hyphen
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "#######.",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x2E - Period
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "........");

        // 0x2F - Slash
        G("........",
          "........",
          "........",
          "........",
          "......#.",
          ".....##.",
          "....##..",
          "...##...",
          "..##....",
          ".##.....",
          "##......",
          "#.......",
          "........",
          "........",
          "........",
          "........");

        // 0x30 - Digit0
        G("........",
          "........",
          "..####..",
          ".##..##.",
          "##....##",
          "##....##",
          "##.##.##",
          "##.##.##",
          "##....##",
          "##....##",
          ".##..##.",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0x31 - Digit1
        G("........",
          "........",
          "...##...",
          "..###...",
          ".####...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          ".######.",
          "........",
          "........",
          "........",
          "........");

        // 0x32 - Digit2
        G("........",
          "........",
          ".#####..",
          "##...##.",
          ".....##.",
          "....##..",
          "...##...",
          "..##....",
          ".##.....",
          "##......",
          "##...##.",
          "#######.",
          "........",
          "........",
          "........",
          "........");

        // 0x33 - Digit3
        G("........",
          "........",
          ".#####..",
          "##...##.",
          ".....##.",
          ".....##.",
          "..####..",
          ".....##.",
          ".....##.",
          ".....##.",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x34 - Digit4
        G("........",
          "........",
          "....##..",
          "...###..",
          "..####..",
          ".##.##..",
          "##..##..",
          "#######.",
          "....##..",
          "....##..",
          "....##..",
          "...####.",
          "........",
          "........",
          "........",
          "........");

        // 0x35 - Digit5
        G("........",
          "........",
          "#######.",
          "##......",
          "##......",
          "##......",
          "######..",
          ".....##.",
          ".....##.",
          ".....##.",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x36 - Digit6
        G("........",
          "........",
          "..###...",
          ".##.....",
          "##......",
          "##......",
          "######..",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x37 - Digit7
        G("........",
          "........",
          "#######.",
          "##...##.",
          ".....##.",
          ".....##.",
          "....##..",
          "...##...",
          "..##....",
          "..##....",
          "..##....",
          "..##....",
          "........",
          "........",
          "........",
          "........");

        // 0x38 - Digit8
        G("........",
          "........",
          ".#####..",
          "##...##.",
          "##...##.",
          "##...##.",
          ".#####..",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x39 - Digit9
        G("........",
          "........",
          ".#####..",
          "##...##.",
          "##...##.",
          "##...##.",
          ".######.",
          ".....##.",
          ".....##.",
          ".....##.",
          "....##..",
          ".####...",
          "........",
          "........",
          "........",
          "........");

        // 0x3A - Colon
        G("........",
          "........",
          "........",
          "........",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x3B - Semicolon
        G("........",
          "........",
          "........",
          "........",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "...##...",
          "...##...",
          "..##....",
          "........",
          "........",
          "........",
          "........");

        // 0x3C - LessThan
        G("........",
          "........",
          "........",
          ".....##.",
          "....##..",
          "...##...",
          "..##....",
          ".##.....",
          "..##....",
          "...##...",
          "....##..",
          ".....##.",
          "........",
          "........",
          "........",
          "........");

        // 0x3D - Equals
        G("........",
          "........",
          "........",
          "........",
          "........",
          ".######.",
          "........",
          "........",
          ".######.",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x3E - GreaterThan
        G("........",
          "........",
          "........",
          ".##.....",
          "..##....",
          "...##...",
          "....##..",
          ".....##.",
          "....##..",
          "...##...",
          "..##....",
          ".##.....",
          "........",
          "........",
          "........",
          "........");

        // 0x3F - Question
        G("........",
          "........",
          ".#####..",
          "##...##.",
          "##...##.",
          "....##..",
          "...##...",
          "...##...",
          "...##...",
          "........",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "........");

        // 0x40 - At
        G("........",
          "........",
          "........",
          ".#####..",
          "##...##.",
          "##...##.",
          "##.####.",
          "##.####.",
          "##.####.",
          "##.###..",
          "##......",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x41 - UpperA
        G("........",
          "........",
          "...#....",
          "..###...",
          ".##.##..",
          "##...##.",
          "##...##.",
          "#######.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "........",
          "........",
          "........",
          "........");

        // 0x42 - UpperB
        G("........",
          "........",
          "######..",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".#####..",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          "######..",
          "........",
          "........",
          "........",
          "........");

        // 0x43 - UpperC
        G("........",
          "........",
          "..####..",
          ".##..##.",
          "##....#.",
          "##......",
          "##......",
          "##......",
          "##......",
          "##....#.",
          ".##..##.",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0x44 - UpperD
        G("........",
          "........",
          "#####...",
          ".##.##..",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##.##..",
          "#####...",
          "........",
          "........",
          "........",
          "........");

        // 0x45 - UpperE
        G("........",
          "........",
          "#######.",
          ".##..##.",
          ".##...#.",
          ".##.#...",
          ".####...",
          ".##.#...",
          ".##.....",
          ".##...#.",
          ".##..##.",
          "#######.",
          "........",
          "........",
          "........",
          "........");

        // 0x46 - UpperF
        G("........",
          "........",
          "#######.",
          ".##..##.",
          ".##...#.",
          ".##.#...",
          ".####...",
          ".##.#...",
          ".##.....",
          ".##.....",
          ".##.....",
          "####....",
          "........",
          "........",
          "........",
          "........");

        // 0x47 - UpperG
        G("........",
          "........",
          "..####..",
          ".##..##.",
          "##....#.",
          "##......",
          "##......",
          "##.####.",
          "##...##.",
          "##...##.",
          ".##..##.",
          "..###.#.",
          "........",
          "........",
          "........",
          "........");

        // 0x48 - UpperH
        G("........",
          "........",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "#######.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "........",
          "........",
          "........",
          "........");

        // 0x49 - UpperI
        G("........",
          "........",
          "..####..",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0x4A - UpperJ
        G("........",
          "........",
          "...####.",
          "....##..",
          "....##..",
          "....##..",
          "....##..",
          "....##..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".####...",
          "........",
          "........",
          "........",
          "........");

        // 0x4B - UpperK
        G("........",
          "........",
          "###..##.",
          ".##..##.",
          ".##..##.",
          ".##.##..",
          ".####...",
          ".####...",
          ".##.##..",
          ".##..##.",
          ".##..##.",
          "###..##.",
          "........",
          "........",
          "........",
          "........");

        // 0x4C - UpperL
        G("........",
          "........",
          "####....",
          ".##.....",
          ".##.....",
          ".##.....",
          ".##.....",
          ".##.....",
          ".##.....",
          ".##...#.",
          ".##..##.",
          "#######.",
          "........",
          "........",
          "........",
          "........");

        // 0x4D - UpperM
        G("........",
          "........",
          "##...##.",
          "###.###.",
          "#######.",
          "#######.",
          "##.#.##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "........",
          "........",
          "........",
          "........");

        // 0x4E - UpperN
        G("........",
          "........",
          "##...##.",
          "###..##.",
          "####.##.",
          "#######.",
          "##.####.",
          "##..###.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "........",
          "........",
          "........",
          "........");

        // 0x4F - UpperO
        G("........",
          "........",
          ".#####..",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x50 - UpperP
        G("........",
          "........",
          "######..",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".#####..",
          ".##.....",
          ".##.....",
          ".##.....",
          ".##.....",
          "####....",
          "........",
          "........",
          "........",
          "........");

        // 0x51 - UpperQ
        G("........",
          "........",
          ".#####..",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##.#.##.",
          "##.####.",
          ".#####..",
          "....##..",
          "....###.",
          "........",
          "........");

        // 0x52 - UpperR
        G("........",
          "........",
          "######..",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".#####..",
          ".##.##..",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          "###..##.",
          "........",
          "........",
          "........",
          "........");

        // 0x53 - UpperS
        G("........",
          "........",
          ".#####..",
          "##...##.",
          "##...##.",
          ".##.....",
          "..###...",
          "....##..",
          ".....##.",
          "##...##.",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x54 - UpperT
        G("........",
          "........",
          "########",
          "##.##.##",
          "#..##..#",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0x55 - UpperU
        G("........",
          "........",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x56 - UpperV
        G("........",
          "........",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".##.##..",
          "..###...",
          "...#....",
          "........",
          "........",
          "........",
          "........");

        // 0x57 - UpperW
        G("........",
          "........",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##.#.##.",
          "##.#.##.",
          "##.#.##.",
          "#######.",
          "###.###.",
          ".##.##..",
          "........",
          "........",
          "........",
          "........");

        // 0x58 - UpperX
        G("........",
          "........",
          "##...##.",
          "##...##.",
          ".##.##..",
          ".#####..",
          "..###...",
          "..###...",
          ".#####..",
          ".##.##..",
          "##...##.",
          "##...##.",
          "........",
          "........",
          "........",
          "........");

        // 0x59 - UpperY
        G("........",
          "........",
          "##...##.",
          "##...##.",
          "##...##.",
          ".##.##..",
          "..###...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0x5A - UpperZ
        G("........",
          "........",
          "#######.",
          "##...##.",
          "#....##.",
          "....##..",
          "...##...",
          "..##....",
          ".##.....",
          "##....#.",
          "##...##.",
          "#######.",
          "........",
          "........",
          "........",
          "........");

        // 0x5B - LeftBracket
        G("........",
          "........",
          "..####..",
          "..##....",
          "..##....",
          "..##....",
          "..##....",
          "..##....",
          "..##....",
          "..##....",
          "..##....",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0x5C - Backslash
        G("........",
          "........",
          "........",
          "#.......",
          "##......",
          "###.....",
          ".###....",
          "..###...",
          "...###..",
          "....###.",
          ".....##.",
          "......#.",
          "........",
          "........",
          "........",
          "........");

        // 0x5D - RightBracket
        G("........",
          "........",
          "..####..",
          "....##..",
          "....##..",
          "....##..",
          "....##..",
          "....##..",
          "....##..",
          "....##..",
          "....##..",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0x5E - Caret
        G("...#....",
          "..###...",
          ".##.##..",
          "##...##.",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x5F - Underscore
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "########",
          "........",
          "........");

        // 0x60 - Backtick
        G("..##....",
          "..##....",
          "...##...",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x61 - LowerA
        G("........",
          "........",
          "........",
          "........",
          "........",
          ".####...",
          "....##..",
          ".#####..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".###.##.",
          "........",
          "........",
          "........",
          "........");

        // 0x62 - LowerB
        G("........",
          "........",
          "###.....",
          ".##.....",
          ".##.....",
          ".####...",
          ".##.##..",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x63 - LowerC
        G("........",
          "........",
          "........",
          "........",
          "........",
          ".#####..",
          "##...##.",
          "##......",
          "##......",
          "##......",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x64 - LowerD
        G("........",
          "........",
          "...###..",
          "....##..",
          "....##..",
          "..####..",
          ".##.##..",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".###.##.",
          "........",
          "........",
          "........",
          "........");

        // 0x65 - LowerE
        G("........",
          "........",
          "........",
          "........",
          "........",
          ".#####..",
          "##...##.",
          "#######.",
          "##......",
          "##......",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x66 - LowerF
        G("........",
          "........",
          "..###...",
          ".##.##..",
          ".##..#..",
          ".##.....",
          "####....",
          ".##.....",
          ".##.....",
          ".##.....",
          ".##.....",
          "####....",
          "........",
          "........",
          "........",
          "........");

        // 0x67 - LowerG
        G("........",
          "........",
          "........",
          "........",
          "........",
          ".###.##.",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".#####..",
          "....##..",
          "##..##..",
          ".####...",
          "........");

        // 0x68 - LowerH
        G("........",
          "........",
          "###.....",
          ".##.....",
          ".##.....",
          ".##.##..",
          ".###.##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          "###..##.",
          "........",
          "........",
          "........",
          "........");

        // 0x69 - LowerI
        G("........",
          "........",
          "...##...",
          "...##...",
          "........",
          "..###...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0x6A - LowerJ
        G("........",
          "........",
          ".....##.",
          ".....##.",
          "........",
          "....###.",
          ".....##.",
          ".....##.",
          ".....##.",
          ".....##.",
          ".....##.",
          ".....##.",
          ".##..##.",
          ".##..##.",
          "..####..",
          "........");

        // 0x6B - LowerK
        G("........",
          "........",
          "###.....",
          ".##.....",
          ".##.....",
          ".##..##.",
          ".##.##..",
          ".####...",
          ".####...",
          ".##.##..",
          ".##..##.",
          "###..##.",
          "........",
          "........",
          "........",
          "........");

        // 0x6C - LowerL
        G("........",
          "........",
          "..###...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0x6D - LowerM
        G("........",
          "........",
          "........",
          "........",
          "........",
          "###..##.",
          "########",
          "##.##.##",
          "##.##.##",
          "##.##.##",
          "##.##.##",
          "##.##.##",
          "........",
          "........",
          "........",
          "........");

        // 0x6E - LowerN
        G("........",
          "........",
          "........",
          "........",
          "........",
          "##.###..",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          "........",
          "........",
          "........",
          "........");

        // 0x6F - LowerO
        G("........",
          "........",
          "........",
          "........",
          "........",
          ".#####..",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x70 - LowerP
        G("........",
          "........",
          "........",
          "........",
          "........",
          "##.###..",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".#####..",
          ".##.....",
          ".##.....",
          "####....",
          "........");

        // 0x71 - LowerQ
        G("........",
          "........",
          "........",
          "........",
          "........",
          ".###.##.",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".#####..",
          "....##..",
          "....##..",
          "...####.",
          "........");

        // 0x72 - LowerR
        G("........",
          "........",
          "........",
          "........",
          "........",
          "##.###..",
          ".###.##.",
          ".##..##.",
          ".##.....",
          ".##.....",
          ".##.....",
          "####....",
          "........",
          "........",
          "........",
          "........");

        // 0x73 - LowerS
        G("........",
          "........",
          "........",
          "........",
          "........",
          ".#####..",
          "##...##.",
          ".##.....",
          "..###...",
          "....##..",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x74 - LowerT
        G("........",
          "........",
          "...#....",
          "..##....",
          "..##....",
          "######..",
          "..##....",
          "..##....",
          "..##....",
          "..##....",
          "..##.##.",
          "...###..",
          "........",
          "........",
          "........",
          "........");

        // 0x75 - LowerU
        G("........",
          "........",
          "........",
          "........",
          "........",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".###.##.",
          "........",
          "........",
          "........",
          "........");

        // 0x76 - LowerV
        G("........",
          "........",
          "........",
          "........",
          "........",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".##.##..",
          "..###...",
          "........",
          "........",
          "........",
          "........");

        // 0x77 - LowerW
        G("........",
          "........",
          "........",
          "........",
          "........",
          "##...##.",
          "##...##.",
          "##.#.##.",
          "##.#.##.",
          "##.#.##.",
          "#######.",
          ".##.##..",
          "........",
          "........",
          "........",
          "........");

        // 0x78 - LowerX
        G("........",
          "........",
          "........",
          "........",
          "........",
          "##...##.",
          ".##.##..",
          "..###...",
          "..###...",
          "..###...",
          ".##.##..",
          "##...##.",
          "........",
          "........",
          "........",
          "........");

        // 0x79 - LowerY
        G("........",
          "........",
          "........",
          "........",
          "........",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".######.",
          ".....##.",
          "....##..",
          "#####...",
          "........");

        // 0x7A - LowerZ
        G("........",
          "........",
          "........",
          "........",
          "........",
          "#######.",
          "##..##..",
          "...##...",
          "..##....",
          ".##.....",
          "##...##.",
          "#######.",
          "........",
          "........",
          "........",
          "........");

        // 0x7B - LeftBrace
        G("........",
          "........",
          "....###.",
          "...##...",
          "...##...",
          "...##...",
          ".###....",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "....###.",
          "........",
          "........",
          "........",
          "........");

        // 0x7C - Pipe
        G("........",
          "........",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "........",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "........");

        // 0x7D - RightBrace
        G("........",
          "........",
          ".###....",
          "...##...",
          "...##...",
          "...##...",
          "....###.",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          ".###....",
          "........",
          "........",
          "........",
          "........");

        // 0x7E - Tilde
        G("........",
          "........",
          ".###.##.",
          "##.###..",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x7F - House
        G("........",
          "........",
          "........",
          "........",
          "...#....",
          "..###...",
          ".##.##..",
          "##...##.",
          "##...##.",
          "##...##.",
          "#######.",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0x80 - CedillaC
        G("........",
          "........",
          "..####..",
          ".##..##.",
          "##....#.",
          "##......",
          "##......",
          "##......",
          "##....#.",
          ".##..##.",
          "..####..",
          "....##..",
          ".....##.",
          ".#####..",
          "........",
          "........");

        // 0x81 - UmlautLowerU
        G("........",
          "........",
          "##..##..",
          "........",
          "........",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".###.##.",
          "........",
          "........",
          "........",
          "........");

        // 0x82 - AcuteLowerE
        G("........",
          "....##..",
          "...##...",
          "..##....",
          "........",
          ".#####..",
          "##...##.",
          "#######.",
          "##......",
          "##......",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x83 - CircumLowerA
        G("........",
          "...#....",
          "..###...",
          ".##.##..",
          "........",
          ".####...",
          "....##..",
          ".#####..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".###.##.",
          "........",
          "........",
          "........",
          "........");

        // 0x84 - UmlautLowerA
        G("........",
          "........",
          "##..##..",
          "........",
          "........",
          ".####...",
          "....##..",
          ".#####..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".###.##.",
          "........",
          "........",
          "........",
          "........");

        // 0x85 - GraveLowerA
        G("........",
          ".##.....",
          "..##....",
          "...##...",
          "........",
          ".####...",
          "....##..",
          ".#####..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".###.##.",
          "........",
          "........",
          "........",
          "........");

        // 0x86 - RingLowerA
        G("........",
          "..###...",
          ".##.##..",
          "..###...",
          "........",
          ".####...",
          "....##..",
          ".#####..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".###.##.",
          "........",
          "........",
          "........",
          "........");

        // 0x87 - CedillaLowerC
        G("........",
          "........",
          "........",
          "........",
          "........",
          ".#####..",
          "##...##.",
          "##......",
          "##......",
          "##......",
          "##...##.",
          ".#####..",
          "...##...",
          "....##..",
          "..###...",
          "........");

        // 0x88 - CircumLowerE
        G("........",
          "...#....",
          "..###...",
          ".##.##..",
          "........",
          ".#####..",
          "##...##.",
          "#######.",
          "##......",
          "##......",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x89 - UmlautLowerE
        G("........",
          "........",
          "##...##.",
          "........",
          "........",
          ".#####..",
          "##...##.",
          "#######.",
          "##......",
          "##......",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x8A - GraveLowerE
        G("........",
          ".##.....",
          "..##....",
          "...##...",
          "........",
          ".#####..",
          "##...##.",
          "#######.",
          "##......",
          "##......",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x8B - UmlautLowerI
        G("........",
          "........",
          ".##..##.",
          "........",
          "........",
          "..###...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0x8C - CircumLowerI
        G("........",
          "...##...",
          "..####..",
          ".##..##.",
          "........",
          "..###...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0x8D - GraveLowerI
        G("........",
          ".##.....",
          "..##....",
          "...##...",
          "........",
          "..###...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0x8E - UmlautUpperA
        G("........",
          "##...##.",
          "........",
          "...#....",
          "..###...",
          ".##.##..",
          "##...##.",
          "##...##.",
          "#######.",
          "##...##.",
          "##...##.",
          "##...##.",
          "........",
          "........",
          "........",
          "........");

        // 0x8F - RingUpperA
        G("..###...",
          ".##.##..",
          "..###...",
          "........",
          "..###...",
          ".##.##..",
          "##...##.",
          "##...##.",
          "#######.",
          "##...##.",
          "##...##.",
          "##...##.",
          "........",
          "........",
          "........",
          "........");

        // 0x90 - AcuteUpperE
        G("....##..",
          "...##...",
          "........",
          "#######.",
          ".##..##.",
          ".##...#.",
          ".##.#...",
          ".####...",
          ".##.#...",
          ".##...#.",
          ".##..##.",
          "#######.",
          "........",
          "........",
          "........",
          "........");

        // 0x91 - LowerAE
        G("........",
          "........",
          "........",
          "........",
          "........",
          ".##.###.",
          "..###.##",
          "...##.##",
          ".######.",
          "##.##...",
          "##.###..",
          ".###.###",
          "........",
          "........",
          "........",
          "........");

        // 0x92 - UpperAE
        G("........",
          "........",
          "..#####.",
          ".##.##..",
          "##..##..",
          "##..##..",
          "#######.",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..###.",
          "........",
          "........",
          "........",
          "........");

        // 0x93 - CircumLowerO
        G("........",
          "...#....",
          "..###...",
          ".##.##..",
          "........",
          ".#####..",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x94 - UmlautLowerO
        G("........",
          "........",
          "##...##.",
          "........",
          "........",
          ".#####..",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x95 - GraveLowerO
        G("........",
          ".##.....",
          "..##....",
          "...##...",
          "........",
          ".#####..",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x96 - CircumLowerU
        G("........",
          "..##....",
          ".####...",
          "##..##..",
          "........",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".###.##.",
          "........",
          "........",
          "........",
          "........");

        // 0x97 - GraveLowerU
        G("........",
          ".##.....",
          "..##....",
          "...##...",
          "........",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".###.##.",
          "........",
          "........",
          "........",
          "........");

        // 0x98 - UmlautLowerY
        G("........",
          "........",
          "##...##.",
          "........",
          "........",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".######.",
          ".....##.",
          "....##..",
          ".####...",
          "........");

        // 0x99 - UmlautUpperO
        G("........",
          "##...##.",
          "........",
          ".#####..",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x9A - UmlautUpperU
        G("........",
          "##...##.",
          "........",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0x9B - Cent
        G("........",
          "...##...",
          "...##...",
          ".#####..",
          "##...##.",
          "##......",
          "##......",
          "##......",
          "##...##.",
          ".#####..",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "........");

        // 0x9C - Pound
        G("........",
          "..###...",
          ".##.##..",
          ".##..#..",
          ".##.....",
          "####....",
          ".##.....",
          ".##.....",
          ".##.....",
          ".##.....",
          "###..##.",
          "######..",
          "........",
          "........",
          "........",
          "........");

        // 0x9D - Yen
        G("........",
          "........",
          "##...##.",
          "##...##.",
          ".##.##..",
          ".#####..",
          "..###...",
          ".######.",
          "...##...",
          ".######.",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "........");

        // 0x9E - Peseta
        G("........",
          "######..",
          ".##..##.",
          ".##..##.",
          ".#####..",
          ".##...#.",
          ".##..##.",
          ".##.####",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          "####..##",
          "........",
          "........",
          "........",
          "........");

        // 0x9F - FlorinSign
        G("........",
          "....###.",
          "...##.##",
          "...##...",
          "...##...",
          "...##...",
          ".######.",
          "...##...",
          "...##...",
          "...##...",
          "##.##...",
          ".###....",
          "........",
          "........",
          "........",
          "........");

        // 0xA0 - AcuteLowerA
        G("........",
          "...##...",
          "..##....",
          ".##.....",
          "........",
          ".####...",
          "....##..",
          ".#####..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".###.##.",
          "........",
          "........",
          "........",
          "........");

        // 0xA1 - AcuteLowerI
        G("........",
          "....##..",
          "...##...",
          "..##....",
          "........",
          "..###...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0xA2 - AcuteLowerO
        G("........",
          "...##...",
          "..##....",
          ".##.....",
          "........",
          ".#####..",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0xA3 - AcuteLowerU
        G("........",
          "...##...",
          "..##....",
          ".##.....",
          "........",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          "##..##..",
          ".###.##.",
          "........",
          "........",
          "........",
          "........");

        // 0xA4 - TildeLowerN
        G("........",
          "........",
          ".###.##.",
          "##.###..",
          "........",
          "##.###..",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          "........",
          "........",
          "........",
          "........");

        // 0xA5 - TildeUpperN
        G(".###.##.",
          "##.###..",
          "........",
          "##...##.",
          "###..##.",
          "####.##.",
          "#######.",
          "##.####.",
          "##..###.",
          "##...##.",
          "##...##.",
          "##...##.",
          "........",
          "........",
          "........",
          "........");

        // 0xA6 - FemOrdinal
        G("........",
          "........",
          "..####..",
          ".##.##..",
          ".##.##..",
          "..#####.",
          "........",
          ".######.",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xA7 - MascOrdinal
        G("........",
          "........",
          "..###...",
          ".##.##..",
          ".##.##..",
          "..###...",
          "........",
          ".#####..",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xA8 - InvQuestion
        G("........",
          "........",
          "..##....",
          "..##....",
          "........",
          "..##....",
          "..##....",
          ".##.....",
          "##......",
          "##...##.",
          "##...##.",
          ".#####..",
          "........",
          "........",
          "........",
          "........");

        // 0xA9 - ReversedNot
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "#######.",
          "##......",
          "##......",
          "##......",
          "##......",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xAA - Not
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "#######.",
          ".....##.",
          ".....##.",
          ".....##.",
          ".....##.",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xAB - Half
        G("........",
          "##......",
          "##......",
          "##....#.",
          "##...##.",
          "##..##..",
          "...##...",
          "..##....",
          ".##.....",
          "##..###.",
          "#..##.##",
          ".....##.",
          "....##..",
          "...#####",
          "........",
          "........");

        // 0xAC - Quarter
        G("........",
          "##......",
          "##......",
          "##....#.",
          "##...##.",
          "##..##..",
          "...##...",
          "..##....",
          ".##..##.",
          "##..###.",
          "#..#.##.",
          "..#####.",
          ".....##.",
          ".....##.",
          "........",
          "........");

        // 0xAD - InvExclamation
        G("........",
          "........",
          "...##...",
          "...##...",
          "........",
          "...##...",
          "...##...",
          "...##...",
          "..####..",
          "..####..",
          "..####..",
          "...##...",
          "........",
          "........",
          "........",
          "........");

        // 0xAE - LeftAngleQuote
        G("........",
          "........",
          "........",
          "........",
          "........",
          "..##.##.",
          ".##.##..",
          "##.##...",
          ".##.##..",
          "..##.##.",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xAF - RightAngleQuote
        G("........",
          "........",
          "........",
          "........",
          "........",
          "##.##...",
          ".##.##..",
          "..##.##.",
          ".##.##..",
          "##.##...",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xB0 - LightShade
        G("...#...#",
          ".#...#..",
          "...#...#",
          ".#...#..",
          "...#...#",
          ".#...#..",
          "...#...#",
          ".#...#..",
          "...#...#",
          ".#...#..",
          "...#...#",
          ".#...#..",
          "...#...#",
          ".#...#..",
          "...#...#",
          ".#...#..");

        // 0xB1 - MediumShade
        G(".#.#.#.#",
          "#.#.#.#.",
          ".#.#.#.#",
          "#.#.#.#.",
          ".#.#.#.#",
          "#.#.#.#.",
          ".#.#.#.#",
          "#.#.#.#.",
          ".#.#.#.#",
          "#.#.#.#.",
          ".#.#.#.#",
          "#.#.#.#.",
          ".#.#.#.#",
          "#.#.#.#.",
          ".#.#.#.#",
          "#.#.#.#.");

        // 0xB2 - DarkShade
        G("##.###.#",
          ".###.###",
          "##.###.#",
          ".###.###",
          "##.###.#",
          ".###.###",
          "##.###.#",
          ".###.###",
          "##.###.#",
          ".###.###",
          "##.###.#",
          ".###.###",
          "##.###.#",
          ".###.###",
          "##.###.#",
          ".###.###");

        // 0xB3 - BoxVert
        G("...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...");

        // 0xB4 - BoxVertLeft
        G("...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "#####...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...");

        // 0xB5 - BoxVertLeftDbl
        G("...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "#####...",
          "...##...",
          "#####...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...");

        // 0xB6 - BoxDblVertLeft
        G("..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "####.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.");

        // 0xB7 - BoxDblDownLeft
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "#######.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.");

        // 0xB8 - BoxDownLeftDbl
        G("........",
          "........",
          "........",
          "........",
          "........",
          "#####...",
          "...##...",
          "#####...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...");

        // 0xB9 - BoxDblVertLeftDbl
        G("..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "####.##.",
          ".....##.",
          "####.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.");

        // 0xBA - BoxDblVert
        G("..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.");

        // 0xBB - BoxDblDownLeftDbl
        G("........",
          "........",
          "........",
          "........",
          "........",
          "#######.",
          ".....##.",
          "####.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.");

        // 0xBC - BoxDblUpLeft
        G("..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "####.##.",
          ".....##.",
          "#######.",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xBD - BoxDblUpLeftDbl
        G("..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "#######.",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xBE - BoxUpLeftDbl
        G("...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "#####...",
          "...##...",
          "#####...",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xBF - BoxDownLeft
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "#####...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...");

        // 0xC0 - BoxUpRight
        G("...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...#####",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xC1 - BoxUpHoriz
        G("...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "########",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xC2 - BoxDownHoriz
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "########",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...");

        // 0xC3 - BoxVertRight
        G("...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...#####",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...");

        // 0xC4 - BoxHoriz
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "########",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xC5 - BoxCross
        G("...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "########",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...");

        // 0xC6 - BoxVertRightDbl
        G("...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...#####",
          "...##...",
          "...#####",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...");

        // 0xC7 - BoxDblVertRight
        G("..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.###",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.");

        // 0xC8 - BoxDblUpRight
        G("..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.###",
          "..##....",
          "..######",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xC9 - BoxDblDownRight
        G("........",
          "........",
          "........",
          "........",
          "........",
          "..######",
          "..##....",
          "..##.###",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.");

        // 0xCA - BoxDblUpHoriz
        G("..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "####.###",
          "........",
          "########",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xCB - BoxDblDownHoriz
        G("........",
          "........",
          "........",
          "........",
          "........",
          "########",
          "........",
          "####.###",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.");

        // 0xCC - BoxDblVertRightDbl
        G("..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.###",
          "..##....",
          "..##.###",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.");

        // 0xCD - BoxDblHoriz
        G("........",
          "........",
          "........",
          "........",
          "........",
          "########",
          "........",
          "########",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xCE - BoxDblCross
        G("..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "####.###",
          "........",
          "####.###",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.");

        // 0xCF - BoxUpHorizDbl
        G("...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "########",
          "........",
          "########",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xD0 - BoxDblUpHorizDbl
        G("..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "########",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xD1 - BoxDownHorizDbl
        G("........",
          "........",
          "........",
          "........",
          "........",
          "########",
          "........",
          "########",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...");

        // 0xD2 - BoxDblDownHorizDbl
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "########",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.");

        // 0xD3 - BoxDblUpRightDbl
        G("..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..######",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xD4 - BoxUpRightDbl
        G("...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...#####",
          "...##...",
          "...#####",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xD5 - BoxDownRightDbl
        G("........",
          "........",
          "........",
          "........",
          "........",
          "...#####",
          "...##...",
          "...#####",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...");

        // 0xD6 - BoxDblDownRightDbl
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "..######",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.");

        // 0xD7 - BoxDblVertHoriz
        G("..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "########",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.",
          "..##.##.");

        // 0xD8 - BoxVertHorizDbl
        G("...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "########",
          "...##...",
          "########",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...");

        // 0xD9 - BoxUpLeft
        G("...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "#####...",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xDA - BoxDownRight
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "...#####",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...");

        // 0xDB - FullBlock
        G("########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########");

        // 0xDC - LowerHalfBlock
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########");

        // 0xDD - LeftHalfBlock
        G("####....",
          "####....",
          "####....",
          "####....",
          "####....",
          "####....",
          "####....",
          "####....",
          "####....",
          "####....",
          "####....",
          "####....",
          "####....",
          "####....",
          "####....",
          "####....");

        // 0xDE - RightHalfBlock
        G("....####",
          "....####",
          "....####",
          "....####",
          "....####",
          "....####",
          "....####",
          "....####",
          "....####",
          "....####",
          "....####",
          "....####",
          "....####",
          "....####",
          "....####",
          "....####");

        // 0xDF - UpperHalfBlock
        G("########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "########",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xE0 - Alpha
        G("........",
          "........",
          "........",
          "........",
          "........",
          ".###.##.",
          "##.###..",
          "##.##...",
          "##.##...",
          "##.##...",
          "##.###..",
          ".###.##.",
          "........",
          "........",
          "........",
          "........");

        // 0xE1 - Beta
        G("........",
          "........",
          ".####...",
          "##..##..",
          "##..##..",
          "##..##..",
          "##.##...",
          "##..##..",
          "##...##.",
          "##...##.",
          "##...##.",
          "##..##..",
          "........",
          "........",
          "........",
          "........");

        // 0xE2 - Gamma
        G("........",
          "........",
          "#######.",
          "##...##.",
          "##...##.",
          "##......",
          "##......",
          "##......",
          "##......",
          "##......",
          "##......",
          "##......",
          "........",
          "........",
          "........",
          "........");

        // 0xE3 - Pi
        G("........",
          "........",
          "........",
          "........",
          "#######.",
          ".##.##..",
          ".##.##..",
          ".##.##..",
          ".##.##..",
          ".##.##..",
          ".##.##..",
          ".##.##..",
          "........",
          "........",
          "........",
          "........");

        // 0xE4 - UpperSigma
        G("........",
          "........",
          "........",
          "#######.",
          "##...##.",
          ".##.....",
          "..##....",
          "...##...",
          "..##....",
          ".##.....",
          "##...##.",
          "#######.",
          "........",
          "........",
          "........",
          "........");

        // 0xE5 - LowerSigma
        G("........",
          "........",
          "........",
          "........",
          "........",
          ".######.",
          "##.##...",
          "##.##...",
          "##.##...",
          "##.##...",
          "##.##...",
          ".###....",
          "........",
          "........",
          "........",
          "........");

        // 0xE6 - Mu
        G("........",
          "........",
          "........",
          "........",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".#####..",
          ".##.....",
          ".##.....",
          "##......",
          "........",
          "........",
          "........");

        // 0xE7 - Tau
        G("........",
          "........",
          "........",
          "........",
          ".###.##.",
          "##.###..",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "........");

        // 0xE8 - UpperPhi
        G("........",
          "........",
          "........",
          ".######.",
          "...##...",
          "..####..",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          "..####..",
          "...##...",
          ".######.",
          "........",
          "........",
          "........",
          "........");

        // 0xE9 - Theta
        G("........",
          "........",
          "........",
          "..###...",
          ".##.##..",
          "##...##.",
          "##...##.",
          "#######.",
          "##...##.",
          "##...##.",
          ".##.##..",
          "..###...",
          "........",
          "........",
          "........",
          "........");

        // 0xEA - Omega
        G("........",
          "........",
          "..###...",
          ".##.##..",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          ".##.##..",
          ".##.##..",
          "###.###.",
          "........",
          "........",
          "........",
          "........");

        // 0xEB - Delta
        G("........",
          "........",
          "...####.",
          "..##....",
          "...##...",
          "....##..",
          "..#####.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          ".##..##.",
          "..####..",
          "........",
          "........",
          "........",
          "........");

        // 0xEC - Infinity
        G("........",
          "........",
          "........",
          "........",
          "........",
          ".######.",
          "##.##.##",
          "##.##.##",
          "##.##.##",
          ".######.",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xED - LowerPhi
        G("........",
          "........",
          "........",
          "......##",
          ".....##.",
          ".######.",
          "##.##.##",
          "##.##.##",
          "####..##",
          ".######.",
          ".##.....",
          "##......",
          "........",
          "........",
          "........",
          "........");

        // 0xEE - Epsilon
        G("........",
          "........",
          "...###..",
          "..##....",
          ".##.....",
          ".##.....",
          ".#####..",
          ".##.....",
          ".##.....",
          ".##.....",
          "..##....",
          "...###..",
          "........",
          "........",
          "........",
          "........");

        // 0xEF - Intersection
        G("........",
          "........",
          "........",
          ".#####..",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "##...##.",
          "........",
          "........",
          "........",
          "........");

        // 0xF0 - Identical
        G("........",
          "........",
          "........",
          "........",
          "#######.",
          "........",
          "........",
          "#######.",
          "........",
          "........",
          "#######.",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xF1 - PlusMinus
        G("........",
          "........",
          "........",
          "........",
          "...##...",
          "...##...",
          ".######.",
          "...##...",
          "...##...",
          "........",
          "........",
          "########",
          "........",
          "........",
          "........",
          "........");

        // 0xF2 - GreaterEqual
        G("........",
          "........",
          "........",
          "..##....",
          "...##...",
          "....##..",
          ".....##.",
          "....##..",
          "...##...",
          "..##....",
          "........",
          ".######.",
          "........",
          "........",
          "........",
          "........");

        // 0xF3 - LessEqual
        G("........",
          "........",
          "........",
          "....##..",
          "...##...",
          "..##....",
          ".##.....",
          "..##....",
          "...##...",
          "....##..",
          "........",
          ".######.",
          "........",
          "........",
          "........",
          "........");

        // 0xF4 - UpperIntegral
        G("........",
          "........",
          "....###.",
          "...##.##",
          "...##.##",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...");

        // 0xF5 - LowerIntegral
        G("...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "...##...",
          "##.##...",
          "##.##...",
          "##.##...",
          ".###....",
          "........",
          "........",
          "........",
          "........");

        // 0xF6 - Division
        G("........",
          "........",
          "........",
          "........",
          "...##...",
          "...##...",
          "........",
          ".######.",
          "........",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xF7 - ApproxEqual
        G("........",
          "........",
          "........",
          "........",
          "........",
          ".###.##.",
          "##.###..",
          "........",
          ".###.##.",
          "##.###..",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xF8 - Degree
        G("........",
          "..###...",
          ".##.##..",
          ".##.##..",
          "..###...",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xF9 - BulletOp
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "...##...",
          "...##...",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xFA - MiddleDot
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "...##...",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xFB - SquareRoot
        G("........",
          "....####",
          "....##..",
          "....##..",
          "....##..",
          "....##..",
          "....##..",
          "###.##..",
          ".##.##..",
          ".##.##..",
          "..####..",
          "...###..",
          "........",
          "........",
          "........",
          "........");

        // 0xFC - SuperscriptN
        G("........",
          "##.##...",
          ".##.##..",
          ".##.##..",
          ".##.##..",
          ".##.##..",
          ".##.##..",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xFD - Superscript2
        G("........",
          ".###....",
          "##.##...",
          "..##....",
          ".##.....",
          "##..#...",
          "#####...",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xFE - FilledSquare
        G("........",
          "........",
          "........",
          "........",
          ".#####..",
          ".#####..",
          ".#####..",
          ".#####..",
          ".#####..",
          ".#####..",
          ".#####..",
          "........",
          "........",
          "........",
          "........",
          "........");

        // 0xFF - NBSP
        G("........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........",
          "........");

        return data;
    }

    /// <summary>
    /// Returns the glyph bitmap for a Unicode character (looked up via CP437 mapping).
    /// Returns null if the character is not in the CP437 set.
    /// </summary>
    public static bool[]? GetChar(char c)
    {
        for (int i = 1; i < 256; i++)
        {
            if (Cp437ToUnicode[i] == c)
                return GetGlyphByIndex(i);
        }

        return null;
    }

    /// <summary>Gets the glyph bitmap for a CP437 index (0-255) as a bool array.</summary>
    private static bool[] GetGlyphByIndex(int index)
    {
        var glyph = new bool[GlyphWidth * GlyphHeight];
        int off = index * GlyphHeight;
        for (int row = 0; row < GlyphHeight; row++)
        {
            byte b = FontData[off + row];
            for (int col = 0; col < GlyphWidth; col++)
            {
                glyph[row * GlyphWidth + col] = (b & (0x80 >> col)) != 0;
            }
        }
        return glyph;
    }

    /// <summary>Returns all glyphs as a dictionary of Unicode char to bool[] bitmap.</summary>
    public static Dictionary<char, bool[]> GetAllGlyphs()
    {
        var dict = new Dictionary<char, bool[]>();
        for (int i = 1; i < 256; i++)
        {
            char c = Cp437ToUnicode[i];
            if (c != '\0' && !dict.ContainsKey(c))
                dict[c] = GetGlyphByIndex(i);
        }
        return dict;
    }
}
