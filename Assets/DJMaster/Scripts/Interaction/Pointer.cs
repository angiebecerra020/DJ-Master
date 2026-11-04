using System.Collections.Generic;
using UnityEngine;

namespace DJMaster
{
    // A fingertip (or the editor mouse) that can touch controls on the board.
    public class Pointer
    {
        public bool Active;
        public Vector3 Position;
        // Mouse clicks land directly inside a control, so they skip the "approach from above" check.
        public bool DirectPress;
        // The control currently holding this pointer, so one finger drives one control at a time.
        public DJControl Owner;

        public bool IsFree(DJControl c) => Active && (Owner == null || Owner == c);
    }

    public static class Pointers
    {
        public static readonly List<Pointer> All = new();

        public static Pointer Create()
        {
            var p = new Pointer();
            All.Add(p);
            return p;
        }

        public static void Remove(Pointer p) => All.Remove(p);
    }
}
