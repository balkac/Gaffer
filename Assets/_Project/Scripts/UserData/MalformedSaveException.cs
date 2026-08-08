using System;

namespace Gaffer.UserData
{
    /// <summary>
    /// Thrown by <see cref="SaveBinaryReader"/> the moment the bytes stop making sense — the stream ended
    /// mid-record, a length is absurd, a string index points at a string the file never defined. It is
    /// INTERNAL and never escapes the assembly: <see cref="BinarySaveSerializer.Deserialize"/> catches it
    /// and returns a <see cref="Gaffer.Common.Result{T}"/> failure, because a torn or hand-edited save is an
    /// expected outcome at the adapter boundary and not something the pure core should ever see
    /// (CONVENTIONS §4, UNITY.md §7 — never an unhandled crash at boot).
    /// <para>
    /// An exception rather than a bool-returning reader on purpose: a decoder is a few dozen reads deep by
    /// the time it hits the end of a truncated file, and threading a failure flag back through every one of
    /// them is exactly how an <c>IndexOutOfRangeException</c> gets left on the boot path. The throw makes
    /// "the file is short" unmissable at the one place that can do something about it.
    /// </para>
    /// </summary>
    internal sealed class MalformedSaveException : Exception
    {
        internal MalformedSaveException(string message)
            : base(message)
        {
        }
    }
}
