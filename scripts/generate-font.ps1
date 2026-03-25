#!/usr/bin/env pwsh
# Generates MiniBitmapFont.cs with ASCII art glyph data instead of hex codes.
# Reads existing hex data from the current file, converts to '#'/'.', and writes new format.

$ErrorActionPreference = 'Stop'

$inputPath  = Join-Path $PSScriptRoot '..\src\Engine\Rendering\Base\MiniBitmapFont.cs'
$outputPath = $inputPath

# ── 1. Parse existing hex byte data ──────────────────────────────────

$content = Get-Content $inputPath -Raw
$match = [regex]::Match($content, '(?s)FontData\s*=\s*\[\s*(.+?)\];')
if (-not $match.Success) { throw 'Could not locate FontData array in source file.' }

# Strip comment portions before extracting hex values
$hexSection = $match.Groups[1].Value
$hexSection = [regex]::Replace($hexSection, '//[^\r\n]*', '')
$hexMatches = [regex]::Matches($hexSection, '0x([0-9A-Fa-f]{2})')
$bytes = [int[]]::new($hexMatches.Count)
for ($i = 0; $i -lt $hexMatches.Count; $i++) {
    $bytes[$i] = [Convert]::ToInt32($hexMatches[$i].Groups[1].Value, 16)
}
if ($bytes.Count -ne 4096) { throw "Expected 4096 bytes, got $($bytes.Count)" }

# ── 2. Glyph names (256 entries, matching CP437 standard) ────────────

$names = @(
    'Null',            'SmileyFace',        'SmileyFaceInv',   'Heart',
    'Diamond',         'Club',              'Spade',           'Bullet',
    'BulletInv',       'Circle',            'CircleInv',       'Male',
    'Female',          'EighthNote',        'BeamedNotes',     'Sun',
    'RightPointer',    'LeftPointer',       'UpDownArrow',     'DoubleExclaim',
    'Pilcrow',         'Section',           'ThickUnderline',  'UpDownArrowUL',
    'UpArrow',         'DownArrow',         'RightArrow',      'LeftArrow',
    'RightAngle',      'LeftRightArrow',    'UpTriangle',      'DownTriangle',
    'Space',           'Exclamation',       'DoubleQuote',     'Hash',
    'Dollar',          'Percent',           'Ampersand',       'Apostrophe',
    'LeftParen',       'RightParen',        'Asterisk',        'Plus',
    'Comma',           'Hyphen',            'Period',          'Slash',
    'Digit0',          'Digit1',            'Digit2',          'Digit3',
    'Digit4',          'Digit5',            'Digit6',          'Digit7',
    'Digit8',          'Digit9',            'Colon',           'Semicolon',
    'LessThan',        'EqualsSign',        'GreaterThan',     'Question',
    'At',              'UpperA',            'UpperB',          'UpperC',
    'UpperD',          'UpperE',            'UpperF',          'UpperG',
    'UpperH',          'UpperI',            'UpperJ',          'UpperK',
    'UpperL',          'UpperM',            'UpperN',          'UpperO',
    'UpperP',          'UpperQ',            'UpperR',          'UpperS',
    'UpperT',          'UpperU',            'UpperV',          'UpperW',
    'UpperX',          'UpperY',            'UpperZ',          'LeftBracket',
    'Backslash',       'RightBracket',      'Caret',           'Underscore',
    'Backtick',        'LowerA',            'LowerB',          'LowerC',
    'LowerD',          'LowerE',            'LowerF',          'LowerG',
    'LowerH',          'LowerI',            'LowerJ',          'LowerK',
    'LowerL',          'LowerM',            'LowerN',          'LowerO',
    'LowerP',          'LowerQ',            'LowerR',          'LowerS',
    'LowerT',          'LowerU',            'LowerV',          'LowerW',
    'LowerX',          'LowerY',            'LowerZ',          'LeftBrace',
    'Pipe',            'RightBrace',        'Tilde',           'House',
    'CedillaC',        'UmlautLowerU',      'AcuteLowerE',     'CircumLowerA',
    'UmlautLowerA',    'GraveLowerA',       'RingLowerA',      'CedillaLowerC',
    'CircumLowerE',    'UmlautLowerE',      'GraveLowerE',     'UmlautLowerI',
    'CircumLowerI',    'GraveLowerI',       'UmlautUpperA',    'RingUpperA',
    'AcuteUpperE',     'LowerAE',           'UpperAE',         'CircumLowerO',
    'UmlautLowerO',    'GraveLowerO',       'CircumLowerU',    'GraveLowerU',
    'UmlautLowerY',    'UmlautUpperO',      'UmlautUpperU',    'Cent',
    'Pound',           'Yen',               'Peseta',          'FlorinSign',
    'AcuteLowerA',     'AcuteLowerI',       'AcuteLowerO',     'AcuteLowerU',
    'TildeLowerN',     'TildeUpperN',       'FemOrdinal',      'MascOrdinal',
    'InvQuestion',     'ReversedNot',       'Not',             'Half',
    'Quarter',         'InvExclamation',    'LeftAngleQuote',  'RightAngleQuote',
    'LightShade',      'MediumShade',       'DarkShade',       'BoxVert',
    'BoxVertLeft',     'BoxVertLeftDbl',    'BoxDblVertLeft',  'BoxDblDownLeft',
    'BoxDownLeftDbl',  'BoxDblVertLeftDbl', 'BoxDblVert',      'BoxDblDownLeftDbl',
    'BoxDblUpLeft',    'BoxDblUpLeftDbl',   'BoxUpLeftDbl',    'BoxDownLeft',
    'BoxUpRight',      'BoxUpHoriz',        'BoxDownHoriz',    'BoxVertRight',
    'BoxHoriz',        'BoxCross',          'BoxVertRightDbl', 'BoxDblVertRight',
    'BoxDblUpRight',   'BoxDblDownRight',   'BoxDblUpHoriz',   'BoxDblDownHoriz',
    'BoxDblVertRightDbl', 'BoxDblHoriz',    'BoxDblCross',     'BoxUpHorizDbl',
    'BoxDblUpHorizDbl','BoxDownHorizDbl',   'BoxDblDownHorizDbl', 'BoxDblUpRightDbl',
    'BoxUpRightDbl',   'BoxDownRightDbl',   'BoxDblDownRightDbl', 'BoxDblVertHoriz',
    'BoxVertHorizDbl', 'BoxUpLeft',         'BoxDownRight',    'FullBlock',
    'LowerHalfBlock',  'LeftHalfBlock',     'RightHalfBlock',  'UpperHalfBlock',
    'Alpha',           'Beta',              'Gamma',           'Pi',
    'UpperSigma',      'LowerSigma',        'Mu',              'Tau',
    'UpperPhi',        'Theta',             'Omega',           'Delta',
    'Infinity',        'LowerPhi',          'Epsilon',         'Intersection',
    'Identical',       'PlusMinus',         'GreaterEqual',    'LessEqual',
    'UpperIntegral',   'LowerIntegral',     'Division',        'ApproxEqual',
    'Degree',          'BulletOp',          'MiddleDot',       'SquareRoot',
    'SuperscriptN',    'Superscript2',      'FilledSquare',    'NBSP'
)

if ($names.Count -ne 256) { throw "Expected 256 names, got $($names.Count)" }

# ── 3. Helper: byte → 8-char art string ────────────────────────────

function ByteToArt([int]$b) {
    $chars = [char[]]::new(8)
    for ($bit = 0; $bit -lt 8; $bit++) {
        $chars[$bit] = if (($b -band (1 -shl (7 - $bit))) -ne 0) { '#' } else { '.' }
    }
    return [string]::new($chars)
}

# ── 4. Build C# source ──────────────────────────────────────────────

$sb = [System.Text.StringBuilder]::new(300000)

# ── Header ──

[void]$sb.AppendLine('namespace Engine.Rendering.Base;')
[void]$sb.AppendLine()
[void]$sb.AppendLine('/// <summary>')
[void]$sb.AppendLine('/// 8x16 bitmap font covering all 256 CP437 codepoints (IBM VGA ROM font).')
[void]$sb.AppendLine('/// Glyph data is defined as ASCII art for readability and easy debugging.')
[void]$sb.AppendLine('/// </summary>')
[void]$sb.AppendLine('public static class MiniBitmapFont')
[void]$sb.AppendLine('{')
[void]$sb.AppendLine('    public const int GlyphWidth = 8;')
[void]$sb.AppendLine('    public const int GlyphHeight = 16;')
[void]$sb.AppendLine()

# ── Glyph constants ──

[void]$sb.AppendLine('    // ── CP437 Glyph Index Constants ────────────────────────────────────')
[void]$sb.AppendLine()

for ($i = 0; $i -lt 256; $i++) {
    $hex = '0x' + $i.ToString('X2')
    [void]$sb.AppendLine("    public const int $($names[$i]) = $hex;")
}

[void]$sb.AppendLine()

# ── Cp437ToUnicode + CreateCp437Map ──

[void]$sb.AppendLine('    /// <summary>Maps CP437 byte index (0-255) to its Unicode character.</summary>')
[void]$sb.AppendLine('    public static readonly char[] Cp437ToUnicode = CreateCp437Map();')
[void]$sb.AppendLine()
[void]$sb.AppendLine('    private static char[] CreateCp437Map()')
[void]$sb.AppendLine('    {')
[void]$sb.AppendLine('        var map = new char[256];')
[void]$sb.AppendLine()
[void]$sb.AppendLine('        // Control / graphic characters (0x00-0x1F)')

# Unicode codepoints for 0x00-0x1F
$controlUnicode = @(
    $null,   '263A', '263B', '2665', '2666', '2663', '2660', '2022',
    '25D8',  '25CB', '25D9', '2642', '2640', '266A', '266B', '263C',
    '25BA',  '25C4', '2195', '203C', '00B6', '00A7', '25AC', '21A8',
    '2191',  '2193', '2192', '2190', '221F', '2194', '25B2', '25BC'
)

for ($i = 0; $i -lt 32; $i++) {
    if ($null -eq $controlUnicode[$i]) {
        [void]$sb.AppendLine("        map[$($names[$i])] = '\0';")
    } else {
        [void]$sb.AppendLine("        map[$($names[$i])] = '\u$($controlUnicode[$i])';")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('        // Standard ASCII (0x20-0x7E) — identity mapping')
[void]$sb.AppendLine('        for (int i = 0x20; i <= 0x7E; i++)')
[void]$sb.AppendLine('            map[i] = (char)i;')
[void]$sb.AppendLine()
[void]$sb.AppendLine("        // House (0x7F)")
[void]$sb.AppendLine("        map[House] = '\u2302';")
[void]$sb.AppendLine()
[void]$sb.AppendLine('        // Extended characters (0x80-0xFF)')

# Unicode codepoints for 0x80-0xFF
$extUnicode = @(
    '00C7', '00FC', '00E9', '00E2', '00E4', '00E0', '00E5', '00E7',
    '00EA', '00EB', '00E8', '00EF', '00EE', '00EC', '00C4', '00C5',
    '00C9', '00E6', '00C6', '00F4', '00F6', '00F2', '00FB', '00F9',
    '00FF', '00D6', '00DC', '00A2', '00A3', '00A5', '20A7', '0192',
    '00E1', '00ED', '00F3', '00FA', '00F1', '00D1', '00AA', '00BA',
    '00BF', '2310', '00AC', '00BD', '00BC', '00A1', '00AB', '00BB',
    '2591', '2592', '2593', '2502', '2524', '2561', '2562', '2556',
    '2555', '2563', '2551', '2557', '255D', '255C', '255B', '2510',
    '2514', '2534', '252C', '251C', '2500', '253C', '255E', '255F',
    '255A', '2554', '2569', '2566', '2560', '2550', '256C', '2567',
    '2568', '2564', '2565', '2559', '2558', '2552', '2553', '256B',
    '256A', '2518', '250C', '2588', '2584', '258C', '2590', '2580',
    '03B1', '00DF', '0393', '03C0', '03A3', '03C3', '00B5', '03C4',
    '03A6', '0398', '03A9', '03B4', '221E', '03C6', '03B5', '2229',
    '2261', '00B1', '2265', '2264', '2320', '2321', '00F7', '2248',
    '00B0', '2219', '00B7', '221A', '207F', '00B2', '25A0', '00A0'
)

for ($i = 0; $i -lt 128; $i++) {
    [void]$sb.AppendLine("        map[$($names[$i + 128])] = '\u$($extUnicode[$i])';")
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('        return map;')
[void]$sb.AppendLine('    }')
[void]$sb.AppendLine()

# ── Font data header ──

[void]$sb.AppendLine('    // ── Font Bitmap Data (ASCII Art) ───────────────────────────────────')
[void]$sb.AppendLine('    // Each glyph: 16 rows of 8 pixels. ''#'' = pixel on, ''.'' = pixel off.')
[void]$sb.AppendLine('    // Bit 7 (MSB) = leftmost pixel.')
[void]$sb.AppendLine()
[void]$sb.AppendLine('    private static readonly byte[] FontData = BuildFontData();')
[void]$sb.AppendLine()
[void]$sb.AppendLine('    private static byte[] BuildFontData()')
[void]$sb.AppendLine('    {')
[void]$sb.AppendLine('        var data = new byte[256 * GlyphHeight];')
[void]$sb.AppendLine('        int offset = 0;')
[void]$sb.AppendLine()
[void]$sb.AppendLine('        void G(')
[void]$sb.AppendLine('            string r0,  string r1,  string r2,  string r3,')
[void]$sb.AppendLine('            string r4,  string r5,  string r6,  string r7,')
[void]$sb.AppendLine('            string r8,  string r9,  string rA,  string rB,')
[void]$sb.AppendLine('            string rC,  string rD,  string rE,  string rF)')
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine('            ReadOnlySpan<string> rows = [r0, r1, r2, r3, r4, r5, r6, r7,')
[void]$sb.AppendLine('                                         r8, r9, rA, rB, rC, rD, rE, rF];')
[void]$sb.AppendLine('            foreach (var row in rows)')
[void]$sb.AppendLine('            {')
[void]$sb.AppendLine('                byte b = 0;')
[void]$sb.AppendLine('                for (int c = 0; c < GlyphWidth; c++)')
[void]$sb.AppendLine('                    if (row[c] == ''#'') b |= (byte)(0x80 >> c);')
[void]$sb.AppendLine('                data[offset++] = b;')
[void]$sb.AppendLine('            }')
[void]$sb.AppendLine('        }')
[void]$sb.AppendLine()

# ── Glyph data as ASCII art ──

for ($g = 0; $g -lt 256; $g++) {
    [void]$sb.AppendLine("        // 0x$($g.ToString('X2')) - $($names[$g])")
    [void]$sb.Append('        G(')
    for ($r = 0; $r -lt 16; $r++) {
        $art = ByteToArt $bytes[$g * 16 + $r]
        if ($r -gt 0) { [void]$sb.Append('          ') }
        [void]$sb.Append("`"$art`"")
        if ($r -lt 15) {
            [void]$sb.AppendLine(',')
        } else {
            [void]$sb.AppendLine(');')
        }
    }
    [void]$sb.AppendLine()
}

[void]$sb.AppendLine('        return data;')
[void]$sb.AppendLine('    }')
[void]$sb.AppendLine()

# ── Public API methods ──

[void]$sb.AppendLine('    /// <summary>')
[void]$sb.AppendLine('    /// Returns the glyph bitmap for a Unicode character (looked up via CP437 mapping).')
[void]$sb.AppendLine('    /// Returns null if the character is not in the CP437 set.')
[void]$sb.AppendLine('    /// </summary>')
[void]$sb.AppendLine('    public static bool[]? GetChar(char c)')
[void]$sb.AppendLine('    {')
[void]$sb.AppendLine('        for (int i = 1; i < 256; i++)')
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine('            if (Cp437ToUnicode[i] == c)')
[void]$sb.AppendLine('                return GetGlyphByIndex(i);')
[void]$sb.AppendLine('        }')
[void]$sb.AppendLine()
[void]$sb.AppendLine('        return null;')
[void]$sb.AppendLine('    }')
[void]$sb.AppendLine()
[void]$sb.AppendLine('    /// <summary>Gets the glyph bitmap for a CP437 index (0-255) as a bool array.</summary>')
[void]$sb.AppendLine('    private static bool[] GetGlyphByIndex(int index)')
[void]$sb.AppendLine('    {')
[void]$sb.AppendLine('        var glyph = new bool[GlyphWidth * GlyphHeight];')
[void]$sb.AppendLine('        int off = index * GlyphHeight;')
[void]$sb.AppendLine('        for (int row = 0; row < GlyphHeight; row++)')
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine('            byte b = FontData[off + row];')
[void]$sb.AppendLine('            for (int col = 0; col < GlyphWidth; col++)')
[void]$sb.AppendLine('            {')
[void]$sb.AppendLine('                glyph[row * GlyphWidth + col] = (b & (0x80 >> col)) != 0;')
[void]$sb.AppendLine('            }')
[void]$sb.AppendLine('        }')
[void]$sb.AppendLine('        return glyph;')
[void]$sb.AppendLine('    }')
[void]$sb.AppendLine()
[void]$sb.AppendLine('    /// <summary>Returns all glyphs as a dictionary of Unicode char to bool[] bitmap.</summary>')
[void]$sb.AppendLine('    public static Dictionary<char, bool[]> GetAllGlyphs()')
[void]$sb.AppendLine('    {')
[void]$sb.AppendLine('        var dict = new Dictionary<char, bool[]>();')
[void]$sb.AppendLine('        for (int i = 1; i < 256; i++)')
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine('            char c = Cp437ToUnicode[i];')
[void]$sb.AppendLine("            if (c != '\0' && !dict.ContainsKey(c))")
[void]$sb.AppendLine('                dict[c] = GetGlyphByIndex(i);')
[void]$sb.AppendLine('        }')
[void]$sb.AppendLine('        return dict;')
[void]$sb.AppendLine('    }')
[void]$sb.AppendLine('}')

# ── 5. Write output ─────────────────────────────────────────────────

Set-Content $outputPath $sb.ToString() -Encoding UTF8 -NoNewline
Write-Host "Generated $outputPath ($($sb.Length) chars)" -ForegroundColor Green
