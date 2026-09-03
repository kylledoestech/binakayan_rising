namespace BinakayanRising.Data
{
    /// <summary>
    /// Support rank of a Kapatiran (Brotherhood) pair, as defined by
    /// <b>Capstone Table 3: Kapatiran (Brotherhood) Synergy Levels</b>.
    /// </summary>
    /// <remarks>
    /// The document names exactly three ranks — C, B and A (Maximum) — awarded in that order as
    /// two bonded units are repeatedly deployed adjacent to one another. <see cref="None"/> is an
    /// implementation-only member representing a pair that has not yet reached rank C, so that a
    /// default-initialised value is never mistaken for an earned rank.
    /// <para>
    /// The underlying values are ordered by strength (None &lt; C &lt; B &lt; A), so
    /// <c>rank &gt;= KapatiranRank.B</c> is a valid "at least rank B" test.
    /// </para>
    /// </remarks>
    public enum KapatiranRank
    {
        /// <summary>No bond has been established yet; no effects apply.</summary>
        None = 0,

        /// <summary>Rank C — the entry rank. Capstone Table 3 grants a lore dialogue only.</summary>
        C = 1,

        /// <summary>Rank B — the intermediate rank. Capstone Table 3 grants a single stat bonus.</summary>
        B = 2,

        /// <summary>Rank A — the maximum rank. Capstone Table 3 grants two stat bonuses.</summary>
        A = 3
    }
}
