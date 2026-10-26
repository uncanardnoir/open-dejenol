namespace Mordor.Core;

/// <summary>
/// The row click of the two shops that sell by alignment: the General Store's elaItem_event0000
/// (FRMCTYS.BAS:326-392) and the Confinement's elaComp_event0000 (FRMCITYC.BAS:302-370), which
/// are the same code over different shelves. Each alignment button is enabled where that row has
/// stock, and m0028/m002C is left at the last one enabled. Only when more than one is enabled is
/// it set to -1 and the "Select Alignment" frame (elaAlignments) shown - its P?C008, property 08,
/// is the Elastic's Visible, which the form data stores as 0 on both frames and on no other Elastic -
/// and the form waits there until a button is pressed; the frame is hidden again after (P?C008 =
/// False, FRMCTYS.BAS:369/371, FRMCITYC.BAS:347/351). With one alignment in stock that one is taken
/// at once and the frame never comes up; with none (a row whose stock has just run out) the
/// alignment chosen before is kept.
/// </summary>
public static class AlignmentChoice
{
    /// <summary>What a row click offers.</summary>
    /// <param name="Enabled">cmdAlign(i).Enabled, one per button, by stock.</param>
    /// <param name="Stocked">l0168 / l0144: how many buttons are enabled.</param>
    /// <param name="Last">The last enabled button, or -1 when none is.</param>
    public readonly record struct Offer(bool[] Enabled, int Stocked, int Last)
    {
        /// <summary>Whether the frame comes up and the form waits for a button (<c>If l0168% &gt; 1</c>).</summary>
        public bool Asks => Stocked > 1;

        /// <summary>
        /// The alignment the click leaves chosen: -1 while the frame waits for one, the only one in
        /// stock, or - with none - the one chosen before, which the loop never touched.
        /// </summary>
        public int Chosen(int before) => Asks ? -1 : Stocked == 1 ? Last : before;
    }

    /// <summary>The offer for a row whose shelf holds <paramref name="counts"/>, one per button.</summary>
    public static Offer For(IReadOnlyList<int> counts)
    {
        var enabled = new bool[counts.Count];
        int stocked = 0, last = -1;
        for (int a = 0; a < counts.Count; a++)
        {
            enabled[a] = counts[a] > 0;
            if (enabled[a]) { stocked++; last = a; }
        }
        return new Offer(enabled, stocked, last);
    }
}
