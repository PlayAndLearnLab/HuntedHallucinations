using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The "Zombie Behaviour Spec" manual page. It is a PuzzleData so the existing book (GDDBookRenderer),
/// GDDPage pickups and BookCheckTracker all work with it unchanged, but it belongs to no junction.
/// ZombieManager creates it at runtime (no asset needed) and fills in the rules rolled for this run.
///
/// Image mode: pageArt is the empty template, and Overlays are full-page transparent images (one word each,
/// already placed in the MAY or WON'T column) that the book stacks on top of it.
/// Text mode (fallback when images are missing): the rules are written out in groundTruthSummary.
/// </summary>
public class ZombieRulesPageData : PuzzleData
{
    public readonly List<Sprite> Overlays = new List<Sprite>();
    public bool UsesImages => Overlays.Count > 0;
}
