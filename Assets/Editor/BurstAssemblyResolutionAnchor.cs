#if UNITY_EDITOR
namespace ChessGame.Editor
{
    /// <summary>
    /// Ensures Unity always emits the Assembly-CSharp-Editor assembly, even if optional tutorial
    /// editor assets are removed. Burst's entry-point scan can fail if that assembly is missing.
    /// </summary>
    internal static class BurstAssemblyResolutionAnchor
    {
    }
}
#endif
