namespace Mordor.Core;

/// <summary>
/// THEME.MID, the title tune, sets Expression (controller 11) to 0 on all five of its channels at
/// its first tick and never raises it again. The sound cards of 1995 ignored Expression, so the
/// tune played. A synthesizer that obeys it, as Windows' own does, plays all two and a half
/// minutes with every note at nothing. It is the only one of the sixteen MIDIs like that: GEN3SC
/// also sends Expression 0, but as part of 512 Expression changes that shape its notes.
///
/// <see cref="Unmuted"/> gives the tune as those cards played it: every Expression value set back
/// to 127, the controller's default, which is the same as the controller being ignored. Nothing
/// else changes, not even a byte's position. Only a file whose every Expression is 0 is touched.
/// </summary>
public static class MidiExpression
{
    /// <summary>
    /// The file with its Expression values at 127, or null when it needs nothing: no Expression
    /// at all, or any Expression above 0, or anything this reader cannot follow.
    /// </summary>
    public static byte[]? Unmuted(byte[] midi)
    {
        var values = new List<int>();   // the offset of each Expression event's value byte
        bool raised = false;
        try
        {
            if (midi.Length < 14 || midi[0] != 'M' || midi[1] != 'T' || midi[2] != 'h' || midi[3] != 'd') return null;
            int pos = 8 + (midi[4] << 24 | midi[5] << 16 | midi[6] << 8 | midi[7]);
            while (pos + 8 <= midi.Length)
            {
                int len = midi[pos + 4] << 24 | midi[pos + 5] << 16 | midi[pos + 6] << 8 | midi[pos + 7];
                int p = pos + 8, end = p + len;
                if (len < 0 || end > midi.Length) return null;
                bool track = midi[pos] == 'M' && midi[pos + 1] == 'T' && midi[pos + 2] == 'r' && midi[pos + 3] == 'k';
                int status = 0;
                while (track && p < end)
                {
                    while ((midi[p++] & 0x80) != 0) { }   // the delta time
                    int b = midi[p];
                    // the length is read first: "p += VarLen(ref p)" would add it to p as it was before the read
                    if (b == 0xFF) { p += 2; int meta = VarLen(midi, ref p); p += meta; continue; }
                    if (b is 0xF0 or 0xF7) { p++; int sysex = VarLen(midi, ref p); p += sysex; continue; }
                    if ((b & 0x80) != 0) { status = b; p++; }
                    else if (status == 0) return null;
                    int kind = status & 0xF0;
                    if (kind is 0xC0 or 0xD0) { p += 1; continue; }
                    if (kind == 0xB0 && midi[p] == 11)
                    {
                        if (midi[p + 1] == 0) values.Add(p + 1);
                        else raised = true;
                    }
                    p += 2;
                }
                pos = end;
            }
        }
        catch (IndexOutOfRangeException) { return null; }
        if (raised || values.Count == 0) return null;
        var copy = (byte[])midi.Clone();
        foreach (int at in values) copy[at] = 127;
        return copy;
    }

    private static int VarLen(byte[] b, ref int p)
    {
        int v = 0;
        int c;
        do { c = b[p++]; v = v << 7 | (c & 0x7F); } while ((c & 0x80) != 0);
        return v;
    }
}
