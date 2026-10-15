using UnityEngine;

namespace Sandbox.CharacterSystem
{
    // Shared ownership contract for the expression module and low-level facial debugger.
    public interface IFacialPreviewOwner
    {
        SkinnedMeshRenderer Face { get; }
        BlendShapeDebugger Debugger { get; }
        bool CanPreviewShape(string shape);
        void BeginDebuggerPreview(BlendShapeDebugger debugger);
        void EndDebuggerPreview(BlendShapeDebugger debugger);
    }
}
